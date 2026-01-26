using ScreenTimeTracker.Tracking;
using ScreenTimeTracker.Utilities;

namespace ScreenTimeTracker.Services;

/// <summary>
/// Event args for when a blocked app is detected during focus mode.
/// </summary>
public class BlockedAppEventArgs : EventArgs
{
    public string AppName { get; }
    public DateTime Timestamp { get; }

    public BlockedAppEventArgs(string appName)
    {
        AppName = appName;
        Timestamp = DateTime.Now;
    }
}

/// <summary>
/// Monitors foreground apps during focus sessions and raises events when blocked apps are detected.
/// </summary>
public class AppBlockerService : IDisposable
{
    private readonly FocusService _focusService;
    private readonly ForegroundAppTracker _foregroundTracker;
    
    private readonly HashSet<string> _blockedApps;
    private readonly HashSet<string> _sessionWhitelist; // Apps allowed "just this time"
    private int _blockedAttemptCount;
    private bool _disposed;

    /// <summary>
    /// Raised when a blocked app is detected during focus mode.
    /// </summary>
    public event EventHandler<BlockedAppEventArgs>? BlockedAppDetected;

    /// <summary>
    /// Number of times blocked apps were opened this session.
    /// </summary>
    public int BlockedAttemptCount => _blockedAttemptCount;

    public AppBlockerService(FocusService focusService, ForegroundAppTracker foregroundTracker)
    {
        _focusService = focusService;
        _foregroundTracker = foregroundTracker;
        _sessionWhitelist = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _blockedAttemptCount = 0;

        // Initialize blocked apps list
        _blockedApps = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Discord",
            "Spotify",
            "Steam",
            "steamwebhelper",
            "Netflix",
            "Twitter",
            "Facebook",
            "Instagram",
            "TikTok",
            "Slack",
            "Telegram",
            "WhatsApp",
            "Messenger",
            // Browser-detected sites (from BrowserTabResolver)
            "YouTube",
            "Reddit",
            "Twitch"
        };

        // Subscribe to foreground changes
        _foregroundTracker.ForegroundChanged += OnForegroundChanged;
        
        // Reset whitelist when focus session ends
        _focusService.FocusModeChanged += OnFocusModeChanged;
    }

    /// <summary>
    /// Checks if an app is in the blocked list.
    /// </summary>
    public bool IsBlocked(string appName)
    {
        if (string.IsNullOrEmpty(appName))
            return false;

        return _blockedApps.Contains(appName);
    }

    /// <summary>
    /// Allows an app for the current focus session only.
    /// </summary>
    public void AllowForSession(string appName)
    {
        if (!string.IsNullOrEmpty(appName))
        {
            _sessionWhitelist.Add(appName);
            Logger.Info($"App '{appName}' allowed for this focus session");
        }
    }

    /// <summary>
    /// Gets the list of currently blocked apps.
    /// </summary>
    public IReadOnlyCollection<string> GetBlockedApps() => _blockedApps;

    private void OnForegroundChanged(object? sender, ForegroundChangedEventArgs e)
    {
        // Only check when in focus mode
        if (!_focusService.IsInFocusMode)
            return;

        // Get the effective name (for browser sites, uses friendly name like "YouTube")
        var appName = e.EffectiveName ?? e.AppName;
        
        if (string.IsNullOrEmpty(appName))
            return;

        // Check if blocked (and not whitelisted for this session)
        if (IsBlocked(appName) && !_sessionWhitelist.Contains(appName))
        {
            _blockedAttemptCount++;
            Logger.Info($"Blocked app detected during focus: {appName} (attempt #{_blockedAttemptCount})");
            
            BlockedAppDetected?.Invoke(this, new BlockedAppEventArgs(appName));
        }
    }

    private void OnFocusModeChanged(object? sender, EventArgs e)
    {
        // Reset session whitelist when focus mode ends
        if (!_focusService.IsInFocusMode)
        {
            _sessionWhitelist.Clear();
            _blockedAttemptCount = 0;
            Logger.Debug("Focus ended - cleared session whitelist");
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _foregroundTracker.ForegroundChanged -= OnForegroundChanged;
        _focusService.FocusModeChanged -= OnFocusModeChanged;
        _disposed = true;
    }
}
