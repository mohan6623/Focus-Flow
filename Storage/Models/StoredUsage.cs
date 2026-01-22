namespace ScreenTimeTracker.Storage.Models;

/// <summary>
/// Database model for stored usage data.
/// </summary>
public class StoredUsage
{
    /// <summary>
    /// Unique identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Application name (process name).
    /// </summary>
    public string AppName { get; set; } = string.Empty;

    /// <summary>
    /// Date of the usage (YYYY-MM-DD format).
    /// </summary>
    public string Date { get; set; } = string.Empty;

    /// <summary>
    /// Total time in seconds for this day.
    /// </summary>
    public long TotalSeconds { get; set; }

    /// <summary>
    /// Number of sessions for this day.
    /// </summary>
    public int SessionCount { get; set; }

    /// <summary>
    /// First usage timestamp (ISO 8601).
    /// </summary>
    public string FirstUsed { get; set; } = string.Empty;

    /// <summary>
    /// Last usage timestamp (ISO 8601).
    /// </summary>
    public string LastUsed { get; set; } = string.Empty;

    /// <summary>
    /// When this record was last updated.
    /// </summary>
    public string UpdatedAt { get; set; } = string.Empty;
}
