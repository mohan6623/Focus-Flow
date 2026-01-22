using System.Text.Json;
using System.IO;
using ScreenTimeTracker.Utilities;

namespace ScreenTimeTracker.Config;

/// <summary>
/// Application configuration settings.
/// </summary>
public class AppSettings
{
    /// <summary>
    /// Whether to start tracking automatically on launch.
    /// </summary>
    public bool AutoStartTracking { get; set; } = true;

    /// <summary>
    /// Whether to start with Windows.
    /// </summary>
    public bool StartWithWindows { get; set; } = true;

    /// <summary>
    /// Idle timeout in minutes before pausing tracking.
    /// </summary>
    public int IdleTimeoutMinutes { get; set; } = 5;

    /// <summary>
    /// How often to persist data (in minutes).
    /// </summary>
    public int PersistIntervalMinutes { get; set; } = 5;

    /// <summary>
    /// Minimum session duration in seconds to record.
    /// </summary>
    public int MinSessionDurationSeconds { get; set; } = 1;

    /// <summary>
    /// Whether to show the tray icon.
    /// </summary>
    public bool ShowTrayIcon { get; set; } = true;

    /// <summary>
    /// Whether sync is enabled.
    /// </summary>
    public bool SyncEnabled { get; set; } = false;

    /// <summary>
    /// Backend API URL for sync.
    /// </summary>
    public string? ApiBaseUrl { get; set; }

    /// <summary>
    /// Sync interval in minutes.
    /// </summary>
    public int SyncIntervalMinutes { get; set; } = 15;

    /// <summary>
    /// List of apps to exclude from tracking.
    /// </summary>
    public List<string> ExcludedApps { get; set; } = new()
    {
        "LockApp",
        "SearchHost",
        "StartMenuExperienceHost",
        "ShellExperienceHost"
    };

    /// <summary>
    /// Loads settings from the default config file.
    /// </summary>
    public static AppSettings Load()
    {
        var configPath = GetConfigPath();

        if (!File.Exists(configPath))
        {
            return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(configPath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    /// <summary>
    /// Saves settings to the default config file.
    /// </summary>
    public void Save()
    {
        var configPath = GetConfigPath();
        var directory = Path.GetDirectoryName(configPath);

        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(this, options);
        File.WriteAllText(configPath, json);
    }

    private static string GetConfigPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "ScreenTimeTracker", "settings.json");
    }
}
