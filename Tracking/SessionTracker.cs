using ScreenTimeTracker.Core;
using ScreenTimeTracker.Utilities;

namespace ScreenTimeTracker.Tracking;

/// <summary>
/// Event args for when a session is completed.
/// </summary>
public class SessionCompletedEventArgs : EventArgs
{
    public UsageSession Session { get; }
    
    public SessionCompletedEventArgs(UsageSession session)
    {
        Session = session;
    }
}

/// <summary>
/// Manages usage sessions - starts new sessions when foreground changes,
/// ends sessions when appropriate, and emits completed sessions.
/// </summary>
public class SessionTracker
{
    private readonly TrackingState _state;
    private readonly object _stateLock = new();

    /// <summary>
    /// Raised when a session is completed.
    /// </summary>
    public event EventHandler<SessionCompletedEventArgs>? SessionCompleted;

    /// <summary>
    /// Minimum session duration to record (filters out accidental clicks).
    /// </summary>
    public TimeSpan MinimumSessionDuration { get; set; } = TimeSpan.FromSeconds(1);

    public SessionTracker()
    {
        _state = new TrackingState();
    }

    /// <summary>
    /// Handles a foreground app change event.
    /// </summary>
    public void OnForegroundChanged(object? sender, ForegroundChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.AppName))
            return;

        lock (_stateLock)
        {
            if (!_state.ShouldTrack)
            {
                Logger.Debug($"Ignoring foreground change while tracking paused: {e.AppName}");
                return;
            }

            // Use EffectiveName which returns WebsiteDomain for browsers, AppName otherwise
            var trackingName = e.EffectiveName ?? e.AppName;
            
            var completedSession = _state.StartNewSession(
                trackingName, 
                e.ProcessId, 
                e.WindowTitle,
                e.WebsiteDomain,
                e.Timestamp);

            if (completedSession != null)
            {
                EmitSession(completedSession);
            }

            Logger.Debug($"Started tracking: {trackingName} (PID: {e.ProcessId}, Domain: {e.WebsiteDomain ?? "N/A"})");
        }
    }

    /// <summary>
    /// Pauses tracking (e.g., when screen is locked).
    /// </summary>
    public void Pause(bool isLocked = false, bool isIdle = false)
    {
        lock (_stateLock)
        {
            var completedSession = _state.PauseTracking(DateTime.Now, isLocked, isIdle);
            
            if (completedSession != null)
            {
                EmitSession(completedSession);
            }

            Logger.Debug($"Tracking paused (locked: {isLocked}, idle: {isIdle})");
        }
    }

    /// <summary>
    /// Resumes tracking after being paused.
    /// </summary>
    public void Resume()
    {
        lock (_stateLock)
        {
            _state.ResumeTracking();
            Logger.Debug("Tracking resumed");
        }
    }

    /// <summary>
    /// Gets the current tracking state.
    /// </summary>
    public (string? AppName, TimeSpan Duration) GetCurrentSession()
    {
        lock (_stateLock)
        {
            if (!_state.IsTracking || string.IsNullOrEmpty(_state.CurrentAppName))
            {
                return (null, TimeSpan.Zero);
            }

            var duration = DateTime.Now - _state.SessionStartTime;
            return (_state.CurrentAppName, duration);
        }
    }

    /// <summary>
    /// Ends the current session (e.g., on shutdown).
    /// </summary>
    public void EndCurrentSession()
    {
        lock (_stateLock)
        {
            var completedSession = _state.CompleteCurrentSession(DateTime.Now);
            
            if (completedSession != null)
            {
                EmitSession(completedSession);
            }
        }
    }

    private void EmitSession(UsageSession session)
    {
        // Filter out very short sessions
        if (session.Duration < MinimumSessionDuration)
        {
            Logger.Debug($"Filtered short session: {session.AppName} ({session.Duration.TotalMilliseconds}ms)");
            return;
        }

        Logger.Debug($"Session completed: {session}");

        try
        {
            SessionCompleted?.Invoke(this, new SessionCompletedEventArgs(session));
        }
        catch (Exception ex)
        {
            Logger.Error($"Error in SessionCompleted handler: {ex.Message}");
        }
    }
}
