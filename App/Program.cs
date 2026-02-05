using System.Threading;
using ScreenTimeTracker.Utilities;

namespace ScreenTimeTracker.App;

/// <summary>
/// Application entry point.
/// </summary>
internal static class Program
{
    private static Mutex? _singleInstanceMutex;
    private static AppBootstrap? _app;

    /// <summary>
    /// The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main(string[] args)
    {
        // Ensure only one instance runs
        const string mutexName = "ScreenTimeTracker_SingleInstance";
        _singleInstanceMutex = new Mutex(true, mutexName, out bool createdNew);

        if (!createdNew)
        {
            // Another instance is already running
            MessageBox.Show(
                "Screen Time Tracker is already running.",
                "Already Running",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        try
        {
            // Required for modern Windows Forms
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Handle unhandled exceptions
            Application.ThreadException += OnThreadException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            // Handle system events
            Application.ApplicationExit += OnApplicationExit;
            SystemEvents.SessionEnding += OnSessionEnding;
            
            // Handle process exit (catches some force kills)
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

            // Create and start application
            _app = new AppBootstrap();
            _app.Start();

            // Run message loop (required for Windows Forms and event hooks)
            Application.Run();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Fatal error in main");
            MessageBox.Show(
                $"A fatal error occurred:\n\n{ex.Message}",
                "Fatal Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            Cleanup();
        }
    }

    private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
    {
        Logger.Error(e.Exception, "Thread exception");
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            Logger.Error(ex, "Unhandled exception");
        }
    }

    private static void OnApplicationExit(object? sender, EventArgs e)
    {
        Logger.Info("Application exit requested");
        _app?.Stop();
    }

    private static void OnSessionEnding(object sender, Microsoft.Win32.SessionEndingEventArgs e)
    {
        Logger.Info($"Session ending: {e.Reason}");
        _app?.Stop();
    }

    private static void OnProcessExit(object? sender, EventArgs e)
    {
        Logger.Info("Process exit detected - emergency persist");
        _app?.Stop();
    }

    private static void Cleanup()
    {
        try
        {
            _app?.Dispose();
            _app = null;

            _singleInstanceMutex?.ReleaseMutex();
            _singleInstanceMutex?.Dispose();
            _singleInstanceMutex = null;

            Logger.Flush();
        }
        catch
        {
            // Ignore errors during cleanup
        }
    }
}

// Required for SystemEvents
internal static class SystemEvents
{
    public static event Microsoft.Win32.SessionEndingEventHandler? SessionEnding
    {
        add => Microsoft.Win32.SystemEvents.SessionEnding += value;
        remove => Microsoft.Win32.SystemEvents.SessionEnding -= value;
    }
}

