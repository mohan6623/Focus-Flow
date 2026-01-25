using ScreenTimeTracker.Utilities;

namespace ScreenTimeTracker.Services;

/// <summary>
/// Manages focus sessions and tracks productivity.
/// </summary>
public class FocusService
{
    private readonly Storage.UsageRepository _repository;
    private bool _isInFocusMode;
    private DateTime _focusStartTime;
    private int _todaySessionCount;
    private TimeSpan _todayFocusTime;

    public bool IsInFocusMode => _isInFocusMode;
    public int TodaySessionCount => _todaySessionCount;
    public TimeSpan TodayFocusTime => _todayFocusTime;

    public event EventHandler? FocusModeChanged;

    public FocusService(Storage.UsageRepository repository)
    {
        _repository = repository;
        LoadTodayStats();
    }

    private void LoadTodayStats()
    {
        // TODO: Load from database
        _todaySessionCount = 0;
        _todayFocusTime = TimeSpan.Zero;
    }

    public void StartFocusSession()
    {
        _isInFocusMode = true;
        _focusStartTime = DateTime.Now;
        FocusModeChanged?.Invoke(this, EventArgs.Empty);
        Logger.Info("Focus session started");
    }

    public void EndFocusSession(bool completed)
    {
        if (!_isInFocusMode) return;

        var duration = DateTime.Now - _focusStartTime;
        _isInFocusMode = false;

        if (completed)
        {
            _todaySessionCount++;
            _todayFocusTime += duration;
            Logger.Info($"Focus session completed: {duration.TotalMinutes:F1} minutes");
            
            // TODO: Save to database
        }
        else
        {
            Logger.Info($"Focus session cancelled after {duration.TotalMinutes:F1} minutes");
        }

        FocusModeChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Gets apps that should be blocked during focus mode.
    /// </summary>
    public List<string> GetBlockedApps()
    {
        // TODO: Get from category service
        return new List<string>
        {
            "Discord",
            "Spotify",
            "Steam",
            "YouTube",
            "Netflix",
            "Twitter",
            "Facebook"
        };
    }
}
