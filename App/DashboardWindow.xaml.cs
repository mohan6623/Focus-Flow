using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using ScreenTimeTracker.Services;
using ScreenTimeTracker.Core;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;
using Brushes = System.Windows.Media.Brushes;

namespace ScreenTimeTracker.App;

public partial class DashboardWindow : Window
{
    private readonly AggregationService _aggregationService;
    private readonly SystemTimeService _systemTimeService;
    private readonly TrayIconManager _trayIconManager;
    private readonly DispatcherTimer _refreshTimer;
    private readonly FocusService _focusService;
    private readonly AppBlockerService _appBlockerService;
    private readonly Tracking.ForegroundAppTracker _foregroundTracker;
    private readonly StreakService _streakService;

    // Tempo Theme Colors
    private readonly SolidColorBrush[] _appColors = new[]
    {
        (SolidColorBrush)new BrushConverter().ConvertFrom("#9333EA")!, // Purple (Top 1)
        (SolidColorBrush)new BrushConverter().ConvertFrom("#A855F7")!, // Light Purple (Top 2)
        (SolidColorBrush)new BrushConverter().ConvertFrom("#C084FC")!, // Lighter Purple (Top 3)
        (SolidColorBrush)new BrushConverter().ConvertFrom("#E9D5FF")!, // Lavender (Top 4)
    };

    public DashboardWindow(AggregationService aggregationService, SystemTimeService systemTimeService, TrayIconManager trayIconManager, FocusService focusService, AppBlockerService appBlockerService, Tracking.ForegroundAppTracker foregroundTracker, StreakService streakService)
    {
        InitializeComponent();
        
        _aggregationService = aggregationService;
        _systemTimeService = systemTimeService;
        _trayIconManager = trayIconManager;
        _focusService = focusService;
        _appBlockerService = appBlockerService;
        _foregroundTracker = foregroundTracker;
        _streakService = streakService;
        
        // Subscribe to streak updates
        _streakService.StreakUpdated += (s, e) => UpdateStreakDisplay();

        // Custom Window Dragging - only drag from non-interactive areas
        this.MouseLeftButtonDown += (s, e) => 
        {
            // Don't drag if clicking on the graph container
            if (!IsClickOnGraphBar(e))
            {
                this.DragMove();
            }
        };

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _refreshTimer.Tick += (s, e) => RefreshData();
        _refreshTimer.Start();

        // Initial Load
        Loaded += (s, e) => RefreshData();
    }

    private void RefreshData()
    {
        // 1. Quick Stats (Right Column)
        UpdateQuickStats();

        // 2. General Statistics (Left Column)
        UpdateGeneralStats();

        // 3. Daily Report (Center Column)
        UpdateDailyReport();
    }

    private void UpdateQuickStats()
    {
        var totalScreenTime = _aggregationService.GetTotalScreenTimeToday();
        TxtActiveTime.Text = FormatTime(totalScreenTime);
        
        // Update streak display
        UpdateStreakDisplay();
    }
    
