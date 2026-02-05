using System.Windows;
using ScreenTimeTracker.Storage;

namespace ScreenTimeTracker.App;

/// <summary>
/// Dialog for editing daily focus goals (Weekday/Weekend/Specific Day).
/// </summary>
public partial class GoalEditorDialog : Window
{
    private readonly UsageRepository _repository;
    private readonly DateOnly _targetDate;

    public GoalEditorDialog(UsageRepository repository, DateOnly targetDate)
    {
        InitializeComponent();
        _repository = repository;
        _targetDate = targetDate;

        // Set date context text
        TxtDateContext.Text = _targetDate.ToString("dddd, MMM d");

        // Load current goal for this date
        int currentGoal = _repository.GetDailyGoal(_targetDate);
        SliderGoal.Value = currentGoal;
        UpdateGoalDisplay(currentGoal);

        // Pre-select appropriate radio based on day type
        bool isWeekend = _targetDate.DayOfWeek == DayOfWeek.Saturday || _targetDate.DayOfWeek == DayOfWeek.Sunday;
        if (isWeekend)
            RbWeekends.IsChecked = true;
        else
            RbWeekdays.IsChecked = true;
    }

    private void SliderGoal_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateGoalDisplay((int)e.NewValue);
    }

    private void UpdateGoalDisplay(int minutes)
    {
        if (TxtGoalValue == null) return;
        int hours = minutes / 60;
        int mins = minutes % 60;
        TxtGoalValue.Text = $"{hours}h {mins:D2}m";
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        int goalMinutes = (int)SliderGoal.Value;

        if (RbTodayOnly.IsChecked == true)
        {
            // Save as daily override
            _repository.SetDailyGoalOverride(_targetDate, goalMinutes);
        }
        else if (RbWeekdays.IsChecked == true)
        {
            // Save as weekday default
            _repository.SetSetting("goal_weekday_minutes", goalMinutes.ToString());
        }
        else if (RbWeekends.IsChecked == true)
        {
            // Save as weekend default
            _repository.SetSetting("goal_weekend_minutes", goalMinutes.ToString());
        }

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
