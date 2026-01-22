using System.Drawing;
using ScreenTimeTracker.Utilities;

namespace ScreenTimeTracker.App;

/// <summary>
/// Manages the system tray icon and context menu.
/// </summary>
public class TrayIconManager : IDisposable
{
    private NotifyIcon? _notifyIcon;
    private ContextMenuStrip? _contextMenu;
    private ToolStripMenuItem? _pauseMenuItem;
    private ToolStripMenuItem? _resumeMenuItem;
    private bool _isPaused;
    private bool _disposed;

    /// <summary>
    /// Raised when user requests to pause tracking.
    /// </summary>
    public event EventHandler? PauseRequested;

    /// <summary>
    /// Raised when user requests to resume tracking.
    /// </summary>
    public event EventHandler? ResumeRequested;

    /// <summary>
    /// Raised when user requests to exit.
    /// </summary>
    public event EventHandler? ExitRequested;

    /// <summary>
    /// Raised when user requests to view stats.
    /// </summary>
    public event EventHandler? StatsRequested;

    /// <summary>
    /// Shows the tray icon.
    /// </summary>
    public void Show()
    {
        if (_notifyIcon != null)
            return;

        CreateContextMenu();
        CreateNotifyIcon();

        Logger.Info("Tray icon shown");
    }

    /// <summary>
    /// Hides the tray icon.
    /// </summary>
    public void Hide()
    {
        if (_notifyIcon == null)
            return;

        _notifyIcon.Visible = false;
        Logger.Info("Tray icon hidden");
    }

    /// <summary>
    /// Updates the tray icon tooltip.
    /// </summary>
    public void UpdateTooltip(string text)
    {
        if (_notifyIcon != null)
        {
            // Tooltip max length is 127 chars
            _notifyIcon.Text = text.Length > 127 ? text[..127] : text;
        }
    }

    /// <summary>
    /// Updates the paused state in the menu.
    /// </summary>
    public void SetPaused(bool isPaused)
    {
        _isPaused = isPaused;

        if (_pauseMenuItem != null)
            _pauseMenuItem.Visible = !isPaused;

        if (_resumeMenuItem != null)
            _resumeMenuItem.Visible = isPaused;

        // Update icon to indicate paused state
        if (_notifyIcon != null)
        {
            UpdateTooltip(isPaused ? "Screen Time Tracker (Paused)" : "Screen Time Tracker");
        }
    }

    private void CreateNotifyIcon()
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = CreateDefaultIcon(),
            Text = "Screen Time Tracker",
            Visible = true,
            ContextMenuStrip = _contextMenu
        };

        _notifyIcon.DoubleClick += (s, e) =>
        {
            // Future: Open stats window
            ShowBalloon("Screen Time Tracker", "Double-click to view stats (coming soon)");
        };
    }

    private void CreateContextMenu()
    {
        _contextMenu = new ContextMenuStrip();

        // Status header
        var statusItem = new ToolStripMenuItem("Screen Time Tracker")
        {
            Enabled = false,
            Font = new Font(_contextMenu.Font, FontStyle.Bold)
        };
        _contextMenu.Items.Add(statusItem);

        _contextMenu.Items.Add(new ToolStripSeparator());

        // Pause/Resume
        _pauseMenuItem = new ToolStripMenuItem("Pause Tracking", null, (s, e) =>
        {
            PauseRequested?.Invoke(this, EventArgs.Empty);
        });
        _contextMenu.Items.Add(_pauseMenuItem);

        _resumeMenuItem = new ToolStripMenuItem("Resume Tracking", null, (s, e) =>
        {
            ResumeRequested?.Invoke(this, EventArgs.Empty);
        })
        {
            Visible = false
        };
        _contextMenu.Items.Add(_resumeMenuItem);

        _contextMenu.Items.Add(new ToolStripSeparator());

        // View stats
        var viewStatsItem = new ToolStripMenuItem("View Stats", null, (s, e) =>
        {
            StatsRequested?.Invoke(this, EventArgs.Empty);
        });
        _contextMenu.Items.Add(viewStatsItem);

        // Settings (future)
        var settingsItem = new ToolStripMenuItem("Settings", null, (s, e) =>
        {
            ShowBalloon("Coming Soon", "Settings will be available in a future update");
        });
        _contextMenu.Items.Add(settingsItem);

        _contextMenu.Items.Add(new ToolStripSeparator());

        // Exit
        var exitItem = new ToolStripMenuItem("Exit", null, (s, e) =>
        {
            ExitRequested?.Invoke(this, EventArgs.Empty);
        });
        _contextMenu.Items.Add(exitItem);
    }

    private void ShowBalloon(string title, string text)
    {
        _notifyIcon?.ShowBalloonTip(3000, title, text, ToolTipIcon.Info);
    }

    private static Icon CreateDefaultIcon()
    {
        // Create a simple clock-like icon programmatically
        // In production, you'd load from resources
        var bitmap = new Bitmap(32, 32);
        
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = global::System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            
            // Background circle
            using (var brush = new SolidBrush(Color.FromArgb(76, 175, 80))) // Green
            {
                g.FillEllipse(brush, 2, 2, 28, 28);
            }

            // Clock hands
            using (var pen = new Pen(Color.White, 2))
            {
                // Center point
                var centerX = 16;
                var centerY = 16;

                // Hour hand (pointing to 10 o'clock)
                g.DrawLine(pen, centerX, centerY, centerX - 5, centerY - 7);

                // Minute hand (pointing to 2 o'clock)
                g.DrawLine(pen, centerX, centerY, centerX + 7, centerY - 5);
            }

            // Center dot
            using (var brush = new SolidBrush(Color.White))
            {
                g.FillEllipse(brush, 14, 14, 4, 4);
            }
        }

        var hIcon = bitmap.GetHicon();
        var icon = Icon.FromHandle(hIcon);
        return icon;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Hide();

        _notifyIcon?.Dispose();
        _notifyIcon = null;

        _contextMenu?.Dispose();
        _contextMenu = null;

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~TrayIconManager()
    {
        Dispose();
    }
}
