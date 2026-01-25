using System.Windows;

namespace ScreenTimeTracker.App;

/// <summary>
/// Dialog for selecting focus duration before starting timer.
/// </summary>
public partial class FocusDurationDialog : Window
{
    public int SelectedMinutes { get; private set; } = 25;
    public bool Confirmed { get; private set; } = false;

    public FocusDurationDialog()
    {
        InitializeComponent();
        
        // Allow dragging
        this.MouseLeftButtonDown += (s, e) => this.DragMove();
        
        // Select default
        UpdatePresetSelection(25);
    }

    private void Preset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string tagValue)
        {
            if (int.TryParse(tagValue, out int minutes))
            {
                TxtCustomMinutes.Text = minutes.ToString();
                UpdatePresetSelection(minutes);
            }
        }
    }

    private void UpdatePresetSelection(int minutes)
    {
        // Reset all buttons
        Btn25.Background = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(51, 51, 51));
        Btn45.Background = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(51, 51, 51));
        Btn60.Background = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(51, 51, 51));

        // Highlight selected
        var accentBrush = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0, 224, 150));
        
        switch (minutes)
        {
            case 25: Btn25.Background = accentBrush; Btn25.Foreground = System.Windows.Media.Brushes.Black; break;
            case 45: Btn45.Background = accentBrush; Btn45.Foreground = System.Windows.Media.Brushes.Black; break;
            case 60: Btn60.Background = accentBrush; Btn60.Foreground = System.Windows.Media.Brushes.Black; break;
        }
    }

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(TxtCustomMinutes.Text, out int minutes) && minutes > 0 && minutes <= 180)
        {
            SelectedMinutes = minutes;
            Confirmed = true;
            this.Close();
        }
        else
        {
            System.Windows.MessageBox.Show("Please enter a valid duration (1-180 minutes)", "Invalid Duration");
        }
    }
}
