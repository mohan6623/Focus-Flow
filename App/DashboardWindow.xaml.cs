using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using ScreenTimeTracker.Services;
using ScreenTimeTracker.Core;

namespace ScreenTimeTracker.App;

public partial class DashboardWindow : Window
{
    private readonly AggregationService _aggregationService;
    private readonly SystemTimeService _systemTimeService;
    private readonly DispatcherTimer _refreshTimer;

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

        RefreshData();
    }

    private void RefreshData()
    {
        var totalScreenTime = _aggregationService.GetTotalScreenTimeToday();
        var systemUptime = _systemTimeService.GetSystemUptime();
        var appUptime = _systemTimeService.GetAppUptime();
        var backgroundTime = appUptime - totalScreenTime;
        if (backgroundTime < TimeSpan.Zero) backgroundTime = TimeSpan.Zero;

        // Update Summary Cards
        TxtActiveTime.Text = FormatTime(totalScreenTime);
        TxtUptime.Text = FormatTime(systemUptime);
        TxtBackground.Text = FormatTime(backgroundTime);

        // Update List
        RefreshAppList(totalScreenTime.TotalSeconds);
    }

    private void RefreshAppList(double totalSecondsDay)
    {
        // For efficiency in MVP, clear and rebuild. 
        // In prod, bind to ObservableCollection.
        AppListPanel.Children.Clear();

        var stats = _aggregationService.GetTodayStats()
            .OrderByDescending(s => s.TotalTime)
            .Take(10);

        foreach (var stat in stats)
        {
            AppListPanel.Children.Add(CreateAppRow(stat, totalSecondsDay));
        }
    }

    private System.Windows.UIElement CreateAppRow(AppUsageStats stat, double totalSecondsDay)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 15) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) }); // Name
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) }); // Time (FIXED: wrapped in ColumnDefinition)
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Progress

        // Name
        var nameTxt = new TextBlock
        {
            Text = stat.AppName,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 200, 200)),
            VerticalAlignment = System.Windows.VerticalAlignment.Center,
            FontSize = 14
        };

        // Time
        var timeTxt = new TextBlock
        {
            Text = FormatTimeCompact(stat.TotalTime),
            Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = System.Windows.VerticalAlignment.Center,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            Margin = new Thickness(0, 0, 15, 0)
        };

        // Progress Bar (Custom visual)
        var pct = totalSecondsDay > 0 ? stat.TotalTime.TotalSeconds / totalSecondsDay : 0;
        var progressBar = new Grid { VerticalAlignment = System.Windows.VerticalAlignment.Center, Height = 8 };
        
        // Track
        var track = new Border 
        { 
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(40, 40, 40)),
            CornerRadius = new CornerRadius(4) 
        };
        
        // Fill
        var fill = new Border
        {
            Background = GetColorForApp(stat.AppName),
            CornerRadius = new CornerRadius(4),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
            // Width is set via binding or manual calc in Grid logic, here we use simple layout
        };
        
        // Since we can't easily bind Width % in code-behind without a grid, let's use a ColumnDefinition trick or fixed width logic
        // Simpler: use the Grid column width we are in.
        // Let's assume max width is available. 
        // We will wrap the fill in a Grid with width * pct
        // Actually, easiest way in code-behind: 
        // Create a grid with 2 columns: [pct*] [rest*]
        
        var progressGrid = new Grid();
        progressGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(pct, GridUnitType.Star) });
        progressGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1 - pct, GridUnitType.Star) });
        
        progressGrid.Children.Add(fill); // Add fill to first col
        Grid.SetColumn(fill, 0);
        Grid.SetColumnSpan(fill, 1);
        
        // Wait, filling a star col with a border stretches it. Correct.

        progressBar.Children.Add(track);
        progressBar.Children.Add(progressGrid);


        Grid.SetColumn(nameTxt, 0);
        Grid.SetColumn(timeTxt, 1);
        Grid.SetColumn(progressBar, 2);

        grid.Children.Add(nameTxt);
        grid.Children.Add(timeTxt);
        grid.Children.Add(progressBar);

        return grid;
    }

    private System.Windows.Media.Brush GetColorForApp(string name)
    {
        // "Habicial" style colors
        var colors = new[] 
        { 
            "#00E096", // Green
            "#00A8FF", // Blue
            "#9C88FF", // Purple
            "#FBC531", // Yellow
            "#E84118"  // Red
        };
        var idx = Math.Abs(name.GetHashCode()) % colors.Length;
        return (System.Windows.Media.SolidColorBrush)new System.Windows.Media.BrushConverter().ConvertFrom(colors[idx])!;
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
        return $"{t.Seconds}s";
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        this.Hide();
    }
}
