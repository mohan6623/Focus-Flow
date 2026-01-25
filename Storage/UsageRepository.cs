using System.IO;
using Microsoft.Data.Sqlite;
using ScreenTimeTracker.Core;
using ScreenTimeTracker.Storage.Models;
using ScreenTimeTracker.Utilities;

namespace ScreenTimeTracker.Storage;

/// <summary>
/// SQLite-based repository for persisting usage data.
/// Implements buffered writes to minimize disk I/O.
/// </summary>
public class UsageRepository : IDisposable
{
    private readonly string _connectionString;
    private readonly string _databasePath;
    private bool _initialized;
    private bool _disposed;

    public UsageRepository(string? databasePath = null)
    {
        _databasePath = databasePath ?? GetDefaultDatabasePath();
        _connectionString = $"Data Source={_databasePath}";
    }

    /// <summary>
    /// Initializes the database (creates tables if needed).
    /// </summary>
    public void Initialize()
    {
        if (_initialized)
            return;

        try
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(_databasePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS app_usage (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    app_name TEXT NOT NULL,
                    date TEXT NOT NULL,
                    total_seconds INTEGER NOT NULL DEFAULT 0,
                    session_count INTEGER NOT NULL DEFAULT 0,
                    first_used TEXT NOT NULL,
                    last_used TEXT NOT NULL,
                    updated_at TEXT NOT NULL,
                    UNIQUE(app_name, date)
                );

                CREATE INDEX IF NOT EXISTS idx_app_usage_date ON app_usage(date);
                CREATE INDEX IF NOT EXISTS idx_app_usage_app_name ON app_usage(app_name);

                CREATE TABLE IF NOT EXISTS app_categories (
                    app_name TEXT PRIMARY KEY,
                    category TEXT NOT NULL,
                    is_user_override INTEGER DEFAULT 0
                );

                CREATE TABLE IF NOT EXISTS app_sessions (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    app_name TEXT NOT NULL,
                    start_time TEXT NOT NULL,
                    end_time TEXT NOT NULL,
                    duration_seconds INTEGER NOT NULL,
                    date TEXT NOT NULL,
                    website_domain TEXT,
                    created_at TEXT NOT NULL
                );

                CREATE INDEX IF NOT EXISTS idx_app_sessions_date ON app_sessions(date);
                CREATE INDEX IF NOT EXISTS idx_app_sessions_app_name ON app_sessions(app_name);
            ";
            command.ExecuteNonQuery();

            _initialized = true;
            Logger.Info($"Database initialized at: {_databasePath}");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to initialize database: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Saves or updates usage stats for an app on a specific date.
    /// Uses UPSERT pattern for efficiency.
    /// </summary>
    public void SaveUsage(AppUsageStats stats)
    {
        EnsureInitialized();

        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO app_usage (app_name, date, total_seconds, session_count, first_used, last_used, updated_at)
                VALUES (@appName, @date, @totalSeconds, @sessionCount, @firstUsed, @lastUsed, @updatedAt)
                ON CONFLICT(app_name, date) DO UPDATE SET
                    total_seconds = @totalSeconds,
                    session_count = @sessionCount,
                    first_used = MIN(first_used, @firstUsed),
                    last_used = MAX(last_used, @lastUsed),
                    updated_at = @updatedAt
            ";

            command.Parameters.AddWithValue("@appName", stats.AppName);
            command.Parameters.AddWithValue("@date", stats.Date.ToString("yyyy-MM-dd"));
            command.Parameters.AddWithValue("@totalSeconds", (long)stats.TotalTime.TotalSeconds);
            command.Parameters.AddWithValue("@sessionCount", stats.SessionCount);
            command.Parameters.AddWithValue("@firstUsed", stats.FirstUsed.ToString("o"));
            command.Parameters.AddWithValue("@lastUsed", stats.LastUsed.ToString("o"));
            command.Parameters.AddWithValue("@updatedAt", DateTime.Now.ToString("o"));

            command.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to save usage for {stats.AppName}: {ex.Message}");
        }
    }

    /// <summary>
    /// Saves multiple usage stats in a single transaction.
    /// </summary>
    public void SaveUsageBatch(IEnumerable<AppUsageStats> statsList)
    {
        EnsureInitialized();

        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var transaction = connection.BeginTransaction();

            foreach (var stats in statsList)
            {
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    INSERT INTO app_usage (app_name, date, total_seconds, session_count, first_used, last_used, updated_at)
                    VALUES (@appName, @date, @totalSeconds, @sessionCount, @firstUsed, @lastUsed, @updatedAt)
                    ON CONFLICT(app_name, date) DO UPDATE SET
                        total_seconds = @totalSeconds,
                        session_count = @sessionCount,
                        first_used = MIN(first_used, @firstUsed),
                        last_used = MAX(last_used, @lastUsed),
                        updated_at = @updatedAt
                ";

                command.Parameters.AddWithValue("@appName", stats.AppName);
                command.Parameters.AddWithValue("@date", stats.Date.ToString("yyyy-MM-dd"));
                command.Parameters.AddWithValue("@totalSeconds", (long)stats.TotalTime.TotalSeconds);
                command.Parameters.AddWithValue("@sessionCount", stats.SessionCount);
                command.Parameters.AddWithValue("@firstUsed", stats.FirstUsed.ToString("o"));
                command.Parameters.AddWithValue("@lastUsed", stats.LastUsed.ToString("o"));
                command.Parameters.AddWithValue("@updatedAt", DateTime.Now.ToString("o"));

                command.ExecuteNonQuery();
            }

            transaction.Commit();
            Logger.Debug($"Saved batch of {statsList.Count()} usage records");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to save usage batch: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets all usage stats for a specific date.
    /// </summary>
    public List<AppUsageStats> GetDailyStats(DateOnly date)
    {
        EnsureInitialized();
        var results = new List<AppUsageStats>();

        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT app_name, date, total_seconds, session_count, first_used, last_used
                FROM app_usage
                WHERE date = @date
                ORDER BY total_seconds DESC
            ";
            command.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var appName = reader.GetString(0);
                var totalSeconds = reader.GetInt64(2);
                var sessionCount = reader.GetInt32(3);
                var firstUsed = DateTime.Parse(reader.GetString(4));
                var lastUsed = DateTime.Parse(reader.GetString(5));

                // Create stats and restore values directly
                var stats = new AppUsageStats(appName, date);
                stats.SetFromDatabase(
                    TimeSpan.FromSeconds(totalSeconds),
                    sessionCount,
                    firstUsed,
                    lastUsed
                );

                results.Add(stats);
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to get daily stats: {ex.Message}");
        }

        return results;
    }

    /// <summary>
    /// Gets total screen time for a specific date.
    /// </summary>
    public TimeSpan GetTotalScreenTime(DateOnly date)
    {
        EnsureInitialized();

        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT COALESCE(SUM(total_seconds), 0)
                FROM app_usage
                WHERE date = @date
            ";
            command.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));

