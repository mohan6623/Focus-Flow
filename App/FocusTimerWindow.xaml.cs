using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ScreenTimeTracker.Services;

namespace ScreenTimeTracker.App;

/// <summary>
/// Minimal, always-on-top Focus Timer window (Wispr Flow style).
/// Gesture controls: Double-click = pause/resume, Triple-click = exit
/// </summary>
public partial class FocusTimerWindow : Window
{
    private readonly DispatcherTimer _timer;
    private readonly DispatcherTimer _clickTimer;
    private readonly AppBlockerService? _appBlockerService;
    private BlockedAppOverlay? _blockedOverlay;
    private TimeSpan _remainingTime;
    private TimeSpan _totalTime;
    private bool _isPaused;
    private bool _isBreak;
    private int _clickCount;
    private bool _showTimeSpent; // Toggle between time left and time spent
    private bool _isDragging;
    private System.Windows.Point _dragStartPoint;

    public event EventHandler? SessionCompleted;
    public event EventHandler? SessionCancelled;
    public event EventHandler<TimerTickEventArgs>? TimerTick;
    public event EventHandler<bool>? PauseStateChanged;

    // Properties for external access
    public TimeSpan RemainingTime => _remainingTime;
    public TimeSpan TotalTime => _totalTime;
    public bool IsPaused => _isPaused;

    public class TimerTickEventArgs : EventArgs
    {
        public TimeSpan Remaining { get; }
        public TimeSpan Total { get; }
        public TimerTickEventArgs(TimeSpan remaining, TimeSpan total)
        {
            Remaining = remaining;
            Total = total;
        }
    }

    private int _scheduledBreakMinutes;

    public FocusTimerWindow(int focusMinutes = 25, int breakMinutes = 5, AppBlockerService? appBlockerService = null)
    {
        InitializeComponent();
        
        _totalTime = TimeSpan.FromMinutes(focusMinutes);
        _remainingTime = _totalTime;
        _scheduledBreakMinutes = breakMinutes;
        _isPaused = false;
        _isBreak = false;
        _clickCount = 0;
        _showTimeSpent = false;
        _isDragging = false;
        _appBlockerService = appBlockerService;

        // Subscribe to blocked app detection
        if (_appBlockerService != null)
        {
            _appBlockerService.BlockedAppDetected += OnBlockedAppDetected;
        }

        // Position near taskbar (bottom-left of screen)
        this.Loaded += (s, e) =>
        {
            var workArea = SystemParameters.WorkArea;
            this.Left = 10; // Left side
            this.Top = workArea.Bottom - this.ActualHeight - 10;
        };

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += Timer_Tick;

        // Click detection timer (resets after 400ms)
        _clickTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(400)
        };
        _clickTimer.Tick += ClickTimer_Tick;

        UpdateDisplay();
        
        // Allow dragging the window AND detect clicks
        this.MouseLeftButtonDown += Window_MouseLeftButtonDown;
        TxtTimer.MouseMove += Timer_MouseMove;

        // Reinforce always-on-top behavior
        this.Topmost = true;
        this.Activated += (s, e) => this.Topmost = true;
        this.Deactivated += (s, e) => this.Topmost = true;

