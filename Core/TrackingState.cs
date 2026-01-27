namespace ScreenTimeTracker.Core;

/// <summary>
/// Represents the current tracking state - which app is currently in foreground
/// and when it became active.
/// </summary>
public class TrackingState
{
    /// <summary>
    /// Name of the currently active application (process name or website domain).
    /// </summary>
    public string? CurrentAppName { get; private set; }

    /// <summary>
    /// Process ID of the currently active application.
    /// </summary>
    public int CurrentProcessId { get; private set; }

    /// <summary>
    /// Window title of the currently active window.
    /// </summary>
    public string? CurrentWindowTitle { get; private set; }

    /// <summary>
    /// Website domain if the current app is a browser.
    /// </summary>
    public string? CurrentWebsiteDomain { get; private set; }

    /// <summary>
    /// Full path to the executable of the current app.
    /// </summary>
    public string? CurrentAppPath { get; private set; }

    /// <summary>
    /// Timestamp when the current app became foreground.
    /// </summary>
    public DateTime SessionStartTime { get; private set; }

    /// <summary>
    /// Indicates whether tracking is currently active.
    /// </summary>
    public bool IsTracking { get; private set; }

    /// <summary>
    /// Indicates whether the user is currently idle.
    /// </summary>
    public bool IsIdle { get; private set; }

    /// <summary>
    /// Indicates whether the screen is locked.
    /// </summary>
    public bool IsLocked { get; private set; }

    /// <summary>
    /// Starts tracking a new foreground application.
    /// </summary>
    public UsageSession? StartNewSession(string appName, int processId, string? windowTitle, string? websiteDomain, DateTime timestamp, string? appPath)
    {
        UsageSession? completedSession = null;

        // If there's an existing session, complete it first
        if (IsTracking && !string.IsNullOrEmpty(CurrentAppName))
        {
            completedSession = CompleteCurrentSession(timestamp);
        }

        CurrentAppName = appName;
        CurrentProcessId = processId;
        CurrentWindowTitle = windowTitle;
        CurrentWebsiteDomain = websiteDomain;
        CurrentAppPath = appPath;
        SessionStartTime = timestamp;
        IsTracking = true;

        return completedSession;
    }

    /// <summary>
    /// Completes the current session and returns the usage data.
    /// </summary>
    public UsageSession? CompleteCurrentSession(DateTime endTime)
    {
        if (!IsTracking || string.IsNullOrEmpty(CurrentAppName))
        {
            return null;
        }

        var session = new UsageSession(
            CurrentAppName,
            CurrentProcessId,
            CurrentWindowTitle,
            SessionStartTime,
            endTime,
            CurrentWebsiteDomain,
            CurrentAppPath
        );

        IsTracking = false;
        CurrentAppName = null;
        CurrentProcessId = 0;
        CurrentWindowTitle = null;
        CurrentWebsiteDomain = null;
        CurrentAppPath = null;

        return session;
    }

    /// <summary>
    /// Pauses tracking (e.g., when screen is locked or user is idle).
    /// </summary>
    public UsageSession? PauseTracking(DateTime timestamp, bool isLocked = false, bool isIdle = false)
    {
        IsLocked = isLocked;
        IsIdle = isIdle;
        return CompleteCurrentSession(timestamp);
    }

    /// <summary>
    /// Resumes tracking after being paused.
    /// </summary>
    public void ResumeTracking()
    {
        IsLocked = false;
        IsIdle = false;
    }

    /// <summary>
    /// Checks if tracking should be active based on lock and idle state.
    /// </summary>
    public bool ShouldTrack => !IsLocked && !IsIdle;
}
