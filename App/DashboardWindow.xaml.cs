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
    private readonly CategoryService _categoryService;
    private readonly ThemeManager _themeManager;
    
    // Active focus session tracking
    private FocusTimerWindow? _activeFocusWindow = null;

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
        StreakService streakService,
        CategoryService categoryService)
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
        _categoryService = categoryService;
        
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
        Loaded += (s, e) => {
            UpdateDailyReport(); // Load graph once on startup
            RefreshData(); // Start updating stats
            // Force verify current foreground on load
            _foregroundTracker.CheckCurrentForeground();
        };
        
        // Debug: Listen to foreground changes
        _foregroundTracker.ForegroundChanged += (s, e) =>
        {
            Dispatcher.Invoke(() =>
            {
                if (TxtTrackingDebug != null)
                {
                    TxtTrackingDebug.Text = $"Tracking: {e.EffectiveName ?? e.AppName} ({e.ProcessId})";}
            });
        };
    }

    private void RefreshData()
    {
        // Quick Stats only - called every second by timer
        // Do NOT call UpdateDailyReport here - it blocks UI
        UpdateStats();
    }

    private async void UpdateStats()
    {
        if (System.Windows.Application.Current == null) return;

        var totalScreenTime = _aggregationService.GetTotalScreenTimeToday();
        TxtActiveTime.Text = FormatTime(totalScreenTime);

        UpdateStreakDisplay();
        // UpdateGeneralStats(); // Disabled widget
        await UpdateAppUsageList();
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
            
            // 1. Label (Day of Week)
            var dayLabel = new TextBlock
            {
                Text = date.DayOfWeek.ToString().Substring(0, 1), // M, T...
                FontSize = 11, 
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextSecondaryBrush"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8)
            };
            Grid.SetRow(dayLabel, 0);
            Grid.SetColumn(dayLabel, i);
            StreakDaysGrid.Children.Add(dayLabel);

            // 2. Bubble Container
            bool isCompleted = false;
            if (streak > 0 && lastSession.HasValue)
            {
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
                    ? (Brush)FindResource("DangerBrush") // Red for completed streak
                    : (Brush)FindResource("BorderBrush")
            };
            
            if (isCompleted)
            {
                // Checkmark for completed days
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

            bubble.Child = bubble.Child;

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

    private async Task UpdateAppUsageList()
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
            var item = await CreateAppUsageListItem(app, totalSeconds);
            AppUsageListContainer.Children.Add(item);
        }
    }

    private async Task<Grid> CreateAppUsageListItem(AppUsageStats app, double totalSeconds)
    {
        double percentage = (app.TotalTime.TotalSeconds / totalSeconds) * 100;
        if (percentage < 1) percentage = 1; // Minimum width

        // Container Grid
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Text Row
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Bar Row

        // 1. Text Row: "[Icon] Name | Time"
        var textStack = new StackPanel 
        { 
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 6)
        };

        // App Icon (Rounded)
        var icon = await Services.IconHelper.GetIconAsync(app.AppPath, app.AppName, app.WebsiteDomain);
        var iconBorder = new Border
        {
            Width = 22,
            Height = 22,
            CornerRadius = new CornerRadius(5), // Visible rounded corners
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Background = new ImageBrush 
            { 
                ImageSource = icon, 
                Stretch = Stretch.Uniform 
            }
        };
        RenderOptions.SetBitmapScalingMode(iconBorder, BitmapScalingMode.HighQuality);
        textStack.Children.Add(iconBorder);

        var nameTxt = new TextBlock
        {
            Text = app.AppName.Replace(".exe", "", StringComparison.OrdinalIgnoreCase),
            Foreground = (Brush)FindResource("TextPrimaryBrush"), 
            FontSize = 13,
            FontWeight = FontWeights.Medium,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 140,
            VerticalAlignment = VerticalAlignment.Center
        };

        var separatorTxt = new TextBlock
        {
            Text = " | ",
            Foreground = (Brush)FindResource("TextSecondaryBrush"),
            FontSize = 13,
            Margin = new Thickness(4, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

         var timeTxt = new TextBlock
        {
            Text = FormatTimeCompact(app.TotalTime),
            Foreground = (Brush)FindResource("TextSecondaryBrush"),
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center
        };

        textStack.Children.Add(nameTxt);
        textStack.Children.Add(separatorTxt);
        textStack.Children.Add(timeTxt);

        Grid.SetRow(textStack, 0);
        grid.Children.Add(textStack);

        // 2. Progress Bar (using accent color as before)
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
            Background = (Brush)FindResource("AccentBrush"), // Keep original accent color
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

    private bool _isUpdatingGraph = false;
    
    private void UpdateDailyReport()
    {
        // Prevent re-entry
        if (_isUpdatingGraph) return;
        _isUpdatingGraph = true;

        try
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            // Use only fast pre-aggregated daily history - NO session queries
            var history = _aggregationService.GetDailyHistory(7);
            
            // Find max for scaling
            double maxSeconds = history.Values.Any() ? history.Values.Max(t => t.TotalSeconds) : 1;
            if (maxSeconds <= 0) maxSeconds = 1;

            // Clear and render
            WeeklyGraphContainer.Children.Clear();
            WeeklyGraphContainer.ColumnDefinitions.Clear();
            
            // Add 7 columns for 7 days
            for (int i = 0; i < 7; i++)
            {
                WeeklyGraphContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            // Render simple solid color bars (FAST)
            int colIndex = 0;
            for (int i = 6; i >= 0; i--)
            {
                var date = today.AddDays(-i);
                var time = history.ContainsKey(date) ? history[date] : TimeSpan.Zero;
                var isToday = date == today;

                // Bar Container
                var container = new Grid 
                { 
                    Margin = new Thickness(5, 0, 5, 0),
                    Background = Brushes.Transparent,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Tag = date
                };
                container.MouseLeftButtonDown += Day_Click;

                container.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                container.RowDefinitions.Add(new RowDefinition { Height = new GridLength(0, GridUnitType.Auto) });

                double pct = time.TotalSeconds / maxSeconds;
                
                var barGrid = new Grid();
                barGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1 - pct, GridUnitType.Star) });
                barGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(pct, GridUnitType.Star) });

                // Simple solid gradient bar (FAST - no category lookup)
                var accentLight = GetColorResource("AccentLightColor", Color.FromRgb(233, 213, 255));
                var accent = GetColorResource("AccentColor", Color.FromRgb(147, 51, 234));
                var inactiveStart = GetColorResource("BarInactiveColorStart", Color.FromRgb(233, 213, 255));
                var inactiveEnd = GetColorResource("BarInactiveColorEnd", Color.FromRgb(216, 180, 254));

                var bar = new Border
                {
                    Background = time.TotalSeconds > 0 
                        ? new LinearGradientBrush(
                            accentLight, 
                            accent, 
                            90)
                        : new LinearGradientBrush(
                            inactiveStart, 
                            inactiveEnd, 
                            90),
                    CornerRadius = new CornerRadius(8),
                    ToolTip = $"{date:ddd}: {FormatTimeCompact(time)}",
                    Effect = isToday ? new System.Windows.Media.Effects.DropShadowEffect
                    {
                        Color = Color.FromRgb(147, 51, 234),
                        BlurRadius = 12,
                        ShadowDepth = 0,
                        Opacity = 0.4
                    } : null
                };
                
                Grid.SetRow(bar, 1);
                barGrid.Children.Add(bar);
                
                Grid.SetRow(barGrid, 0);
                container.Children.Add(barGrid);

                // Day Label
                var dayLabel = new TextBlock
                {
                    Text = date.ToString("ddd"),
                    Foreground = isToday
                        ? GetBrushResource("AccentBrush", Brushes.MediumPurple)
                        : GetBrushResource("TextSecondaryBrush", Brushes.Gray),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 8, 0, 0),
                    FontSize = 12,
                    FontWeight = isToday ? FontWeights.Bold : FontWeights.Normal
                };
                
                Grid.SetRow(dayLabel, 1);
                container.Children.Add(dayLabel);

                Grid.SetColumn(container, colIndex);
                WeeklyGraphContainer.Children.Add(container);
                
                colIndex++;
            }
        
            // Update Daily Average display
            var weeklyTotalSeconds = history.Values.Sum(t => t.TotalSeconds);
            var avgSeconds = weeklyTotalSeconds / 7.0;
            var avgTime = TimeSpan.FromSeconds(avgSeconds);
            TxtDailyAverage.Text = avgTime.TotalHours >= 1 
                ? $"{(int)avgTime.TotalHours}h {avgTime.Minutes}m"
                : $"{avgTime.Minutes}m";

            // Render simple Category Legend (no data - just placeholders for now)
            CategoryLegendContainer.Children.Clear();
            CategoryLegendContainer.ColumnDefinitions.Clear();
            CategoryLegendContainer.RowDefinitions.Clear();
            
            // 2x2 grid
            CategoryLegendContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            CategoryLegendContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            CategoryLegendContainer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            CategoryLegendContainer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            
            // Fixed 4 categories
            var fixedCategories = new (string name, int col, int row)[]
            {
                ("Productivity", 0, 0),
                ("Entertainment", 1, 0),
                ("Social", 0, 1),
                ("Other", 1, 1)
            };
            
            foreach (var (catName, col, row) in fixedCategories)
            {
                var legendItem = new StackPanel 
                { 
                    Orientation = System.Windows.Controls.Orientation.Horizontal, 
                    Margin = new Thickness(0, 8, 12, 8) 
                };
                
                var categoryBrushKey = CategoryService.GetCategoryBrushKey(catName);
                var categoryBrush = GetBrushResource(categoryBrushKey, GetBrushResource("CategoryOtherBrush", Brushes.Gray));
                var categoryIcon = CategoryService.GetCategoryIcon(catName);
                
                var iconGrid = new Grid { Width = 36, Height = 36, Margin = new Thickness(0, 0, 10, 0) };
                var iconBg = new Border
                {
                    Width = 36,
                    Height = 36,
                    CornerRadius = new CornerRadius(18),
                    Background = categoryBrush,
                    Opacity = 0.15
                };
                var iconEmoji = new TextBlock
                {
                    Text = categoryIcon,
                    FontSize = 16,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                iconGrid.Children.Add(iconBg);
                iconGrid.Children.Add(iconEmoji);
                
                var categoryNameText = new TextBlock 
                { 
                    Text = catName, 
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = (Brush)FindResource("TextPrimaryBrush"),
                    VerticalAlignment = VerticalAlignment.Center
                };
                
                legendItem.Children.Add(iconGrid);
                legendItem.Children.Add(categoryNameText);
                
                Grid.SetColumn(legendItem, col);
                Grid.SetRow(legendItem, row);
                CategoryLegendContainer.Children.Add(legendItem);
            }
        }
        finally
        {
            _isUpdatingGraph = false;
        }
    }

    private string FormatTime(TimeSpan t)
    {
        if (t.TotalHours >= 1) return $"{t.TotalHours:F1}h";
        return $"{t.Minutes}m";
    }

    private Brush GetBrushResource(string key, Brush fallback)
    {
        if (TryFindResource(key) is Brush brush)
            return brush;

        return fallback;
    }

    private Color GetColorResource(string key, Color fallback)
    {
        var resource = TryFindResource(key);
        if (resource is Color color)
            return color;

        if (resource is SolidColorBrush brush)
            return brush.Color;

        return fallback;
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
            
            // Navigate to Activity Log section
            NavigateToActivityLog(date);
        }
    }

    private void NavigateToActivityLog(DateOnly date)
    {
        // TODO: Navigate to Activity Log section with the selected date
        // For now, just show a message
        System.Windows.MessageBox.Show($"Activity Log for {date:ddd, MMM d}\n\nThis will open the Activity Log section.", "Activity Log", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void FocusButton_Click(object sender, RoutedEventArgs e)
    {
        // Prevent concurrent focus sessions
        if (_activeFocusWindow != null)
        {
            System.Windows.MessageBox.Show("A focus session is already active. Please stop it first.", "Focus Session Active", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // First, show duration selection dialog
        var durationDialog = new FocusDurationDialog();
        durationDialog.Owner = this;
        durationDialog.ShowDialog();

        if (!durationDialog.Confirmed)
            return; // User cancelled

        int selectedMinutes = durationDialog.SelectedMinutes;
        int breakMinutes = durationDialog.BreakMinutes;
        string intent = string.IsNullOrEmpty(durationDialog.FocusIntent) ? "Deep Work" : durationDialog.FocusIntent;

        // Show Progress Card
        FocusProgressCard.Visibility = Visibility.Visible;
        TxtCurrentIntent.Text = intent;
        UpdateFocusUIDisplay(TimeSpan.FromMinutes(selectedMinutes), TimeSpan.FromMinutes(selectedMinutes));

        // Start tray focus mode
        _trayIconManager.StartFocusMode(selectedMinutes);

        // Start focus session in service (enables app blocking)
        _focusService.StartFocusSession();

        // Open the Focus Timer popup with selected duration and blocker service
        var focusWindow = new FocusTimerWindow(selectedMinutes, breakMinutes, _appBlockerService);
        _activeFocusWindow = focusWindow;
        
        // Wire up timer tick to update tray and dashboard
        focusWindow.TimerTick += (s, args) =>
        {
            Dispatcher.Invoke(() => {
                _trayIconManager.UpdateFocusTime(args.Remaining, args.Total);
                UpdateFocusUIDisplay(args.Remaining, args.Total);
            });
        };

        // Wire up pause state to tray
        focusWindow.PauseStateChanged += (s, isPaused) =>
        {
            Dispatcher.Invoke(() => {
                _trayIconManager.SetFocusPaused(isPaused);
                TxtFocusStatus.Text = isPaused ? "Focus Session Paused" : "Focus Session Active";
                TxtFocusStatus.Foreground = isPaused ? (Brush)FindResource("TextSecondaryBrush") : (Brush)FindResource("AccentBrush");
            });
        };

        focusWindow.SessionCompleted += (s, args) =>
        {
            Dispatcher.Invoke(() => {
                _trayIconManager.EndFocusMode();
                _focusService.EndFocusSession(completed: true);
                _streakService.RecordSessionCompletion();
                FocusProgressCard.Visibility = Visibility.Collapsed;
                _activeFocusWindow = null;
            });
        };
        
        focusWindow.SessionCancelled += (s, args) =>
        {
            Dispatcher.Invoke(() => {
                _trayIconManager.EndFocusMode();
                _focusService.EndFocusSession(completed: false);
                FocusProgressCard.Visibility = Visibility.Collapsed;
                _activeFocusWindow = null;
            });
        };

        // Handle window closed (e.g., if user closes via other means)
        focusWindow.Closed += (s, args) =>
        {
            Dispatcher.Invoke(() => {
                _trayIconManager.EndFocusMode();
                _focusService.EndFocusSession(completed: false);
                FocusProgressCard.Visibility = Visibility.Collapsed;
                _activeFocusWindow = null;
            });
        };

        focusWindow.Show();
        focusWindow.StartTimer();
    }

    private void StopFocusButton_Click(object sender, RoutedEventArgs e)
    {
        if (_activeFocusWindow != null)
        {
            _activeFocusWindow.Close();
            _activeFocusWindow = null;
        }
        
        _trayIconManager.EndFocusMode();
        _focusService.EndFocusSession(completed: false);
        FocusProgressCard.Visibility = Visibility.Collapsed;
    }

    // Focus timer state
    private bool _focusShowTimeSpent = false;
    private TimeSpan _focusRemaining;
    private TimeSpan _focusTotal;

    private void FocusTimerDisplay_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _focusShowTimeSpent = !_focusShowTimeSpent;
        UpdateFocusUIDisplay(_focusRemaining, _focusTotal);
    }

    private void UpdateFocusUIDisplay(TimeSpan remaining, TimeSpan total)
    {
        _focusRemaining = remaining;
        _focusTotal = total;
        
        TimeSpan displayTime;
        if (_focusShowTimeSpent)
        {
            displayTime = total - remaining;
            TxtFocusTimeLabel.Text = "SPENT";
            TxtFocusTimeLeft.Foreground = (Brush)FindResource("SuccessBrush"); // Green for spent
        }
        else
        {
            displayTime = remaining;
            TxtFocusTimeLabel.Text = "LEFT";
            TxtFocusTimeLeft.Foreground = (Brush)FindResource("TextPrimaryBrush"); // Default
        }

        TxtFocusTimeLeft.Text = $"{(int)displayTime.TotalMinutes:D2}:{displayTime.Seconds:D2}";
        
        double progress = 1 - (remaining.TotalSeconds / total.TotalSeconds);
        FocusProgressBar.Value = progress * 100;

        // Update Arc (same logic as widget)
        double circumference = 3.14159; 
        double filledAmount = progress * circumference;
        FocusProgressArc.StrokeDashArray = new DoubleCollection { filledAmount, 100 };
    }
}