        this.SourceInitialized += Window_SourceInitialized;
    }



    private const int WM_SIZING = 0x0214;
    private const int WMSZ_LEFT = 1;
    private const int WMSZ_RIGHT = 2;
    private const int WMSZ_TOP = 3;
    private const int WMSZ_TOPLEFT = 4;
    private const int WMSZ_TOPRIGHT = 5;
    private const int WMSZ_BOTTOM = 6;
    private const int WMSZ_BOTTOMLEFT = 7;
    private const int WMSZ_BOTTOMRIGHT = 8;
    private double _aspectRatio;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left, top, right, bottom;
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        var handle = (new WindowInteropHelper(this)).Handle;
        HwndSource.FromHwnd(handle).AddHook(WindowProc);
        
        // Capture strictly the initial design aspect ratio
        if (double.IsNaN(this.Width) || double.IsNaN(this.Height) || this.Height == 0)
            _aspectRatio = 110.0 / 42.0;
        else
            _aspectRatio = this.Width / this.Height;
    }

    private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_SIZING)
        {
            RECT rect = Marshal.PtrToStructure<RECT>(lParam);
            double width = rect.right - rect.left;
            double height = rect.bottom - rect.top;

            int edge = wParam.ToInt32();
            
            // Enforce aspect ratio
            if (edge == WMSZ_LEFT || edge == WMSZ_RIGHT)
            {
                // Dragging sides -> Force height
                height = width / _aspectRatio;
            }
            else if (edge == WMSZ_TOP || edge == WMSZ_BOTTOM)
            {
                // Dragging top/bottom -> Force width
                width = height * _aspectRatio;
            }
            else 
            {
                // Dragging corners -> Prioritize width (force height)
                height = width / _aspectRatio;
            }

            // Apply calculated width/height back to rect
            // If we are changing a dimension that wasn't dragged (e.g. Width when dragging Bottom), 
            // we default to extending Right/Bottom.

            // 1. Update Width (Left or Right)
            if (edge == WMSZ_LEFT || edge == WMSZ_TOPLEFT || edge == WMSZ_BOTTOMLEFT)
            {
                rect.left = rect.right - (int)width;
            }
            else
            {
                // Right, TopRight, BottomRight, OR Top, Bottom (default to expanding right)
                rect.right = rect.left + (int)width;
            }

            // 2. Update Height (Top or Bottom)
            if (edge == WMSZ_TOP || edge == WMSZ_TOPLEFT || edge == WMSZ_TOPRIGHT)
            {
                rect.top = rect.bottom - (int)height;
            }
            else
            {
                // Bottom, BottomLeft, BottomRight, OR Left, Right (default to expanding bottom)
                rect.bottom = rect.top + (int)height;
            }

            Marshal.StructureToPtr(rect, lParam, true);
            handled = true;
            return new IntPtr(1); // Return TRUE to indicate processed
        }

        return IntPtr.Zero;
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        TrackClick();
        
        // Allow drag on single click
        if (_clickCount == 1)
        {
            try { this.DragMove(); } catch { }
        }
    }

    private void TrackClick()
    {
        _clickCount++;
        _clickTimer.Stop();
        _clickTimer.Start();
    }

    private void ClickTimer_Tick(object? sender, EventArgs e)
    {
        _clickTimer.Stop();
        
        // Double-click anywhere = Exit
        if (_clickCount >= 2)
        {
            ConfirmAndExit();
        }
        
        _clickCount = 0;
    }

    // Pause button click (also from menu)
    private void Pause_Click(object sender, RoutedEventArgs e)
    {
        TogglePause();
    }

    // Timer text MouseDown - start tracking for click or drag
    private void Timer_Click(object sender, MouseButtonEventArgs e)
    {
        TrackClick();
        e.Handled = true; // Prevent window drag
        
        _isDragging = false;
        _dragStartPoint = e.GetPosition(this);
        TxtTimer.CaptureMouse();
        TxtTimer.MouseLeftButtonUp += Timer_MouseUp;
    }

    private void Timer_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && TxtTimer.IsMouseCaptured)
        {
            var currentPoint = e.GetPosition(this);
            if ((currentPoint - _dragStartPoint).Length > 10) // Threshold
            {
                TxtTimer.ReleaseMouseCapture();
                TxtTimer.MouseLeftButtonUp -= Timer_MouseUp;
                _isDragging = true;
                try { this.DragMove(); } catch { }
            }
        }
    }

    private void Timer_MouseUp(object sender, MouseButtonEventArgs e)
    {
        TxtTimer.MouseLeftButtonUp -= Timer_MouseUp;
        TxtTimer.ReleaseMouseCapture();
        
        if (!_isDragging && _clickCount == 1)
        {
            // Quick single click - toggle time display
            _showTimeSpent = !_showTimeSpent;
            UpdateDisplay();
        }
        _isDragging = false;
    }

    // Click on circle to pause/resume (also track for double-click)
    private void Circle_Click(object sender, MouseButtonEventArgs e)
    {
        TrackClick();
        
        // If double-click, will be handled by timer
        // Single click = pause
        if (_clickCount == 1)
        {
            TogglePause();
        }
        e.Handled = true;
    }

    // Context menu handlers
    private void ShowTimeLeft_Click(object sender, RoutedEventArgs e)
    {
        _showTimeSpent = false;
        UpdateDisplay();
    }

    private void ShowTimeSpent_Click(object sender, RoutedEventArgs e)
    {
        _showTimeSpent = true;
        UpdateDisplay();
    }

    private void EndSession_Click(object sender, RoutedEventArgs e)
    {
        ConfirmAndExit();
    }

    private void TogglePause()
    {
        _isPaused = !_isPaused;
        // Toggle between pause bars and play triangle
        PauseIconCanvas.Visibility = _isPaused ? Visibility.Collapsed : Visibility.Visible;
        PlayIcon.Visibility = _isPaused ? Visibility.Visible : Visibility.Collapsed;
        PauseStateChanged?.Invoke(this, _isPaused);
    }

    private void ConfirmAndExit()
    {
        var result = System.Windows.MessageBox.Show(
            "Are you sure you want to end this focus session early?",
            "End Focus Session",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _timer.Stop();
            SessionCancelled?.Invoke(this, EventArgs.Empty);
            this.Close();
        }
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (_isPaused) return;

        _remainingTime = _remainingTime.Subtract(TimeSpan.FromSeconds(1));
        UpdateDisplay();
        
        // Notify tray icon
        TimerTick?.Invoke(this, new TimerTickEventArgs(_remainingTime, _totalTime));

        if (_remainingTime <= TimeSpan.Zero)
        {
            _timer.Stop();
            OnSessionComplete();
        }
    }

    private void UpdateDisplay()
    {
        TimeSpan displayTime;
        if (_showTimeSpent)
        {
            // Show time spent
            displayTime = _totalTime - _remainingTime;
            TxtTimer.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 224, 150)); // Green for spent (Success/Accent)
            // Or use dynamic resource: TxtTimer.SetResourceReference(TextBlock.ForegroundProperty, "SuccessBrush");
        }
        else
        {
            // Show time left
            displayTime = _remainingTime;
            // Use Theme Brush instead of hardcoded White
            TxtTimer.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
        }
        
        TxtTimer.Text = $"{(int)displayTime.TotalMinutes:D2}:{displayTime.Seconds:D2}";
        
        // Update progress ring using StrokeDashArray
        // Ellipse is 24x24, circumference = π * diameter = π * 24 ≈ 75.4
        double progress = 1 - (_remainingTime.TotalSeconds / _totalTime.TotalSeconds);
        double circumference = Math.PI * 24; // ~75.4 for 24px diameter
        double filledAmount = progress * circumference;
        ProgressArcFill.StrokeDashArray = new DoubleCollection { filledAmount, circumference };
    }

    public void StartTimer()
    {
        _isPaused = false;
        _timer.Start();
    }

    private void OnSessionComplete()
    {
        if (!_isBreak)
        {
            // Focus session complete - offer break
            SessionCompleted?.Invoke(this, EventArgs.Empty);
            
            // Start break
            var result = System.Windows.MessageBox.Show(
                $"Focus session complete! Take a {_scheduledBreakMinutes}-minute break?",
                "Well Done!",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
            {
                StartBreak(_scheduledBreakMinutes);
            }
            else
            {
                this.Close();
            }
        }
        else
        {
            // Break complete
            System.Windows.MessageBox.Show("Break time is over. Ready for another focus session?", "Break Complete");
            this.Close();
        }
    }

    private void StartBreak(int minutes)
    {
        _isBreak = true;
        _totalTime = TimeSpan.FromMinutes(minutes);
        _remainingTime = _totalTime;
        _isPaused = false;
        
        // Change appearance for break
        MainBorder.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 60, 40));
        ProgressArcFill.Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 200, 150));
        
        UpdateDisplay();
        _timer.Start();
    }

    private void OnBlockedAppDetected(object? sender, BlockedAppEventArgs e)
    {
        // Show overlay on UI thread
        Dispatcher.Invoke(() =>
        {
            if (_blockedOverlay == null)
            {
                _blockedOverlay = new BlockedAppOverlay(_appBlockerService!);
            }

            if (!_blockedOverlay.IsVisible)
            {
                _blockedOverlay.ShowForApp(e.AppName);
            }
        });
    }

    protected override void OnClosed(EventArgs e)
    {
        // Unsubscribe from blocker events
        if (_appBlockerService != null)
        {
            _appBlockerService.BlockedAppDetected -= OnBlockedAppDetected;
        }

        // Close overlay if open
        _blockedOverlay?.Close();

        base.OnClosed(e);
    }
}
