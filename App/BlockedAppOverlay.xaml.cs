using System.Windows;
using System.Windows.Threading;
using ScreenTimeTracker.Services;

namespace ScreenTimeTracker.App;

/// <summary>
/// Full-screen overlay shown when a blocked app is detected during focus mode.
/// </summary>
public partial class BlockedAppOverlay : Window
{
    private readonly AppBlockerService _blockerService;
    private readonly DispatcherTimer _autoCloseTimer;
    private string _blockedAppName = "";
    private int _countdown = 5;

    public BlockedAppOverlay(AppBlockerService blockerService)
    {
        InitializeComponent();
        _blockerService = blockerService;

        // Auto-close timer
        _autoCloseTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _autoCloseTimer.Tick += AutoCloseTimer_Tick;
    }

    /// <summary>
    /// Shows the overlay for a specific blocked app.
    /// </summary>
    public void ShowForApp(string appName)
    {
        _blockedAppName = appName;
        TxtAppName.Text = $"{appName} is blocked during focus";
        
        // Reset countdown
        _countdown = 5;
        TxtCountdown.Text = $"Auto-closing in {_countdown}s...";
        
        // Start auto-close timer
        _autoCloseTimer.Start();
        
        // Show window
        this.Show();
        this.Activate();
    }

    private void AutoCloseTimer_Tick(object? sender, EventArgs e)
    {
        _countdown--;
        
        if (_countdown <= 0)
        {
            CloseOverlay();
        }
        else
        {
            TxtCountdown.Text = $"Auto-closing in {_countdown}s...";
        }
    }

    private void ReturnToWork_Click(object sender, RoutedEventArgs e)
    {
        CloseOverlay();
    }

    private void AllowOnce_Click(object sender, RoutedEventArgs e)
    {
        // Whitelist this app for the current focus session
        _blockerService.AllowForSession(_blockedAppName);
        CloseOverlay();
    }

    private void Backdrop_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // Clicking backdrop = Return to Work
        CloseOverlay();
    }

    private void CloseOverlay()
    {
        _autoCloseTimer.Stop();
        this.Hide();
    }

    protected override void OnClosed(EventArgs e)
    {
        _autoCloseTimer.Stop();
        base.OnClosed(e);
    }
}
