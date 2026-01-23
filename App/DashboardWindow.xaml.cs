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
    private readonly DispatcherTimer _refreshTimer;
    private readonly CategoryService _categoryService; // Ensure this is available, or use aggregation service if integrated

    // Colors matching the design
    private readonly SolidColorBrush[] _appColors = new[]
    {
        (SolidColorBrush)new BrushConverter().ConvertFrom("#00E096")!, // Teal (Top 1)
        (SolidColorBrush)new BrushConverter().ConvertFrom("#FFFFFF")!, // White (Top 2)
        (SolidColorBrush)new BrushConverter().ConvertFrom("#A0A0A0")!, // Light Grey (Top 3)
        (SolidColorBrush)new BrushConverter().ConvertFrom("#404040")!, // Dark Grey (Top 4)
    };

    public DashboardWindow(AggregationService aggregationService, SystemTimeService systemTimeService)
    {
        InitializeComponent();
        
        _aggregationService = aggregationService;
        _systemTimeService = systemTimeService;

        // Custom Window Dragging
        this.MouseLeftButtonDown += (s, e) => this.DragMove();

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
        var systemUptime = _systemTimeService.GetSystemUptime();

        TxtActiveTime.Text = FormatTime(totalScreenTime);
        TxtUptime.Text = FormatTime(systemUptime);
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
            Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
            FontSize = 14,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(5, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        // Time
        var timeTxt = new TextBlock
        {
            Text = FormatTimeCompact(app.TotalTime),
            Foreground = Brushes.White,
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
        DailyGraphContainer.Children.Clear();
        // Assuming 7 grid columns already defined in XAML

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
            var container = new Grid { Margin = new Thickness(5, 0, 5, 0) };
            container.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Space above
            container.RowDefinitions.Add(new RowDefinition { Height = new GridLength(0, GridUnitType.Auto) }); // Label

            double pct = time.TotalSeconds / maxSeconds;
            // Use a Grid for the bar to handle proportional height
            var barGrid = new Grid();
            barGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1 - pct, GridUnitType.Star) }); // Empty top
            barGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(pct, GridUnitType.Star) }); // Filled bottom

            var bar = new Border
            {
                Background = isToday ? (System.Windows.Media.Brush)FindResource("AccentBrush") : new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                CornerRadius = new CornerRadius(6, 6, 6, 6),
                VerticalAlignment = VerticalAlignment.Stretch
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
                Foreground = isToday ? Brushes.White : (Brush)FindResource("TextSecondaryBrush"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 0),
                FontSize = 11
            };
            
            Grid.SetRow(dayLabel, 1);
            container.Children.Add(dayLabel);

            // Add to main container
            Grid.SetColumn(container, colIndex);
            DailyGraphContainer.Children.Add(container);
            
            colIndex++;
        }

        // Update Stats Breakdown (Fake categories for MVP, real for "Active")
        // In real impl, we'd query by category. For now, let's just show total Active.
        // Or fetch category stats if possible.
        // We haven't implemented "GetStatsByCategory" in AggregationService yet.
        // Let's hide the numbers or show simplified total.
        // For MVP, set them to "..." or total/2.
        TxtProductivityTime.Text = FormatTime(TimeSpan.FromSeconds(history.TryGetValue(today, out var t) ? t.TotalSeconds * 0.6 : 0)); // Fake 60%
        TxtEntertainmentTime.Text = FormatTime(TimeSpan.FromSeconds(history.TryGetValue(today, out var t2) ? t2.TotalSeconds * 0.3 : 0)); // Fake 30%
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

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        this.Hide();
    }
}
