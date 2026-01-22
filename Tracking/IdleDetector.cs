using System.Runtime.InteropServices;
using ScreenTimeTracker.Utilities;

namespace ScreenTimeTracker.Tracking;

/// <summary>
/// Event args for idle state changes.
/// </summary>
public class IdleStateChangedEventArgs : EventArgs
{
    public bool IsIdle { get; }
    public TimeSpan IdleDuration { get; }
    public DateTime Timestamp { get; }

    public IdleStateChangedEventArgs(bool isIdle, TimeSpan idleDuration, DateTime timestamp)
    {
        IsIdle = isIdle;
        IdleDuration = idleDuration;
        Timestamp = timestamp;
    }
}

/// <summary>
/// Detects user idle state based on keyboard/mouse input.
/// Uses low-frequency polling (configurable, default 30 seconds).
/// </summary>
public class IdleDetector : IDisposable
{
    #region Native Methods

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    [DllImport("kernel32.dll")]
    private static extern uint GetTickCount();

    #endregion

    private readonly global::System.Threading.Timer _checkTimer;
    private bool _isIdle;
    private bool _disposed;

    /// <summary>
    /// Threshold after which user is considered idle.
    /// </summary>
    public TimeSpan IdleThreshold { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How often to check for idle state.
    /// </summary>
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets whether the user is currently idle.
    /// </summary>
    public bool IsIdle => _isIdle;

    /// <summary>
    /// Raised when idle state changes.
    /// </summary>
    public event EventHandler<IdleStateChangedEventArgs>? IdleStateChanged;

    public IdleDetector()
    {
        _checkTimer = new global::System.Threading.Timer(CheckIdleState, null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>
    /// Starts the idle detector.
    /// </summary>
    public void Start()
    {
        _checkTimer.Change(CheckInterval, CheckInterval);
        Logger.Info($"Idle detector started (threshold: {IdleThreshold.TotalMinutes}m, interval: {CheckInterval.TotalSeconds}s)");
    }

    /// <summary>
    /// Stops the idle detector.
    /// </summary>
    public void Stop()
    {
        _checkTimer.Change(Timeout.Infinite, Timeout.Infinite);
        Logger.Info("Idle detector stopped");
    }

    /// <summary>
    /// Gets the time since last user input.
    /// </summary>
    public TimeSpan GetIdleTime()
    {
        var lastInput = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        
        if (!GetLastInputInfo(ref lastInput))
        {
            return TimeSpan.Zero;
        }

        var idleMilliseconds = GetTickCount() - lastInput.dwTime;
        return TimeSpan.FromMilliseconds(idleMilliseconds);
    }

    private void CheckIdleState(object? state)
    {
        try
        {
            var idleTime = GetIdleTime();
            var wasIdle = _isIdle;
            var nowIdle = idleTime >= IdleThreshold;

            if (nowIdle != wasIdle)
            {
                _isIdle = nowIdle;
                var timestamp = DateTime.Now;

                if (nowIdle)
                {
                    Logger.Debug($"User became idle (idle for {idleTime.TotalMinutes:F1}m)");
                }
                else
                {
                    Logger.Debug("User became active");
                }

                RaiseIdleStateChanged(nowIdle, idleTime, timestamp);
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Error checking idle state: {ex.Message}");
        }
    }

    private void RaiseIdleStateChanged(bool isIdle, TimeSpan idleDuration, DateTime timestamp)
    {
        try
        {
            IdleStateChanged?.Invoke(this, new IdleStateChangedEventArgs(isIdle, idleDuration, timestamp));
        }
        catch (Exception ex)
        {
            Logger.Error($"Error in IdleStateChanged handler: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Stop();
        _checkTimer.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~IdleDetector()
    {
        Dispose();
    }
}
