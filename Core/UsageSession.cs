namespace ScreenTimeTracker.Core;

/// <summary>
/// Represents a single usage session - one continuous period where an app was in foreground.
/// Immutable record for thread safety and data integrity.
/// </summary>
public sealed record UsageSession
{
    /// <summary>
    /// Name of the application (process name without extension, or website domain for browsers).
    /// </summary>
    public string AppName { get; }

    /// <summary>
    /// Full path to the executable file.
    /// </summary>
    public string? AppPath { get; }

    /// <summary>
    /// Process ID during this session.
    /// </summary>
    public int ProcessId { get; }

    /// <summary>
    /// Window title at the start of the session (may be null).
    /// </summary>
    public string? WindowTitle { get; }

    /// <summary>
    /// Website domain if this session is a browser tab (e.g., "youtube.com").
    /// </summary>
    public string? WebsiteDomain { get; }

    /// <summary>
    /// When the app became foreground.
    /// </summary>
    public DateTime StartTime { get; }

    /// <summary>
    /// When the app lost foreground.
    /// </summary>
    public DateTime EndTime { get; }

    /// <summary>
    /// Duration of this session.
    /// </summary>
    public TimeSpan Duration => EndTime - StartTime;

    /// <summary>
    /// The date this session belongs to (for daily aggregation).
    /// Uses the start time's date.
    /// </summary>
    public DateOnly Date => DateOnly.FromDateTime(StartTime);

    /// <summary>
    /// Indicates if this session is for a website in a browser.
    /// </summary>
    public bool IsWebsite => !string.IsNullOrEmpty(WebsiteDomain);

    public UsageSession(string appName, int processId, string? windowTitle, DateTime startTime, DateTime endTime, string? websiteDomain = null, string? appPath = null)
    {
        if (string.IsNullOrWhiteSpace(appName))
            throw new ArgumentException("App name cannot be empty", nameof(appName));
        
        if (endTime < startTime)
            throw new ArgumentException("End time cannot be before start time", nameof(endTime));

        AppName = appName;
        AppPath = appPath;
        ProcessId = processId;
        WindowTitle = windowTitle;
        WebsiteDomain = websiteDomain;
        StartTime = startTime;
        EndTime = endTime;
    }

    /// <summary>
    /// Creates a session with the current time as end time.
    /// </summary>
    public static UsageSession CreateEndingNow(string appName, int processId, string? windowTitle, DateTime startTime, string? websiteDomain = null, string? appPath = null)
    {
        return new UsageSession(appName, processId, windowTitle, startTime, DateTime.Now, websiteDomain, appPath);
    }

    public override string ToString()
    {
        var name = IsWebsite ? $"{AppName} ({WebsiteDomain})" : AppName;
        return $"{name}: {Duration.TotalSeconds:F1}s ({StartTime:HH:mm:ss} - {EndTime:HH:mm:ss})";
    }
}

