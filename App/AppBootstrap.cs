using ScreenTimeTracker.Config;
using ScreenTimeTracker.Services;
using ScreenTimeTracker.Storage;
using ScreenTimeTracker.Tracking;
using ScreenTimeTracker.Utilities;
using ScreenTimeTracker.SystemUtils;

namespace ScreenTimeTracker.App;

/// <summary>
/// Wires up all application dependencies and manages lifecycle.
/// </summary>
public class AppBootstrap : IDisposable
{
    private readonly AppSettings _settings;
    
    // System layer
    private readonly ProcessResolver _processResolver;
    private readonly WindowInfoProvider _windowInfoProvider;
    
    // Tracking layer
    private readonly ForegroundAppTracker _foregroundTracker;
    private readonly SessionTracker _sessionTracker;
    private readonly LockStateMonitor _lockStateMonitor;
    private readonly IdleDetector _idleDetector;
    
    // Storage and services
    private readonly UsageRepository _repository;
    private readonly AggregationService _aggregationService;
    private readonly SyncService _syncService;
    private readonly SystemTimeService _systemTimeService;
    private readonly CategoryService _categoryService;
    private readonly BrowserTabResolver _browserTabResolver;
    private readonly FocusService _focusService;
    private readonly AppBlockerService _appBlockerService;
    private readonly StreakService _streakService;
    
    // UI
    private DashboardWindow? _dashboardWindow;
    
    // Tray
    private readonly TrayIconManager _trayIconManager;

    // Theme
    private readonly ThemeManager _themeManager;

    private bool _isRunning;
    private bool _disposed;

    public AppBootstrap()
    {
        // Load settings
        _settings = AppSettings.Load();

        // Initialize system layer
        _processResolver = new ProcessResolver();
        _windowInfoProvider = new WindowInfoProvider();
        _browserTabResolver = new BrowserTabResolver();

        // Initialize tracking layer
        _foregroundTracker = new ForegroundAppTracker(_windowInfoProvider, _processResolver, _browserTabResolver);
        _sessionTracker = new SessionTracker
        {
            MinimumSessionDuration = TimeSpan.FromSeconds(_settings.MinSessionDurationSeconds)
        };
        _lockStateMonitor = new LockStateMonitor();
        _idleDetector = new IdleDetector
        {
            IdleThreshold = TimeSpan.FromMinutes(_settings.IdleTimeoutMinutes)
        };

        // Initialize storage and services
        _repository = new UsageRepository();
        _aggregationService = new AggregationService(_repository)
        {
            PersistInterval = TimeSpan.FromMinutes(_settings.PersistIntervalMinutes)
        };
        _syncService = new SyncService
        {
            IsEnabled = _settings.SyncEnabled,
            ApiBaseUrl = _settings.ApiBaseUrl,
            SyncInterval = TimeSpan.FromMinutes(_settings.SyncIntervalMinutes)
        };
        
        // Initialize system time service
        _systemTimeService = new SystemTimeService();
        
        // Initialize category service
        _categoryService = new CategoryService(_repository);

        // Initialize focus and app blocker services
        _focusService = new FocusService(_repository);
        _appBlockerService = new AppBlockerService(_focusService, _foregroundTracker);
        
        // Initialize streak service
        _streakService = new StreakService(_repository);

        // Initialize theme manager
        _themeManager = new ThemeManager();
        _themeManager.ApplyTheme(ThemeType.Light);

        // Initialize tray
        _trayIconManager = new TrayIconManager();

        // Wire up events
        WireEvents();
    }

    /// <summary>
    /// Starts all tracking components.
    /// </summary>
    public void Start()
    {
        if (_isRunning)
            return;

        Logger.Initialize();
        Logger.Info("Application starting...");

        try
        {
            // Start services
            _aggregationService.Start();
            _syncService.Start();

            // Start trackers
            _lockStateMonitor.Start();
            _idleDetector.Start();
            _foregroundTracker.Start();

            // Show tray icon
            if (_settings.ShowTrayIcon)
            {
                _trayIconManager.Show();
            }

            _isRunning = true;
            Logger.Info("Application started successfully");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to start application");
            throw;
        }
    }

