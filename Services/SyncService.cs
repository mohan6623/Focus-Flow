using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using ScreenTimeTracker.Core;
using ScreenTimeTracker.Utilities;

namespace ScreenTimeTracker.Services;

/// <summary>
/// Handles synchronization with backend API (stubbed for future implementation).
/// Implements offline-first with retry logic.
/// </summary>
public class SyncService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly Queue<SyncPayload> _offlineQueue;
    private readonly object _queueLock = new();
    private readonly global::System.Threading.Timer _syncTimer;
    
    private bool _disposed;
    private string? _authToken;

    /// <summary>
    /// Backend API base URL.
    /// </summary>
    public string? ApiBaseUrl { get; set; }

    /// <summary>
    /// How often to attempt sync.
    /// </summary>
    public TimeSpan SyncInterval { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Maximum retry attempts before giving up on a payload.
    /// </summary>
    public int MaxRetries { get; set; } = 5;

    /// <summary>
    /// Whether sync is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = false;

    public SyncService()
    {
        _httpClient = new HttpClient();
        _offlineQueue = new Queue<SyncPayload>();
        _syncTimer = new global::System.Threading.Timer(SyncCallback, null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>
    /// Sets the authentication token for API requests.
    /// </summary>
    public void SetAuthToken(string token)
    {
        _authToken = token;
        _httpClient.DefaultRequestHeaders.Authorization = 
            new global::System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        Logger.Info("Auth token set for sync service");
    }

    /// <summary>
    /// Starts the sync service.
    /// </summary>
    public void Start()
    {
        if (!IsEnabled || string.IsNullOrEmpty(ApiBaseUrl))
        {
            Logger.Info("Sync service not enabled or no API URL configured");
            return;
        }

        _syncTimer.Change(SyncInterval, SyncInterval);
        Logger.Info($"Sync service started (interval: {SyncInterval.TotalMinutes}m)");
    }

    /// <summary>
    /// Stops the sync service.
    /// </summary>
    public void Stop()
    {
        _syncTimer.Change(Timeout.Infinite, Timeout.Infinite);
        Logger.Info("Sync service stopped");
    }

    /// <summary>
    /// Queues usage data for sync.
    /// </summary>
    public void QueueForSync(List<AppUsageStats> stats, DateOnly date)
    {
        if (!IsEnabled)
            return;

        lock (_queueLock)
        {
            var payload = new SyncPayload
            {
                Date = date.ToString("yyyy-MM-dd"),
                Stats = stats.Select(s => new SyncAppStats
                {
                    AppName = s.AppName,
                    TotalSeconds = (long)s.TotalTime.TotalSeconds,
                    SessionCount = s.SessionCount,
                    FirstUsed = s.FirstUsed.ToString("o"),
                    LastUsed = s.LastUsed.ToString("o")
                }).ToList(),
                QueuedAt = DateTime.Now,
                RetryCount = 0
            };

            _offlineQueue.Enqueue(payload);
            Logger.Debug($"Queued sync payload for {date} with {stats.Count} apps");
        }
    }

    private async void SyncCallback(object? state)
    {
        if (!IsEnabled || string.IsNullOrEmpty(ApiBaseUrl))
            return;

        await ProcessQueueAsync();
    }

    private async Task ProcessQueueAsync()
    {
        while (true)
        {
            SyncPayload? payload;

            lock (_queueLock)
            {
                if (_offlineQueue.Count == 0)
                    break;

                payload = _offlineQueue.Peek();
            }

            var success = await TrySyncAsync(payload);

            lock (_queueLock)
            {
                if (success)
                {
                    _offlineQueue.Dequeue();
                    Logger.Debug($"Synced payload for {payload.Date}");
                }
                else
                {
                    payload.RetryCount++;
                    
                    if (payload.RetryCount >= MaxRetries)
                    {
                        _offlineQueue.Dequeue();
                        Logger.Warning($"Dropped payload for {payload.Date} after {MaxRetries} retries");
                    }
                    else
                    {
                        // Move to end of queue for retry
                        _offlineQueue.Dequeue();
                        _offlineQueue.Enqueue(payload);
                        break; // Wait for next sync interval
                    }
                }
            }
        }
    }

    private async Task<bool> TrySyncAsync(SyncPayload payload)
    {
        try
        {
            // Calculate exponential backoff delay
            if (payload.RetryCount > 0)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, payload.RetryCount));
                await Task.Delay(delay);
            }

            var url = $"{ApiBaseUrl}/api/usage/sync";
            var response = await _httpClient.PostAsJsonAsync(url, payload);

            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            Logger.Warning($"Sync failed with status {response.StatusCode}");
            return false;
        }
        catch (Exception ex)
        {
            Logger.Warning($"Sync error: {ex.Message}");
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Stop();
        _syncTimer.Dispose();
        _httpClient.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~SyncService()
    {
        Dispose();
    }
}

/// <summary>
/// Payload for syncing usage data.
/// </summary>
internal class SyncPayload
{
    public string Date { get; set; } = string.Empty;
    public List<SyncAppStats> Stats { get; set; } = new();
    public DateTime QueuedAt { get; set; }
    public int RetryCount { get; set; }
}

/// <summary>
/// App stats within a sync payload.
/// </summary>
internal class SyncAppStats
{
    public string AppName { get; set; } = string.Empty;
    public long TotalSeconds { get; set; }
    public int SessionCount { get; set; }
    public string FirstUsed { get; set; } = string.Empty;
    public string LastUsed { get; set; } = string.Empty;
}