            var totalSeconds = (long)command.ExecuteScalar()!;
            return TimeSpan.FromSeconds(totalSeconds);
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to get total screen time: {ex.Message}");
            return TimeSpan.Zero;
        }
    }

    /// <summary>
    /// Gets top N apps by usage time for a date.
    /// </summary>
    public List<(string AppName, TimeSpan Time, int Sessions)> GetTopApps(DateOnly date, int count = 10)
    {
        EnsureInitialized();
        var results = new List<(string, TimeSpan, int)>();

        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT app_name, total_seconds, session_count
                FROM app_usage
                WHERE date = @date
                ORDER BY total_seconds DESC
                LIMIT @count
            ";
            command.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
            command.Parameters.AddWithValue("@count", count);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                results.Add((
                    reader.GetString(0),
                    TimeSpan.FromSeconds(reader.GetInt64(1)),
                    reader.GetInt32(2)
                ));
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to get top apps: {ex.Message}");
        }

        return results;
    }

    /// <summary>
    /// Gets total screen time for a specific date range.
    /// Returns a dictionary mapping Date -> TotalTime.
    /// </summary>
    public Dictionary<DateOnly, TimeSpan> GetUsageHistory(DateOnly startDate, DateOnly endDate)
    {
        EnsureInitialized();
        var results = new Dictionary<DateOnly, TimeSpan>();

        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT date, SUM(total_seconds)
                FROM app_usage
                WHERE date BETWEEN @startDate AND @endDate
                GROUP BY date
                ORDER BY date ASC
            ";
            command.Parameters.AddWithValue("@startDate", startDate.ToString("yyyy-MM-dd"));
            command.Parameters.AddWithValue("@endDate", endDate.ToString("yyyy-MM-dd"));

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (DateOnly.TryParse(reader.GetString(0), out var date))
                {
                    results[date] = TimeSpan.FromSeconds(reader.GetInt64(1));
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to get usage history: {ex.Message}");
        }

        return results;
    }

    /// <summary>
    /// Gets the user-defined category for an app.
    /// </summary>
    public string? GetAppCategory(string appName)
    {
        EnsureInitialized();

        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT category FROM app_categories WHERE app_name = @appName";
            command.Parameters.AddWithValue("@appName", appName);

            var result = command.ExecuteScalar();
            return result?.ToString();
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to get category for {appName}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Sets a category for an app (user override).
    /// </summary>
    public void SetAppCategory(string appName, string category, bool isUserOverride = false)
    {
        EnsureInitialized();

        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO app_categories (app_name, category, is_user_override)
                VALUES (@appName, @category, @isUserOverride)
                ON CONFLICT(app_name) DO UPDATE SET
                    category = @category,
                    is_user_override = @isUserOverride
            ";
            command.Parameters.AddWithValue("@appName", appName);
            command.Parameters.AddWithValue("@category", category);
            command.Parameters.AddWithValue("@isUserOverride", isUserOverride ? 1 : 0);

            command.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to set category for {appName}: {ex.Message}");
        }
    }

    /// <summary>
    /// Saves a batch of sessions to the database.
    /// </summary>
    public void SaveSessionsBatch(IEnumerable<UsageSession> sessions)
    {
        EnsureInitialized();
        var sessionList = sessions.ToList();
        if (sessionList.Count == 0) return;

        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            foreach (var session in sessionList)
            {
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    INSERT INTO app_sessions (app_name, start_time, end_time, duration_seconds, date, website_domain, created_at)
                    VALUES (@appName, @startTime, @endTime, @duration, @date, @domain, @createdAt)
                ";
                command.Parameters.AddWithValue("@appName", session.AppName);
                command.Parameters.AddWithValue("@startTime", session.StartTime.ToString("o"));
                command.Parameters.AddWithValue("@endTime", session.EndTime.ToString("o"));
                command.Parameters.AddWithValue("@duration", (long)session.Duration.TotalSeconds);
                command.Parameters.AddWithValue("@date", session.Date.ToString("yyyy-MM-dd"));
                command.Parameters.AddWithValue("@domain", session.WebsiteDomain ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@createdAt", DateTime.Now.ToString("o"));
                command.ExecuteNonQuery();
            }

            transaction.Commit();
            Logger.Debug($"Saved batch of {sessionList.Count} sessions");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to save sessions batch: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets all sessions for a specific date (for hourly breakdown).
    /// </summary>
    public List<UsageSession> GetSessionsForDate(DateOnly date)
    {
        EnsureInitialized();
        var results = new List<UsageSession>();

        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT app_name, start_time, end_time, website_domain
                FROM app_sessions
                WHERE date = @date
                ORDER BY start_time ASC
            ";
            command.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var appName = reader.GetString(0);
                var startTime = DateTime.Parse(reader.GetString(1));
                var endTime = DateTime.Parse(reader.GetString(2));
                var domain = reader.IsDBNull(3) ? null : reader.GetString(3);

                var session = new UsageSession(appName, 0, null, startTime, endTime, domain);
                results.Add(session);
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to get sessions for date: {ex.Message}");
        }

        return results;
    }

    private void EnsureInitialized()
    {
        if (!_initialized)
        {
            Initialize();
        }
    }

    private static string GetDefaultDatabasePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "ScreenTimeTracker", "usage.db");
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~UsageRepository()
    {
        Dispose();
    }
}