    /// <summary>
    /// Stops all tracking components.
    /// </summary>
    public void Stop()
    {
        if (!_isRunning)
            return;

        Logger.Info("Application stopping...");

        try
        {
            // End current session and persist
            _sessionTracker.EndCurrentSession();

            // Stop trackers
            _foregroundTracker.Stop();
            _idleDetector.Stop();
            _lockStateMonitor.Stop();

            // Stop services
            _syncService.Stop();
            _aggregationService.Stop();

            // Hide tray
            _trayIconManager.Hide();

            _isRunning = false;
            Logger.Info("Application stopped");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during shutdown");
        }
        finally
        {
            Logger.Flush();
        }
    }

    /// <summary>
    /// Pauses tracking temporarily.
    /// </summary>
    public void PauseTracking()
    {
        _sessionTracker.Pause();
        _trayIconManager.SetPaused(true);
        Logger.Info("Tracking paused by user");
    }

    /// <summary>
    /// Resumes tracking.
    /// </summary>
    public void ResumeTracking()
    {
        _sessionTracker.Resume();
        _foregroundTracker.CheckCurrentForeground();
        _trayIconManager.SetPaused(false);
        Logger.Info("Tracking resumed by user");
    }

    /// <summary>
    /// Gets today's total screen time.
    /// </summary>
    public TimeSpan GetTodayScreenTime()
    {
        return _aggregationService.GetTotalScreenTimeToday();
    }

    /// <summary>
    /// Gets the current tracking session info.
    /// </summary>
    public (string? AppName, TimeSpan Duration) GetCurrentSession()
    {
        return _sessionTracker.GetCurrentSession();
    }

    private void WireEvents()
    {
        // Foreground changes -> Session tracker
        _foregroundTracker.ForegroundChanged += _sessionTracker.OnForegroundChanged;

        // Completed sessions -> Aggregation
        _sessionTracker.SessionCompleted += _aggregationService.OnSessionCompleted;

        // Lock state -> Session tracker
        _lockStateMonitor.LockStateChanged += (s, e) =>
        {
            if (e.IsLocked)
            {
                _sessionTracker.Pause(isLocked: true);
            }
            else
            {
                _sessionTracker.Resume();
                _foregroundTracker.CheckCurrentForeground();
            }
        };

        // Idle state -> Session tracker
        _idleDetector.IdleStateChanged += (s, e) =>
        {
            if (e.IsIdle)
            {
                _sessionTracker.Pause(isIdle: true);
            }
            else
            {
                _sessionTracker.Resume();
                _foregroundTracker.CheckCurrentForeground();
            }
        };

        // Tray menu actions
        _trayIconManager.PauseRequested += (s, e) => PauseTracking();
        _trayIconManager.ResumeRequested += (s, e) => ResumeTracking();
        _trayIconManager.ExitRequested += (s, e) =>
        {
            Stop();
            Application.Exit();
        };

        // View Stats
        _trayIconManager.StatsRequested += (s, e) => ShowStats();

        // Update tray tooltip with current stats
        _aggregationService.StatsUpdated += (s, e) =>
        {
            var totalTime = _aggregationService.GetTotalScreenTimeToday();
            _trayIconManager.UpdateTooltip($"Screen Time: {FormatTime(totalTime)}");
        };
    }

    private static string FormatTime(TimeSpan time)
    {
        if (time.TotalHours >= 1)
        {
            return $"{(int)time.TotalHours}h {time.Minutes}m";
        }
        return $"{time.Minutes}m {time.Seconds}s";
    }

    private void ShowStats()
    {
        // Ensure WPF Application exists (required for resource lookup)
        if (System.Windows.Application.Current == null)
        {
            new System.Windows.Application();
            // Apply theme after WPF Application is created
            _themeManager.ApplyTheme(_themeManager.CurrentTheme);
        }
        
        if (_dashboardWindow == null || !_dashboardWindow.IsLoaded)
        {
            _dashboardWindow = new DashboardWindow(_aggregationService, _systemTimeService, _trayIconManager!, _focusService, _appBlockerService, _foregroundTracker, _themeManager, _streakService, _categoryService);
            _dashboardWindow.Closed += (s, e) => _dashboardWindow = null;
        }

        if (!_dashboardWindow.IsVisible)
        {
            _dashboardWindow.Show();
        }
        else
        {
            _dashboardWindow.Activate();
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Stop();

        _trayIconManager.Dispose();
        _foregroundTracker.Dispose();
        _lockStateMonitor.Dispose();
        _idleDetector.Dispose();
        _aggregationService.Dispose();
        _syncService.Dispose();
        _repository.Dispose();

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~AppBootstrap()
    {
        Dispose();
    }
}
