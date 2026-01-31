using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

// Resolve ambiguities between WPF and Windows Forms
using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using MessageBox = System.Windows.MessageBox;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;

namespace ScreenTimeTracker.App;

/// <summary>
/// Two-step dialog for selecting focus duration and intent.
/// Step 1: Select focus duration and break time (with circular dial)
/// Step 2: Enter the one thing to focus on
/// </summary>
public partial class FocusDurationDialog : Window
{
    public int SelectedMinutes { get; private set; } = 25;
    public int BreakMinutes { get; private set; } = 5;
    public string FocusIntent { get; private set; } = string.Empty;
    public bool Confirmed { get; private set; } = false;

    private int _currentStep = 1;
    private bool _isDragging = false;
    
    // Dial settings
    private const double DialRadius = 90;
    private const double KnobRadius = 14;
    private const int MinBreakMinutes = 1;
    private const int MaxBreakMinutes = 30;

    public FocusDurationDialog()
    {
        InitializeComponent();
        
        // Allow dragging the window
        this.MouseLeftButtonDown += (s, e) => 
        {
            if (e.OriginalSource != DialCanvas && !_isDragging)
                this.DragMove();
        };
        
        // Initialize on load
        this.Loaded += (s, e) => 
        {
            UpdatePresetSelection(25);
            UpdateDialPosition(5);
        };
    }

