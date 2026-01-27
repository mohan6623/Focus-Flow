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
    private readonly ThemeManager _themeManager;

    // Tempo Theme Colors - Now retrieved dynamically or kept for graph logic
    // We will use DynamicResource in XAML, but for code-behind graph we might need access.
    // However, the Graph bars use specific colors. We can keep these or update them.
    // For now, let's keep them but maybe update to use theme colors if needed later.

    public DashboardWindow(
        AggregationService aggregationService, 
        SystemTimeService systemTimeService, 
        TrayIconManager trayIconManager, 
        FocusService focusService, 
        AppBlockerService appBlockerService, 
        Tracking.ForegroundAppTracker foregroundTracker, 
        ThemeManager themeManager,
        StreakService streakService)
    {
        InitializeComponent();
        
        _aggregationService = aggregationService;
        _systemTimeService = systemTimeService;
        _trayIconManager = trayIconManager;
        _focusService = focusService;
        _appBlockerService = appBlockerService;
        _foregroundTracker = foregroundTracker;
        _themeManager = themeManager;
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

        // 2. Daily Report (Center Column)
        UpdateDailyReport();
    }

    private void UpdateQuickStats()
    {
        var totalScreenTime = _aggregationService.GetTotalScreenTimeToday();
        TxtActiveTime.Text = FormatTime(totalScreenTime);
        
        // Day labels are updated in UpdateStreakDisplay()
        UpdateGeneralStats();
        UpdateAppUsageList();
        
        // Update streak display
        UpdateStreakDisplay();
    }
    
    private void UpdateStreakDisplay()
    {
        var streak = _streakService.CurrentStreak;
        TxtStreakCount.Text = streak.ToString();
        
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
                    ? (Brush)FindResource("SuccessBrush") // Use Success instead of Red for positive streak? Or stick to Fire Red?
                    : (Brush)FindResource("BorderBrush") 
            };
            
            // If we want Red for streak, we should add Fire/Danger brush
            if (isCompleted) bubble.Background = (Brush)FindResource("DangerBrush"); // Fire is usually red/orange
            
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
        /* Widget removed from UI */
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

    private void UpdateAppUsageList()
    {
        if (AppUsageListContainer == null) return;

        AppUsageListContainer.Children.Clear();

        var stats = _aggregationService.GetTodayStats();
        var totalTime = _aggregationService.GetTotalScreenTimeToday();
        var totalSeconds = totalTime.TotalSeconds;

        if (totalSeconds <= 0) totalSeconds = 1; // Avoid division by zero

        // Sort by time desc
        var topApps = stats.OrderByDescending(s => s.TotalTime).Take(8).ToList(); // Show top 8

        foreach (var app in topApps)
        {
            var item = CreateAppUsageListItem(app, totalSeconds);
            AppUsageListContainer.Children.Add(item);
        }
    }

    private UIElement CreateAppUsageListItem(AppUsageStats app, double totalSeconds)
    {
        double percentage = (app.TotalTime.TotalSeconds / totalSeconds) * 100;
        if (percentage > 100) percentage = 100;
        if (percentage < 0) percentage = 0;

        // Container Grid
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Text Row
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Bar Row

        // 1. Text Row: "Name | Time"
        var textStack = new StackPanel 
        { 
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 6)
        };

        var nameTxt = new TextBlock
        {
            Text = app.AppName.Replace(".exe", "", StringComparison.OrdinalIgnoreCase),
            Foreground = (Brush)FindResource("TextPrimaryBrush"), 
            FontSize = 13,
            FontWeight = FontWeights.Medium,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 140
        };

        var separatorTxt = new TextBlock
        {
            Text = " | ",
            Foreground = (Brush)FindResource("TextSecondaryBrush"),
            FontSize = 13,
            Margin = new Thickness(4, 0, 4, 0)
        };

         var timeTxt = new TextBlock
        {
            Text = FormatTimeCompact(app.TotalTime),
            Foreground = (Brush)FindResource("TextSecondaryBrush"),
            FontSize = 13
        };

        textStack.Children.Add(nameTxt);
        textStack.Children.Add(separatorTxt);
        textStack.Children.Add(timeTxt);

        Grid.SetRow(textStack, 0);
        grid.Children.Add(textStack);

        // 2. Progress Bar
        var barGrid = new Grid { Height = 6, ClipToBounds = true }; 
        var barCornerRadius = new CornerRadius(3);
        
        // Background track
        var track = new Border { Background = (Brush)FindResource("BorderBrush"), CornerRadius = barCornerRadius, Opacity = 0.5 };
        
        // Fill
        var fillDef = new ColumnDefinition { Width = new GridLength(percentage, GridUnitType.Star) };
        var emptyDef = new ColumnDefinition { Width = new GridLength(100 - percentage, GridUnitType.Star) };
        
        var fillGrid = new Grid();
        fillGrid.ColumnDefinitions.Add(fillDef);
        fillGrid.ColumnDefinitions.Add(emptyDef);
        
        var fill = new Border 
        { 
            Background = (Brush)FindResource("AccentBrush"), 
            CornerRadius = barCornerRadius
        };
        Grid.SetColumn(fill, 0);
        
        fillGrid.Children.Add(fill);
        
        barGrid.Children.Add(track);
        barGrid.Children.Add(fillGrid);

        Grid.SetRow(barGrid, 1);
        grid.Children.Add(barGrid);

        return grid;
    }

    private void ThemeButton_Click(object sender, RoutedEventArgs e)
    {
        _themeManager.ToggleTheme();
        UpdateAllDynamicResources();
    }

    private void UpdateAllDynamicResources()
    {
        // WPF DynamicResource usually handles this automatically if the resource dictionary is replaced at the App level.
        // However, since we are doing manual merging in ThemeManager, let's verify if we need to force update.
        // If we replaced Application.Current.Resources, it should propagate.
        // But if it doesn't, we might need to invalidate visual tree or re-apply.
        // Let's assume automatic propagation for now. 
        // We might want to update the icon of the button though.
        
        UpdateThemeIcon();
        
        // Re-render graphs if they use hardcoded brushes that need to switch
        RefreshData();
    }

    private void UpdateThemeIcon()
    {
         if (BtnThemeTheme?.Content is TextBlock tb)
         {
             tb.Text = _themeManager.CurrentTheme == ThemeType.Light ? "🌙" : "☀️";
         }
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
            var accentColor = (Color)FindResource("AccentColor");
            var accentLightColor = (Color)FindResource("AccentLightColor"); // Or use a darker shade for gradient end?
            
            // Actually, let's use the define BarActiveColorStart/End if available, or just derive
            // For now, let's just use the AccentColor for simplicity or simple gradient
            // But to keep the "Wow" factor, let's use the gradient.
            // We can treat AccentColor as Start and a slightly modified version as End, or just use Solid for now to be safe with switching.
            // Or better: Let's use the resource brushes directly if possible.
            // But we need a gradient.
            
            if (isToday)
            {
                barBrush = new LinearGradientBrush(
                    (Color)FindResource("BarActiveColorStart"), 
                    (Color)FindResource("BarActiveColorEnd"), 
                    90);
            }
            else
            {
                barBrush = new LinearGradientBrush(
                    (Color)FindResource("BarInactiveColorStart"), 
                    (Color)FindResource("BarInactiveColorEnd"), 
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
                Foreground = isToday ? (Brush)FindResource("AccentBrush") : (Brush)FindResource("TextSecondaryBrush"),
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
                    (Color)FindResource("BarActiveColorStart"),
                    (Color)FindResource("BarActiveColorEnd"),
                    90)
                : (Brush)FindResource("BgBrush"); // inactive

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
