using System.Runtime.InteropServices;
using ScreenTimeTracker.Utilities;

namespace ScreenTimeTracker.Tracking;

/// <summary>
/// Event arguments for foreground app change events.
/// </summary>
public class ForegroundChangedEventArgs : EventArgs
{
    public string? AppName { get; }
    public string? AppPath { get; }
    public int ProcessId { get; }
    public string? WindowTitle { get; }
    public string? WebsiteDomain { get; }
    public DateTime Timestamp { get; }

    public ForegroundChangedEventArgs(string? appName, string? appPath, int processId, string? windowTitle, string? websiteDomain, DateTime timestamp)
    {
        AppName = appName;
        AppPath = appPath;
        ProcessId = processId;
        WindowTitle = windowTitle;
        WebsiteDomain = websiteDomain;
        Timestamp = timestamp;
    }

    /// <summary>
    /// Gets the effective tracked name (friendly website name if available, otherwise app name).
    /// For browsers, this returns a friendly name like "YouTube" instead of "youtube.com".
    /// </summary>
    public string? EffectiveName => !string.IsNullOrEmpty(WebsiteDomain) 
        ? ScreenTimeTracker.SystemUtils.BrowserTabResolver.GetFriendlyName(WebsiteDomain) 
        : AppName;
}

/// <summary>
/// Tracks foreground application changes using Windows event hooks.
/// Event-driven approach - no polling required.
/// </summary>
public class ForegroundAppTracker : IDisposable
{
    #region Native Methods

    private delegate void WinEventDelegate(
        IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject,
        int idChild, uint dwEventThread, uint dwmsEventTime);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(
        uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
        WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    private const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

    #endregion

    private readonly ScreenTimeTracker.SystemUtils.WindowInfoProvider _windowInfoProvider;
    private readonly ScreenTimeTracker.SystemUtils.ProcessResolver _processResolver;
    private readonly ScreenTimeTracker.SystemUtils.BrowserTabResolver _browserTabResolver;
    
    private IntPtr _eventHook = IntPtr.Zero;
    private WinEventDelegate? _winEventDelegate;
    private bool _isRunning;
    private bool _disposed;

    /// <summary>
    /// Raised when the foreground application changes.
    /// </summary>
    public event EventHandler<ForegroundChangedEventArgs>? ForegroundChanged;

    public ForegroundAppTracker(
        ScreenTimeTracker.SystemUtils.WindowInfoProvider windowInfoProvider,
        ScreenTimeTracker.SystemUtils.ProcessResolver processResolver,
        ScreenTimeTracker.SystemUtils.BrowserTabResolver browserTabResolver)
    {
        _windowInfoProvider = windowInfoProvider;
        _processResolver = processResolver;
        _browserTabResolver = browserTabResolver;
    }

    /// <summary>
    /// Starts listening for foreground window changes.
    /// </summary>
    public void Start()
    {
        if (_isRunning)
            return;

        // Keep delegate alive to prevent garbage collection
        _winEventDelegate = WinEventProc;

        _eventHook = SetWinEventHook(
            EVENT_SYSTEM_FOREGROUND,
            EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero,
            _winEventDelegate,
            0,
            0,
            WINEVENT_OUTOFCONTEXT); // Removed WINEVENT_SKIPOWNPROCESS to track self

        if (_eventHook == IntPtr.Zero)
        {
            Logger.Error("Failed to set foreground event hook");
            throw new InvalidOperationException("Failed to set foreground event hook");
        }

        _isRunning = true;
        Logger.Info("Foreground app tracker started");

        // Get initial foreground window
        CheckCurrentForeground();
    }

    /// <summary>
    /// Stops listening for foreground window changes.
    /// </summary>
    public void Stop()
    {
        if (!_isRunning)
            return;

        if (_eventHook != IntPtr.Zero)
        {
            UnhookWinEvent(_eventHook);
            _eventHook = IntPtr.Zero;
        }

        _winEventDelegate = null;
        _isRunning = false;
        Logger.Info("Foreground app tracker stopped");
    }

    /// <summary>
    /// Manually checks the current foreground window.
    /// Useful for initial state or after resuming from idle/lock.
    /// </summary>
    public void CheckCurrentForeground()
    {
        var windowInfo = _windowInfoProvider.GetForegroundWindowInfo();
        
        if (windowInfo == null)
            return;

        var appName = _processResolver.GetProcessName(windowInfo.ProcessId);
        
        if (string.IsNullOrEmpty(appName))
            return;

        RaiseForegroundChanged(appName, windowInfo.ProcessId, windowInfo.Title, windowInfo.Handle);
    }

    private void WinEventProc(
        IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject,
        int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (eventType != EVENT_SYSTEM_FOREGROUND)
            return;

        try
        {
            var windowInfo = _windowInfoProvider.GetWindowInfo(hwnd);
            
            if (windowInfo == null)
                return;

            var appName = _processResolver.GetProcessName(windowInfo.ProcessId);
            
            if (string.IsNullOrEmpty(appName))
                return;

            RaiseForegroundChanged(appName, windowInfo.ProcessId, windowInfo.Title, hwnd);
        }
        catch (Exception ex)
        {
            Logger.Error($"Error in WinEventProc: {ex.Message}");
        }
    }

    private void RaiseForegroundChanged(string appName, int processId, string? windowTitle, IntPtr windowHandle)
    {
        // Special case: if this is our own process OR the app name is our executable
        // Ensure we identify as "Focus Flow"
        if (processId == Environment.ProcessId || 
            appName.Equals("ScreenTimeTracker", StringComparison.OrdinalIgnoreCase) ||
            appName.Equals("ScreenTimeTracker.exe", StringComparison.OrdinalIgnoreCase))
        {
            appName = "Focus Flow";
        }

        // Get the executable path
        var appPath = _processResolver.GetExecutablePath(processId);

        // Check if this is a browser and extract URL
        string? websiteDomain = null;
        if (_browserTabResolver.IsBrowser(appName))
        {
            var url = _browserTabResolver.GetBrowserUrl(windowHandle, appName);
            var domain = ScreenTimeTracker.SystemUtils.BrowserTabResolver.ExtractDomain(url);
            
            if (!string.IsNullOrEmpty(domain))
            {
                websiteDomain = domain;
                Logger.Debug($"Browser detected: {appName}, Domain: {domain}, URL: {url}");
            }
            else if (!string.IsNullOrEmpty(url))
            {
                // URL extraction worked but domain parsing failed - use the raw URL
                websiteDomain = url;
                Logger.Debug($"Browser detected: {appName}, Raw URL: {url}");
            }
        }

        var args = new ForegroundChangedEventArgs(appName, appPath, processId, windowTitle, websiteDomain, DateTime.Now);
        
        try
        {
            ForegroundChanged?.Invoke(this, args);
        }
        catch (Exception ex)
        {
            Logger.Error($"Error in ForegroundChanged handler: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Stop();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~ForegroundAppTracker()
    {
        Dispose();
    }
}
