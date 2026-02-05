using ScreenTimeTracker.Core;
using ScreenTimeTracker.Storage;
using ScreenTimeTracker.Tracking;
using ScreenTimeTracker.Utilities;

namespace ScreenTimeTracker.Services;

/// <summary>
/// Aggregates session data into daily statistics.
/// Manages in-memory stats and periodic persistence.
/// </summary>
public class AggregationService : IDisposable
{
    private readonly UsageRepository _repository;
    private readonly Dictionary<string, AppUsageStats> _todayStats;
    private readonly List<UsageSession> _pendingSessions;
    private readonly object _statsLock = new();
    private readonly global::System.Threading.Timer _persistTimer;
    
    private DateOnly _currentDate;
    private bool _disposed;
    private bool _isDirty;

    /// <summary>
    /// How often to persist data to disk.
    /// </summary>
    public TimeSpan PersistInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Raised when stats are updated.
    /// </summary>
    public event EventHandler? StatsUpdated;

    public AggregationService(UsageRepository repository)
    {
        _repository = repository;
        _todayStats = new Dictionary<string, AppUsageStats>(StringComparer.OrdinalIgnoreCase);
        _pendingSessions = new List<UsageSession>();
        _currentDate = DateOnly.FromDateTime(DateTime.Now);
        _persistTimer = new global::System.Threading.Timer(PersistCallback, null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>
    /// Starts the aggregation service.
    /// </summary>
    public void Start()
    {
        _repository.Initialize();
        LoadTodayStats(); // Load data before starting timer
        _persistTimer.Change(PersistInterval, PersistInterval);
        Logger.Info($"Aggregation service started (persist interval: {PersistInterval.TotalMinutes}m)");
    }

    private void LoadTodayStats()
    {
        try
        {
            var stats = _repository.GetDailyStats(_currentDate);
            lock (_statsLock)
            {
                foreach (var s in stats)
                {
                    _todayStats[s.AppName] = s;
                }
            }
            if (stats.Count > 0)
            {
                Logger.Info($"Loaded {stats.Count} app stats for {_currentDate}");
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to load today's stats: {ex.Message}");
        }
    }

    /// <summary>
    /// Stops the aggregation service and persists remaining data.
    /// </summary>
    public void Stop()
    {
        _persistTimer.Change(Timeout.Infinite, Timeout.Infinite);
        PersistNow();
        Logger.Info("Aggregation service stopped");
    }

    /// <summary>
    /// Handles a completed session event.
    /// </summary>
    public void OnSessionCompleted(object? sender, SessionCompletedEventArgs e)
    {
        AddSession(e.Session);
    }

    /// <summary>
    /// Adds a session to the statistics.
    /// Immediately persists to prevent data loss on unexpected shutdown.
    /// </summary>
    public void AddSession(UsageSession session)
    {
        lock (_statsLock)
        {
            CheckDateRollover();

            if (!_todayStats.TryGetValue(session.AppName, out var stats))
            {
                stats = new AppUsageStats(session.AppName, _currentDate);
                _todayStats[session.AppName] = stats;
            }

            stats.AddSession(session);
            _pendingSessions.Add(session);
            _isDirty = true;

            Logger.Debug($"Added session to {session.AppName}: {session.Duration.TotalSeconds:F1}s");
        }

        // Immediately persist to prevent data loss on unexpected shutdown
        // This is critical - we don't want to lose any session data
        PersistNow();

        try
        {
            StatsUpdated?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Logger.Error($"Error in StatsUpdated handler: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets today's stats for all apps.
    /// </summary>
    public List<AppUsageStats> GetTodayStats()
    {
        lock (_statsLock)
        {
            return _todayStats.Values.OrderByDescending(s => s.TotalTime).ToList();
        }
    }

    /// <summary>
    /// Gets today's stats for a specific app.
    /// </summary>
    public AppUsageStats? GetAppStats(string appName)
    {
        lock (_statsLock)
        {
            return _todayStats.TryGetValue(appName, out var stats) ? stats : null;
        }
    }

    /// <summary>
    /// Gets total screen time for today.
    /// </summary>
    public TimeSpan GetTotalScreenTimeToday()
    {
        lock (_statsLock)
        {
            return _todayStats.Values.Aggregate(TimeSpan.Zero, (sum, s) => sum + s.TotalTime);
        }
    }

    /// <summary>
    /// Gets the top N apps by usage time today.
    /// </summary>
    public List<AppUsageStats> GetTopAppsToday(int count = 10)
    {
        lock (_statsLock)
        {
            return _todayStats.Values
                .OrderByDescending(s => s.TotalTime)
                .Take(count)
                .ToList();
        }
    }

    /// <summary>
    /// Gets usage history for the last N days.
    /// Used for the Daily Report graph.
    /// </summary>
    public Dictionary<DateOnly, TimeSpan> GetDailyHistory(int days = 7)
    {
        var endDate = DateOnly.FromDateTime(DateTime.Now);
        var startDate = endDate.AddDays(-days + 1);
        
        // Fetch from repo
        var history = _repository.GetUsageHistory(startDate, endDate);
        
        // Ensure today's in-memory stats are included/overridden if repo is lagging
        // (Repo is updated periodically, so in-memory is fresher for today)
        lock (_statsLock)
        {
            history[endDate] = GetTotalScreenTimeToday();
        }

        return history;
    }

    /// <summary>
    /// Gets all sessions for a specific date (for hourly breakdown view).
    /// </summary>
    public List<UsageSession> GetSessionsForDate(DateOnly date)
    {
        // First check if it's today and we have pending sessions in memory
        var today = DateOnly.FromDateTime(DateTime.Now);
        
        if (date == today)
        {
            // For today, combine DB sessions + pending (in-memory) sessions
            var dbSessions = _repository.GetSessionsForDate(date);
            lock (_statsLock)
            {
                dbSessions.AddRange(_pendingSessions.Where(s => s.Date == date));
            }
            return dbSessions.OrderBy(s => s.StartTime).ToList();
        }
        
        // For past days, just get from DB
        return _repository.GetSessionsForDate(date);
    }

    /// <summary>
    /// Forces immediate persistence to disk.
    /// </summary>
    public void PersistNow()
    {
        List<AppUsageStats> statsToPersist;

        lock (_statsLock)
        {
            if (!_isDirty || _todayStats.Count == 0)
            {
                return;
            }

            statsToPersist = _todayStats.Values.ToList();
            _isDirty = false;
        }

        // Grab pending sessions
        List<UsageSession> sessionsToPersist;
        lock (_statsLock)
        {
            sessionsToPersist = _pendingSessions.ToList();
            _pendingSessions.Clear();
        }

        try
        {
            _repository.SaveUsageBatch(statsToPersist);
            if (sessionsToPersist.Count > 0)
            {
                _repository.SaveSessionsBatch(sessionsToPersist);
            }
            Logger.Debug($"Persisted {statsToPersist.Count} app stats and {sessionsToPersist.Count} sessions");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to persist stats: {ex.Message}");
            
            // Mark as dirty again so we retry
            lock (_statsLock)
            {
                _isDirty = true;
            }
        }
    }

    private void PersistCallback(object? state)
    {
        try
        {
            PersistNow();
        }
        catch (Exception ex)
        {
            Logger.Error($"Error in persist callback: {ex.Message}");
        }
    }

    private void CheckDateRollover()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        
        if (today == _currentDate)
            return;

        Logger.Info($"Date rollover detected: {_currentDate} -> {today}");

        // Persist previous day's data
        if (_todayStats.Count > 0)
        {
            try
            {
                _repository.SaveUsageBatch(_todayStats.Values.ToList());
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to persist on date rollover: {ex.Message}");
            }
        }

        // Clear for new day
        _todayStats.Clear();
        _currentDate = today;
        _isDirty = false;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Stop();
        _persistTimer.Dispose();
        _repository.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~AggregationService()
    {
        Dispose();
    }
}