    private void UpdateStreakDisplay()
    {
        var streak = _streakService.CurrentStreak;
        TxtStreakCount.Text = streak.ToString();
        // TxtStreakMessage is a Run
        TxtStreakMessage.Text = _streakService.GetStreakMessage();
        
        // Populate visual bubbles (Current Week: Mon-Sun)
        StreakDaysGrid.Children.Clear();
        StreakDaysGrid.ColumnDefinitions.Clear();
        StreakDaysGrid.RowDefinitions.Clear();
        
        // Definitions
        StreakDaysGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Labels
        StreakDaysGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Bubbles
        
        for (int i = 0; i < 7; i++)
            StreakDaysGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var today = DateOnly.FromDateTime(DateTime.Now);
        // Find Monday of current week
        int daysSinceMonday = ((int)today.DayOfWeek == 0) ? 6 : (int)today.DayOfWeek - 1;
        var startOfWeek = today.AddDays(-daysSinceMonday);

        var lastSession = _streakService.LastSessionDate; 
        
        for (int i = 0; i < 7; i++)
        {
            var date = startOfWeek.AddDays(i);
            
            // 1. Label
            var dayLabel = new TextBlock
            {
                Text = date.DayOfWeek.ToString().Substring(0, 1), // M, T...
                FontSize = 10, 
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175)), // Gray-400
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 6)
            };
            Grid.SetRow(dayLabel, 0);
            Grid.SetColumn(dayLabel, i);
            StreakDaysGrid.Children.Add(dayLabel);

            // 2. Bubble
            bool isCompleted = false;
            if (streak > 0 && lastSession.HasValue)
            {
                // Check if date is within the streak range ending at LastSession
                var startOfStreak = lastSession.Value.AddDays(-(streak - 1));
                if (date >= startOfStreak && date <= lastSession.Value)
                {
                    isCompleted = true;
                }
            }

            var bubble = new Border
            {
                Width = 22, Height = 22,
                CornerRadius = new CornerRadius(11),
                HorizontalAlignment = HorizontalAlignment.Center,
                Background = isCompleted 
                    ? new SolidColorBrush(Color.FromRgb(239, 68, 68)) // Red-500
                    : new SolidColorBrush(Color.FromRgb(229, 231, 235)) // Gray-200
            };
            
            if (isCompleted)
            {
                // Checkmark
                var check = new TextBlock
                {
                    Text = "✓",
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                bubble.Child = check;
            }

            Grid.SetRow(bubble, 1);
            Grid.SetColumn(bubble, i);
            StreakDaysGrid.Children.Add(bubble);
        }
    }

    private void UpdateGeneralStats()
    {
        // Clear previous
        AppListPanel.Children.Clear();
        StackedBarContainer.RowDefinitions.Clear();
        StackedBarContainer.Children.Clear();

        // Get Top 4 Apps
        var topApps = _aggregationService.GetTopAppsToday(4);
        var totalTime = _aggregationService.GetTotalScreenTimeToday().TotalSeconds;
        
        if (totalTime <= 0) totalTime = 1; // Avoid divide by zero

        // 1. Populate List (Standard Order: Big -> Small)
        for (int i = 0; i < topApps.Count; i++)
        {
            var app = topApps[i];
            var color = _appColors[i % _appColors.Length];
            AppListPanel.Children.Add(CreateAppListItem(app, color));
        }

        // 2. Populate Stacked Bar (Reverse Order: Small -> Big, so Big is at Bottom)
        // We render Top-to-Bottom, so we want Smallest first.
        
        var reversedApps = topApps.AsEnumerable().Reverse().ToList();
        
        // If there are fewer than 4 apps, we might need to handle empty space if we want full height?
        // But logic relies on Star sizing, so they will fill the space proportionally.
        // We need to account for "Other" if typical usage implies 100%. 
        // For visual "stacked card" style, usually we just stack what we have.
        
        for (int i = 0; i < reversedApps.Count; i++)
        {
            var app = reversedApps[i];
            // Find original index to get correct color
            int originalIndex = topApps.IndexOf(app);
            var color = _appColors[originalIndex % _appColors.Length];
            
            var percentage = app.TotalTime.TotalSeconds / totalTime;
            
            // Add Row
            var rowDef = new RowDefinition { Height = new GridLength(percentage, GridUnitType.Star) };
            StackedBarContainer.RowDefinitions.Add(rowDef);

            // Determine Corner Radius
            // User requested curves on edges of each tile -> All Uniform
            
            var radius = new CornerRadius(12); // Uniform rounding for all individual cards

            var barSegmentBorder = new Border
            {
                Background = color,
                CornerRadius = radius,
                Margin = new Thickness(0, 2, 0, 2) // Gap between tiles
            };

            // If it's the bottom tile (Biggest, i == count-1), add Speckles
            if (i == reversedApps.Count - 1)
            {
                var grid = new Grid();
                grid.Children.Add(CreateSpeckleCanvas());
                barSegmentBorder.Child = grid;
            }
            
            Grid.SetRow(barSegmentBorder, i);
            StackedBarContainer.Children.Add(barSegmentBorder);
        }
        
        // Note: If "Other" exists (total < 100%), we might want to add it. 
        // Logic for "Other" would be:
        // If we want "Other" to be at the TOP (Smallest?), we add it first.
        // Or if "Other" is implicit transparent space?
        // Visual style implies full card. User probably expects the apps to fill the bar or "Other" to be a segment.
        // For now, let's just stack the top apps. If 100% is not reached, the Grid/Star sizing will expand them to fill the container?
        // YES. WPF Grid with all Star rows will normalize to 100% of available space.
        // So 50%, 20%, 10% (Sum 80%) will be rendered as 50/80, 20/80, 10/80.
        // This effectively hides "Other" and expands Top Apps. This looks cleaner for this design.
    }

    private Canvas CreateSpeckleCanvas()
    {
        var canvas = new Canvas { ClipToBounds = true };
        var random = new Random();
        int dotCount = 40; // Dense enough

        // Bind canvas size or use SizeChanged? 
        // Elements in Canvas at random % positions using binding is hard in pure code without size.
        // Alternative: Use a UniformGrid or just standard absolute placement assuming a base size? No.
        // Approach: Use SizeChanged event to scatter? Or simplified:
        // Use a Viewbox or just Relative position?
        // WPF Canvas children use absolute coordinates.
        // Trick: Place items at x,y and use Loaded event.
        
        canvas.Loaded += (s, e) =>
        {
            var c = (Canvas)s;
            c.Children.Clear();
            double w = c.ActualWidth;
            double h = c.ActualHeight;
            if (w <= 0 || h <= 0) return;

            for (int k = 0; k < dotCount; k++)
            {
                var size = random.NextDouble() * 2 + 1; // 1 to 3 px
                var dot = new Ellipse
                {
                    Width = size, Height = size,
                    Fill = Brushes.White,
                    Opacity = random.NextDouble() * 0.5 + 0.1 // 0.1 to 0.6
                };
                Canvas.SetLeft(dot, random.NextDouble() * w);
                Canvas.SetTop(dot, random.NextDouble() * h);
                c.Children.Add(dot);
            }
        };
        
        return canvas;
    }

    private UIElement CreateAppListItem(AppUsageStats app, Brush color)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 15) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) }); // Dot
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Name
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Time

        // Dot
        var dot = new Border
        {
            Width = 10, Height = 10,
            CornerRadius = new CornerRadius(5),
            BorderThickness = new Thickness(2),
            BorderBrush = color,
            Background = Brushes.Transparent,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        // Name
        var nameTxt = new TextBlock
        {
            Text = app.AppName.Replace(".exe", "", StringComparison.OrdinalIgnoreCase), // Clean name
            Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128)), // Gray-500
            FontSize = 14,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(5, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        // Time
        var timeTxt = new TextBlock
        {
            Text = FormatTimeCompact(app.TotalTime),
            Foreground = new SolidColorBrush(Color.FromRgb(31, 41, 55)), // Gray-800
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };

        Grid.SetColumn(dot, 0);
        Grid.SetColumn(nameTxt, 1);
        Grid.SetColumn(timeTxt, 2);

        grid.Children.Add(dot);
        grid.Children.Add(nameTxt);
        grid.Children.Add(timeTxt);

        return grid;
    }

    private void UpdateDailyReport()
    {
        // Always update weekly chart
        WeeklyGraphContainer.Children.Clear();
        WeeklyGraphContainer.ColumnDefinitions.Clear();
        
        // Add 7 columns for 7 days
        for (int i = 0; i < 7; i++)
        {
            WeeklyGraphContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        var history = _aggregationService.GetDailyHistory(7);
        var today = DateOnly.FromDateTime(DateTime.Now);
        
        // Find max for scaling
        double maxSeconds = history.Values.Any() ? history.Values.Max(t => t.TotalSeconds) : 1;
        if (maxSeconds <= 0) maxSeconds = 1;

        int colIndex = 0;
        // Iterate last 7 days including today
        for (int i = 6; i >= 0; i--)
        {
            var date = today.AddDays(-i);
            var time = history.ContainsKey(date) ? history[date] : TimeSpan.Zero;
            var isToday = date == today;

            // Bar Container
            var container = new Grid 
            { 
                Margin = new Thickness(5, 0, 5, 0),
                Background = Brushes.Transparent, // Capture clicks
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = date
            };
            container.MouseLeftButtonDown += Day_Click;

            container.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Space above
            container.RowDefinitions.Add(new RowDefinition { Height = new GridLength(0, GridUnitType.Auto) }); // Label

            double pct = time.TotalSeconds / maxSeconds;
            // Use a Grid for the bar to handle proportional height
            var barGrid = new Grid();
            barGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1 - pct, GridUnitType.Star) }); // Empty top
            barGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(pct, GridUnitType.Star) }); // Filled bottom

            // Create gradient brush for active bars - Tempo theme
            Brush barBrush;
            if (isToday)
            {
                barBrush = new LinearGradientBrush(
                    Color.FromRgb(147, 51, 234), // Purple-600
                    Color.FromRgb(126, 34, 206), // Purple-700
                    90);
            }
            else
            {
                barBrush = new LinearGradientBrush(
                    Color.FromRgb(233, 213, 255), // Purple-200
                    Color.FromRgb(216, 180, 254), // Purple-300
                    90);
            }

            var bar = new Border
            {
                Background = barBrush,
                CornerRadius = new CornerRadius(8),
                VerticalAlignment = VerticalAlignment.Stretch,
                Effect = isToday ? new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Color.FromRgb(147, 51, 234), // Purple glow
                    BlurRadius = 12,
                    ShadowDepth = 0,
                    Opacity = 0.4
                } : null
            };
            
            Grid.SetRow(bar, 1);
            barGrid.Children.Add(bar);
            
            // Add barGrid to container
            Grid.SetRow(barGrid, 0);
            container.Children.Add(barGrid);

            // Day Label
            var dayLabel = new TextBlock
            {
                Text = date.ToString("ddd"), // Mon, Tue...
                Foreground = isToday ? new SolidColorBrush(Color.FromRgb(147, 51, 234)) : new SolidColorBrush(Color.FromRgb(107, 114, 128)), // Purple or Gray
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 8, 0, 0),
                FontSize = 12,
                FontWeight = isToday ? FontWeights.Bold : FontWeights.Normal
            };
            
            Grid.SetRow(dayLabel, 1);
            container.Children.Add(dayLabel);

            // Add to weekly container
            Grid.SetColumn(container, colIndex);
            WeeklyGraphContainer.Children.Add(container);
            
            colIndex++;
        }
    }

    private string FormatTime(TimeSpan t)
    {
        if (t.TotalHours >= 1) return $"{t.TotalHours:F1}h";
        return $"{t.Minutes}m";
    }

    private string FormatTimeCompact(TimeSpan t)
    {
        if (t.TotalHours >= 1) return $"{t.TotalHours:F1}h";
        if (t.TotalMinutes >= 1) return $"{t.TotalMinutes:F0}m";
        return $"{t.TotalSeconds:F0}s";
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        this.WindowState = WindowState.Minimized;
    }

    private void DebugButton_Click(object sender, RoutedEventArgs e)
    {
        var debugWin = new DebugWindow(_foregroundTracker);
        debugWin.Owner = this;
        debugWin.Show();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        this.Hide(); // Don't close, just hide to tray
    }

    private bool IsClickOnGraphBar(System.Windows.Input.MouseButtonEventArgs e)
    {
        // Check if the click originated from within the WeeklyGraphContainer
        var source = e.OriginalSource as DependencyObject;
        while (source != null)
        {
            if (source == WeeklyGraphContainer)
                return true;
            source = VisualTreeHelper.GetParent(source);
        }
        return false;
    }

    private void Day_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is Grid grid && grid.Tag is DateOnly date)
        {
            e.Handled = true; // Prevent event from bubbling up to trigger DragMove
            ShowHourlyDetail(date);
        }
    }

    private void BackToWeekly_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        e.Handled = true;
        
        // Hide hourly view
        HourlyGraphContainer.Visibility = Visibility.Collapsed;
        
        // Update header
        TxtReportSubtitle.Text = "Last 7 Days";
        ((TextBlock)((Border)ViewModeBadge).Child).Text = "Weekly View";
    }

    private void ShowHourlyDetail(DateOnly date)
    {
        // Update header
        TxtReportSubtitle.Text = $"{date:ddd, MMM d} - Hourly Breakdown";
        ((TextBlock)((Border)ViewModeBadge).Child).Text = "Hourly View";

        var sessions = _aggregationService.GetSessionsForDate(date);
        
        // Create hourly buckets (0-23)
        var hourlyMinutes = new double[24];
        foreach (var session in sessions)
        {
            int hour = session.StartTime.Hour;
            hourlyMinutes[hour] += session.Duration.TotalMinutes;
        }
        
        double maxMinutes = hourlyMinutes.Max();
        if (maxMinutes <= 0) maxMinutes = 1;

        // Show and populate hourly container
        HourlyGraphContainer.Visibility = Visibility.Visible;
        HourlyGraphContainer.Children.Clear();
        HourlyGraphContainer.ColumnDefinitions.Clear();

        // Add 24 columns for hours
        for (int h = 0; h < 24; h++)
        {
            HourlyGraphContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        for (int h = 0; h < 24; h++)
        {
            double pct = hourlyMinutes[h] / maxMinutes;

            // Bar Container
            var container = new Grid 
            { 
                Margin = new Thickness(1, 0, 1, 0),
                Background = Brushes.Transparent,
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = $"{h}:00 - {hourlyMinutes[h]:F0} min"
            };
            container.MouseLeftButtonDown += BackToWeekly_Click;

            container.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Space above
            container.RowDefinitions.Add(new RowDefinition { Height = new GridLength(18, GridUnitType.Pixel) }); // Label row with fixed height

            // Use a Grid for the bar to handle proportional height
            var barGrid = new Grid();
            barGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1 - pct, GridUnitType.Star) }); // Empty top
            barGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(pct, GridUnitType.Star) }); // Filled bottom

            // Create gradient brush for active bars - Tempo theme
            Brush barBrush = hourlyMinutes[h] > 0 
                ? new LinearGradientBrush(
                    Color.FromRgb(147, 51, 234), // Purple-600
                    Color.FromRgb(126, 34, 206), // Purple-700
                    90)
                : new SolidColorBrush(Color.FromRgb(243, 244, 246)); // Gray-100;

            var bar = new Border
            {
                Background = barBrush,
                CornerRadius = new CornerRadius(4),
                VerticalAlignment = VerticalAlignment.Stretch,
                Effect = hourlyMinutes[h] > 0 ? new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Color.FromRgb(147, 51, 234), // Purple glow
                    BlurRadius = 8,
                    ShadowDepth = 0,
                    Opacity = 0.3
                } : null
            };
            
            Grid.SetRow(bar, 1);
            barGrid.Children.Add(bar);
            
            Grid.SetRow(barGrid, 0);
            container.Children.Add(barGrid);

            // Hour label - show every 6 hours for cleaner look (0, 6, 12, 18)
            if (h % 6 == 0)
            {
                var label = new TextBlock 
                { 
                    Text = $"{h}h", 
                    FontSize = 10,
                    FontWeight = FontWeights.Medium,
                    Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128)), // Gray-500
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetRow(label, 1);
                container.Children.Add(label);
            }

            Grid.SetColumn(container, h);
            HourlyGraphContainer.Children.Add(container);
        }
    }

    private void FocusButton_Click(object sender, RoutedEventArgs e)
    {
        // First, show duration selection dialog
        var durationDialog = new FocusDurationDialog();
        durationDialog.Owner = this;
        durationDialog.ShowDialog();

        if (!durationDialog.Confirmed)
            return; // User cancelled

        int selectedMinutes = durationDialog.SelectedMinutes;

        // Start tray focus mode
        _trayIconManager.StartFocusMode(selectedMinutes);

        // Start focus session in service (enables app blocking)
        _focusService.StartFocusSession();

        // Open the Focus Timer popup with selected duration and blocker service
        var focusWindow = new FocusTimerWindow(selectedMinutes, _appBlockerService);
        
        // Wire up timer tick to update tray
        focusWindow.TimerTick += (s, args) =>
        {
            _trayIconManager.UpdateFocusTime(args.Remaining, args.Total);
        };

        // Wire up pause state to tray
        focusWindow.PauseStateChanged += (s, isPaused) =>
        {
            _trayIconManager.SetFocusPaused(isPaused);
        };

        focusWindow.SessionCompleted += (s, args) =>
        {
            _trayIconManager.EndFocusMode();
            _focusService.EndFocusSession(completed: true);
            _streakService.RecordSessionCompletion();
        };
        
        focusWindow.SessionCancelled += (s, args) =>
        {
            _trayIconManager.EndFocusMode();
            _focusService.EndFocusSession(completed: false);
        };

        // Handle window closed (e.g., if user closes via other means)
        focusWindow.Closed += (s, args) =>
        {
            _trayIconManager.EndFocusMode();
            _focusService.EndFocusSession(completed: false);
        };

        focusWindow.Show();
        focusWindow.StartTimer();
    }
}