    #region Step Navigation

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_currentStep == 1)
        {
            // Validate duration
            if (!int.TryParse(TxtCustomMinutes.Text, out int minutes) || minutes <= 0 || minutes > 180)
            {
                MessageBox.Show("Please enter a valid duration (1-180 minutes)", "Invalid Duration");
                return;
            }

            SelectedMinutes = minutes;
            BreakMinutes = int.Parse(TxtBreakDisplay.Text);
            
            // Update summary
            TxtSummaryFocus.Text = $"{SelectedMinutes} minutes";
            TxtSummaryBreak.Text = $"{BreakMinutes} minutes";
            
            // Switch to step 2
            _currentStep = 2;
            Step1Panel.Visibility = Visibility.Collapsed;
            Step2Panel.Visibility = Visibility.Visible;
            TxtTitle.Text = "One Thing";
            
            // Update button
            UpdateButtonText("Start Focus");
            
            // Focus the text input
            TxtFocusIntent.Focus();
        }
        else
        {
            // Start focus
            FocusIntent = TxtFocusIntent.Text?.Trim() ?? string.Empty;
            Confirmed = true;
            this.Close();
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        _currentStep = 1;
        Step2Panel.Visibility = Visibility.Collapsed;
        Step1Panel.Visibility = Visibility.Visible;
        TxtTitle.Text = "Focus Session";
        UpdateButtonText("Next");
    }

    private void UpdateButtonText(string text)
    {
        // Find the TextBlock inside the button template
        if (BtnNext.Template.FindName("btnText", BtnNext) is TextBlock textBlock)
        {
            textBlock.Text = text;
        }
        else
        {
            // Template might not be applied yet, defer
            BtnNext.ApplyTemplate();
            if (BtnNext.Template.FindName("btnText", BtnNext) is TextBlock tb)
            {
                tb.Text = text;
            }
        }
    }

    #endregion

    #region Preset Buttons

    private void Preset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tagValue)
        {
            if (int.TryParse(tagValue, out int minutes))
            {
                TxtCustomMinutes.Text = minutes.ToString();
                UpdatePresetSelection(minutes);
            }
        }
    }

    private void CustomDuration_TextChanged(object sender, TextChangedEventArgs e)
    {
        // Guard against early calls before Loaded event
        if (TxtBreakDisplay == null || Btn25 == null) return;
        
        if (int.TryParse(TxtCustomMinutes.Text, out int minutes))
        {
            // Auto-adjust break time based on focus duration
            int suggestedBreak = CalculateSuggestedBreak(minutes);
            UpdateDialPosition(suggestedBreak);
            
            // Update preset highlights
            if (minutes != 25 && minutes != 45 && minutes != 60)
            {
                ClearPresetHighlights();
            }
            else
            {
                UpdatePresetSelection(minutes);
            }
        }
    }

    private int CalculateSuggestedBreak(int focusMinutes)
    {
        // ~20% of focus time, clamped to valid range
        int breakTime = Math.Max(MinBreakMinutes, Math.Min(MaxBreakMinutes, focusMinutes / 5));
        return breakTime;
    }

    private void ClearPresetHighlights()
    {
        var bgBrush = (Brush)FindResource("BgBrush");
        var textBrush = (Brush)FindResource("TextSecondaryBrush");

        Btn25.Background = bgBrush; Btn25.Foreground = textBrush;
        Btn45.Background = bgBrush; Btn45.Foreground = textBrush;
        Btn60.Background = bgBrush; Btn60.Foreground = textBrush;
    }

    private void UpdatePresetSelection(int minutes)
    {
        ClearPresetHighlights();

        var accentBrush = (Brush)FindResource("AccentBrush");
        var whiteBrush = Brushes.White;
        
        switch (minutes)
        {
            case 25: Btn25.Background = accentBrush; Btn25.Foreground = whiteBrush; break;
            case 45: Btn45.Background = accentBrush; Btn45.Foreground = whiteBrush; break;
            case 60: Btn60.Background = accentBrush; Btn60.Foreground = whiteBrush; break;
        }
        
        // Update dial to suggested break time
        int suggestedBreak = CalculateSuggestedBreak(minutes);
        UpdateDialPosition(suggestedBreak);
    }

    #endregion

    #region Circular Dial Control

    private void Dial_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _isDragging = true;
        DialCanvas.CaptureMouse();
        UpdateDialFromMouse(e.GetPosition(DialCanvas));
    }

    private void Dial_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDragging)
        {
            UpdateDialFromMouse(e.GetPosition(DialCanvas));
        }
    }

    private void Dial_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        DialCanvas.ReleaseMouseCapture();
    }

    private void Dial_MouseLeave(object sender, MouseEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            DialCanvas.ReleaseMouseCapture();
        }
    }

    private void UpdateDialFromMouse(Point mousePos)
    {
        // Calculate angle from center
        double centerX = DialCanvas.Width / 2;
        double centerY = DialCanvas.Height / 2;
        
        double dx = mousePos.X - centerX;
        double dy = mousePos.Y - centerY;
        
        // Calculate angle (0 at top, clockwise)
        double angle = Math.Atan2(dx, -dy) * 180 / Math.PI;
        if (angle < 0) angle += 360;
        
        // Convert angle to minutes (0-360 degrees = MinBreak-MaxBreak minutes)
        int breakMinutes = (int)Math.Round(MinBreakMinutes + (angle / 360) * (MaxBreakMinutes - MinBreakMinutes));
        breakMinutes = Math.Max(MinBreakMinutes, Math.Min(MaxBreakMinutes, breakMinutes));
        
        UpdateDialPosition(breakMinutes);
    }

    private void UpdateDialPosition(int breakMinutes)
    {
        // Update display
        TxtBreakDisplay.Text = breakMinutes.ToString();
        
        // Calculate angle for this value
        double fraction = (double)(breakMinutes - MinBreakMinutes) / (MaxBreakMinutes - MinBreakMinutes);
        double angle = fraction * 360;
        double angleRad = (angle - 90) * Math.PI / 180; // -90 to start from top
        
        double centerX = DialCanvas.Width / 2;
        double centerY = DialCanvas.Height / 2;
        double knobDistance = DialRadius - 6; // Slightly inside the track
        
        // Position the knob
        double knobX = centerX + knobDistance * Math.Cos(angleRad) - KnobRadius;
        double knobY = centerY + knobDistance * Math.Sin(angleRad) - KnobRadius;
        
        Canvas.SetLeft(DialKnob, knobX);
        Canvas.SetTop(DialKnob, knobY);
        
        // Draw the arc
        DrawArc(angle);
    }

    private void DrawArc(double angleDegrees)
    {
        if (angleDegrees <= 0)
        {
            DialArc.Data = null;
            return;
        }

        double centerX = DialCanvas.Width / 2;
        double centerY = DialCanvas.Height / 2;
        double radius = DialRadius - 6;
        
        // Start at top (12 o'clock)
        double startAngle = -90;
        double endAngle = startAngle + angleDegrees;
        
        double startRad = startAngle * Math.PI / 180;
        double endRad = endAngle * Math.PI / 180;
        
        double startX = centerX + radius * Math.Cos(startRad);
        double startY = centerY + radius * Math.Sin(startRad);
        double endX = centerX + radius * Math.Cos(endRad);
        double endY = centerY + radius * Math.Sin(endRad);
        
        bool largeArc = angleDegrees > 180;
        
        var pathGeometry = new PathGeometry();
        var pathFigure = new PathFigure
        {
            StartPoint = new Point(startX, startY),
            IsClosed = false
        };
        
        var arcSegment = new ArcSegment
        {
            Point = new Point(endX, endY),
            Size = new Size(radius, radius),
            IsLargeArc = largeArc,
            SweepDirection = SweepDirection.Clockwise
        };
        
        pathFigure.Segments.Add(arcSegment);
        pathGeometry.Figures.Add(pathFigure);
        
        DialArc.Data = pathGeometry;
    }

    #endregion

    #region Window Controls

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }

    #endregion
}
