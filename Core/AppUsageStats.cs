namespace ScreenTimeTracker.Core;

/// <summary>
/// Aggregated usage statistics for a single application on a specific day.
/// This is what gets displayed to the user (MIUI-style).
/// </summary>
public class AppUsageStats
{
    /// <summary>
    /// Name of the application.
    /// </summary>
    public string AppName { get; }

    /// <summary>
    /// The date these stats are for.
    /// </summary>
    public DateOnly Date { get; }

    /// <summary>
    /// Total time spent in this app for the day.
    /// </summary>
    public TimeSpan TotalTime { get; private set; }

    /// <summary>
    /// Number of times the app was brought to foreground.
    /// </summary>
    public int SessionCount { get; private set; }

    /// <summary>
    /// First time the app was used today.
    /// </summary>
    public DateTime FirstUsed { get; private set; }

    /// <summary>
    /// Last time the app was used today.
    /// </summary>
    public DateTime LastUsed { get; private set; }

    /// <summary>
    /// Average session duration.
    /// </summary>
    public TimeSpan AverageSessionDuration => 
        SessionCount > 0 ? TimeSpan.FromTicks(TotalTime.Ticks / SessionCount) : TimeSpan.Zero;

    public AppUsageStats(string appName, DateOnly date)
    {
        AppName = appName;
        Date = date;
        TotalTime = TimeSpan.Zero;
        SessionCount = 0;
        FirstUsed = DateTime.MaxValue;
        LastUsed = DateTime.MinValue;
    }

    /// <summary>
    /// Adds a completed session to these stats.
    /// </summary>
    public void AddSession(UsageSession session)
    {
        if (session.AppName != AppName)
        {
            throw new ArgumentException($"Session app '{session.AppName}' doesn't match stats app '{AppName}'");
        }

        TotalTime += session.Duration;
        SessionCount++;

        if (session.StartTime < FirstUsed)
        {
            FirstUsed = session.StartTime;
        }

        if (session.EndTime > LastUsed)
        {
            LastUsed = session.EndTime;
        }
    }

    /// <summary>
    /// Merges another stats object into this one (for combining loaded data with live data).
    /// </summary>
    public void Merge(AppUsageStats other)
    {
        if (other.AppName != AppName || other.Date != Date)
        {
            throw new ArgumentException("Cannot merge stats from different apps or dates");
        }

        TotalTime += other.TotalTime;
        SessionCount += other.SessionCount;

        if (other.FirstUsed < FirstUsed)
        {
            FirstUsed = other.FirstUsed;
        }

        if (other.LastUsed > LastUsed)
        {
            LastUsed = other.LastUsed;
        }
    }

    /// <summary>
    /// Creates a formatted summary string (MIUI-style).
    /// </summary>
    public string GetFormattedSummary()
    {
        var timeStr = TotalTime.TotalHours >= 1
            ? $"{(int)TotalTime.TotalHours}h {TotalTime.Minutes}m"
            : $"{TotalTime.Minutes}m {TotalTime.Seconds}s";

        return $"{AppName}: {timeStr} ({SessionCount} sessions)";
    }

    public override string ToString() => GetFormattedSummary();
}
