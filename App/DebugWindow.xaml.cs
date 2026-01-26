using System.Windows;
using ScreenTimeTracker.Tracking;

namespace ScreenTimeTracker.App;

public partial class DebugWindow : Window
{
    private readonly ForegroundAppTracker _tracker;

    public DebugWindow(ForegroundAppTracker tracker)
    {
        InitializeComponent();
        _tracker = tracker;
        
        // Subscribe to events
        _tracker.ForegroundChanged += OnForegroundChanged;
        
        // Log initial state
        Log("Debug window started. Monitoring events...");
        CheckCurrent_Click(null, null);
    }

    private void OnForegroundChanged(object? sender, ForegroundChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            Log($"EVENT: App='{e.AppName}' ID={e.ProcessId} Title='{e.WindowTitle}' Domain='{e.WebsiteDomain}' Effective='{e.EffectiveName}'");
        });
    }

    private void CheckCurrent_Click(object? sender, RoutedEventArgs? e)
    {
        _tracker.CheckCurrentForeground();
        Log("Requested manual check...");
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        TxtLog.Clear();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Log(string message)
    {
        TxtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
        TxtLog.ScrollToEnd();
    }

    protected override void OnClosed(EventArgs e)
    {
        // Unsubscribe to avoid memory leaks
        _tracker.ForegroundChanged -= OnForegroundChanged;
        base.OnClosed(e);
    }
}
