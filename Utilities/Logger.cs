using Serilog;
using Serilog.Events;
using System.IO;

namespace ScreenTimeTracker.Utilities;

/// <summary>
/// Lightweight logger wrapper using Serilog.
/// Configured for daily rolling files with minimal overhead.
/// </summary>
public static class Logger
{
    private static bool _initialized;
    private static readonly object _initLock = new();

    /// <summary>
    /// Initializes the logger with file output.
    /// </summary>
    public static void Initialize(string? logDirectory = null)
    {
        lock (_initLock)
        {
            if (_initialized)
                return;

            var logPath = logDirectory ?? GetDefaultLogDirectory();
            
            if (!Directory.Exists(logPath))
            {
                Directory.CreateDirectory(logPath);
            }

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(
                    Path.Combine(logPath, "log-.txt"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                    restrictedToMinimumLevel: LogEventLevel.Debug)
                .CreateLogger();

            _initialized = true;
            Info("Logger initialized");
        }
    }

    /// <summary>
    /// Ensures logger is initialized with defaults if not already.
    /// </summary>
    private static void EnsureInitialized()
    {
        if (!_initialized)
        {
            Initialize();
        }
    }

    /// <summary>
    /// Logs a debug message.
    /// </summary>
    public static void Debug(string message)
    {
        EnsureInitialized();
        Log.Debug(message);
    }

    /// <summary>
    /// Logs an info message.
    /// </summary>
    public static void Info(string message)
    {
        EnsureInitialized();
        Log.Information(message);
    }

    /// <summary>
    /// Logs a warning message.
    /// </summary>
    public static void Warning(string message)
    {
        EnsureInitialized();
        Log.Warning(message);
    }

    /// <summary>
    /// Logs an error message.
    /// </summary>
    public static void Error(string message)
    {
        EnsureInitialized();
        Log.Error(message);
    }

    /// <summary>
    /// Logs an error with exception.
    /// </summary>
    public static void Error(Exception ex, string message)
    {
        EnsureInitialized();
        Log.Error(ex, message);
    }

    /// <summary>
    /// Flushes any buffered log entries.
    /// </summary>
    public static void Flush()
    {
        Log.CloseAndFlush();
    }

    private static string GetDefaultLogDirectory()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "ScreenTimeTracker", "Logs");
    }
}
