using Microsoft.Win32;
using ScreenTimeTracker.Utilities;

namespace ScreenTimeTracker.Tracking;

/// <summary>
/// Event args for lock state changes.
/// </summary>
public class LockStateChangedEventArgs : EventArgs
{
    public bool IsLocked { get; }
    public DateTime Timestamp { get; }

    public LockStateChangedEventArgs(bool isLocked, DateTime timestamp)
    {
        IsLocked = isLocked;
        Timestamp = timestamp;
    }
}

/// <summary>
/// Monitors screen lock/unlock state using Windows session events.
/// </summary>
public class LockStateMonitor : IDisposable
{
    private bool _isLocked;
    private bool _disposed;

    /// <summary>
    /// Raised when the lock state changes.
    /// </summary>
    public event EventHandler<LockStateChangedEventArgs>? LockStateChanged;

    /// <summary>
    /// Gets whether the screen is currently locked.
    /// </summary>
    public bool IsLocked => _isLocked;

    /// <summary>
    /// Starts monitoring lock state.
    /// </summary>
    public void Start()
    {
        SystemEvents.SessionSwitch += OnSessionSwitch;
        Logger.Info("Lock state monitor started");
    }

    /// <summary>
    /// Stops monitoring lock state.
    /// </summary>
    public void Stop()
    {
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        Logger.Info("Lock state monitor stopped");
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        var timestamp = DateTime.Now;
        bool? newLockState = null;

        switch (e.Reason)
        {
            case SessionSwitchReason.SessionLock:
                newLockState = true;
                Logger.Debug("Screen locked");
                break;

            case SessionSwitchReason.SessionUnlock:
                newLockState = false;
                Logger.Debug("Screen unlocked");
                break;

            case SessionSwitchReason.SessionLogoff:
                newLockState = true;
                Logger.Debug("Session logoff");
                break;

            case SessionSwitchReason.SessionLogon:
                newLockState = false;
                Logger.Debug("Session logon");
                break;

            case SessionSwitchReason.ConsoleDisconnect:
                // Remote session connected, treat as lock
                newLockState = true;
                Logger.Debug("Console disconnected");
                break;

            case SessionSwitchReason.ConsoleConnect:
                newLockState = false;
                Logger.Debug("Console connected");
                break;
        }

        if (newLockState.HasValue && newLockState.Value != _isLocked)
        {
            _isLocked = newLockState.Value;
            RaiseLockStateChanged(newLockState.Value, timestamp);
        }
    }

    private void RaiseLockStateChanged(bool isLocked, DateTime timestamp)
    {
        try
        {
            LockStateChanged?.Invoke(this, new LockStateChangedEventArgs(isLocked, timestamp));
        }
        catch (Exception ex)
        {
            Logger.Error($"Error in LockStateChanged handler: {ex.Message}");
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

    ~LockStateMonitor()
    {
        Dispose();
    }
}
