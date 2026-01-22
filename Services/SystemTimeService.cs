using System.Runtime.InteropServices;
using ScreenTimeTracker.Utilities;

namespace ScreenTimeTracker.Services;

/// <summary>
/// Provides system uptime and session duration information.
/// </summary>
public class SystemTimeService
{
    private readonly DateTime _appStartTime;
    private readonly ITimeProvider _timeProvider;

    public SystemTimeService(ITimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? new SystemTimeProvider();
        _appStartTime = _timeProvider.Now;
    }

    /// <summary>
    /// Gets the duration the application has been running.
    /// </summary>
    public TimeSpan GetAppUptime()
    {
        return _timeProvider.Now - _appStartTime;
    }

    /// <summary>
    /// Gets the total system uptime (time since last boot).
    /// </summary>
    public TimeSpan GetSystemUptime()
    {
        try
        {
            // GetTickCount64 returns milliseconds since system boot
            return TimeSpan.FromMilliseconds(GetTickCount64());
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to get system uptime");
            return TimeSpan.Zero;
        }
    }

    [DllImport("kernel32.dll")]
    private static extern ulong GetTickCount64();
}
