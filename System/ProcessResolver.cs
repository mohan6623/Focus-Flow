using System.Diagnostics;
using ScreenTimeTracker.Utilities;

namespace ScreenTimeTracker.SystemUtils;

/// <summary>
/// Resolves process IDs to executable names with caching for performance.
/// </summary>
public class ProcessResolver
{
    private readonly Dictionary<int, string> _processNameCache = new();
    private readonly object _cacheLock = new();
    private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(5);
    private DateTime _lastCacheClean = DateTime.Now;

    /// <summary>
    /// Gets the process name (executable name without extension) for a given process ID.
    /// </summary>
    public string? GetProcessName(int processId)
    {
        if (processId <= 0)
            return null;

        CleanCacheIfNeeded();

        lock (_cacheLock)
        {
            if (_processNameCache.TryGetValue(processId, out var cachedName))
            {
                return cachedName;
            }
        }

        try
        {
            using var process = Process.GetProcessById(processId);
            var name = process.ProcessName;

            lock (_cacheLock)
            {
                _processNameCache[processId] = name;
            }

            return name;
        }
        catch (ArgumentException)
        {
            // Process no longer exists
            Logger.Debug($"Process {processId} no longer exists");
            return null;
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to get process name for PID {processId}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Gets the executable path for a given process ID.
    /// </summary>
    public string? GetExecutablePath(int processId)
    {
        if (processId <= 0)
            return null;

        try
        {
            using var process = Process.GetProcessById(processId);
            return process.MainModule?.FileName;
        }
        catch (Exception ex)
        {
            Logger.Debug($"Failed to get executable path for PID {processId}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Clears the process name cache.
    /// </summary>
    public void ClearCache()
    {
        lock (_cacheLock)
        {
            _processNameCache.Clear();
        }
    }

    private void CleanCacheIfNeeded()
    {
        if (DateTime.Now - _lastCacheClean < _cacheExpiry)
            return;

        lock (_cacheLock)
        {
            // Remove entries for processes that no longer exist
            var keysToRemove = new List<int>();
            
            foreach (var pid in _processNameCache.Keys)
            {
                try
                {
                    Process.GetProcessById(pid);
                }
                catch
                {
                    keysToRemove.Add(pid);
                }
            }

            foreach (var key in keysToRemove)
            {
                _processNameCache.Remove(key);
            }

            _lastCacheClean = DateTime.Now;
        }
    }
}
