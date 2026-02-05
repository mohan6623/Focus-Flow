using ScreenTimeTracker.Storage;
using ScreenTimeTracker.Utilities;

namespace ScreenTimeTracker.Services;

/// <summary>
/// Manages focus streak tracking - consecutive days of completing focus sessions.
/// </summary>
public class StreakService
{
    private readonly UsageRepository _repository;
    
    private int _currentStreak;
    private int _longestStreak;
    private DateOnly? _lastSessionDate;

    /// <summary>
    /// Current consecutive day streak.
    /// </summary>
    public int CurrentStreak => _currentStreak;

    /// <summary>
    /// All-time longest streak.
    /// </summary>
    public int LongestStreak => _longestStreak;

    /// <summary>
    /// The date of the last completed session.
    /// </summary>
    public DateOnly? LastSessionDate => _lastSessionDate;

    /// <summary>
    /// Whether the user has completed a session today.
    /// </summary>
    public bool HasCompletedToday => _lastSessionDate == DateOnly.FromDateTime(DateTime.Now);

    /// <summary>
    /// True if streak will break tomorrow without a session.
    /// </summary>
    public bool IsAtRisk => !HasCompletedToday && _currentStreak > 0;

    /// <summary>
    /// Event fired when streak data changes.
    /// </summary>
    public event EventHandler? StreakUpdated;

    public StreakService(UsageRepository repository)
    {
        _repository = repository;
        LoadStreakData();
    }

    private void LoadStreakData()
    {
        var (current, longest, lastDate) = _repository.GetStreakData();
        _currentStreak = current;
        _longestStreak = longest;
        _lastSessionDate = lastDate;

        // Check if streak should be reset (missed a day)
        CheckStreakExpiry();
        
        Logger.Info($"Streak loaded: {_currentStreak} days (best: {_longestStreak})");
    }

    private void CheckStreakExpiry()
    {
        if (_lastSessionDate == null || _currentStreak == 0)
            return;

        var today = DateOnly.FromDateTime(DateTime.Now);
        var daysSinceLastSession = today.DayNumber - _lastSessionDate.Value.DayNumber;

        // If more than 1 day has passed, reset the streak
        if (daysSinceLastSession > 1)
        {
            Logger.Info($"Streak expired! Last session was {daysSinceLastSession} days ago.");
            _currentStreak = 0;
            SaveStreakData();
        }
    }

    /// <summary>
    /// Gets the daily focus goal in minutes for the specified date.
    /// Checks override -> weekday/weekend default -> fallback 120.
    /// </summary>
    public int GetDailyGoalMinutes(DateOnly date)
    {
        return _repository.GetDailyGoal(date);
    }

    /// <summary>
    /// Gets today's daily focus goal in minutes.
    /// </summary>
    public int DailyGoalMinutes => GetDailyGoalMinutes(DateOnly.FromDateTime(DateTime.Now));

    /// <summary>
    /// Records a completed focus session and updates streak.
    /// </summary>
    public void RecordSessionCompletion(double durationSeconds)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);

        // Always record in history table (allows multiple sessions per day tracking)
        _repository.RecordFocusSessionDate(today, durationSeconds);

        // Already completed a session today - no streak change needed
        if (_lastSessionDate == today)
        {
            Logger.Debug("Session completed, but streak already recorded for today.");
            StreakUpdated?.Invoke(this, EventArgs.Empty); // Still notify UI to refresh
            return;
        }

        // Check if this continues the streak or starts fresh
        if (_lastSessionDate != null)
        {
            var daysSinceLastSession = today.DayNumber - _lastSessionDate.Value.DayNumber;
            
            if (daysSinceLastSession == 1)
            {
                // Consecutive day - increment streak
                _currentStreak++;
                Logger.Info($"Streak continued! Now at {_currentStreak} days.");
            }
            else if (daysSinceLastSession > 1)
            {
                // Missed days - start new streak
                _currentStreak = 1;
                Logger.Info("New streak started after gap.");
            }
            // daysSinceLastSession == 0 is handled above (same day)
        }
        else
        {
            // First ever session
            _currentStreak = 1;
            Logger.Info("First streak started!");
        }

        // Update longest streak if needed
        if (_currentStreak > _longestStreak)
        {
            _longestStreak = _currentStreak;
            Logger.Info($"New record streak: {_longestStreak} days!");
        }

        _lastSessionDate = today;
        SaveStreakData();
        
        StreakUpdated?.Invoke(this, EventArgs.Empty);
    }

    private void SaveStreakData()
    {
        _repository.SaveStreakData(_currentStreak, _longestStreak, _lastSessionDate);
    }

    /// <summary>
    /// Gets a message describing the current streak status.
    /// </summary>
    public string GetStreakMessage()
    {
        if (_currentStreak == 0)
        {
            return "Complete a focus session to start your streak!";
        }

        if (HasCompletedToday)
        {
            if (_currentStreak == _longestStreak && _currentStreak > 1)
            {
                return $"🏆 New record! Keep it going!";
            }
            return $"Great job! See you tomorrow!";
        }

        // At risk - hasn't completed today
        return $"⚠️ Don't lose your {_currentStreak}-day streak!";
    }

    /// <summary>
    /// Gets a list of dates in the current week (Mon-Sun) that have completed sessions.
    /// </summary>
    public List<DateOnly> GetWeekHistory()
    {
        // Get the last 14 days of history (covers any week scenario)
        var history = _repository.GetFocusSessionHistory(14);
        return history;
    }

    /// <summary>
    /// Checks if a specific date has a completed focus session.
    /// </summary>
    public bool HasSessionOnDate(DateOnly date)
    {
        var history = _repository.GetFocusSessionHistory(14);
        return history.Contains(date);
    }

    /// <summary>
    /// Gets the total focus duration (in seconds) for a specific date.
    /// </summary>
    public double GetFocusTimeForDate(DateOnly date)
    {
        return _repository.GetDailyFocusTime(date);
    }

    /// <summary>
    /// Gets focus history for a specific month with daily stats.
    /// Returns list of (date, focusSeconds, goalMinutes).
    /// </summary>
    public List<(DateOnly Date, double FocusSeconds, int GoalMinutes)> GetMonthHistory(int year, int month)
    {
        return _repository.GetFocusHistoryForMonth(year, month);
    }

    /// <summary>
    /// Gets total focus time (in seconds) for a specific month.
    /// </summary>
    public double GetMonthlyFocusSeconds(int year, int month)
    {
        return _repository.GetMonthlyFocusTotal(year, month);
    }

    /// <summary>
    /// Gets the date when the longest streak was achieved.
    /// </summary>
    public DateOnly? GetLongestStreakDate()
    {
        // For now, return null as we don't track this yet
        // Could be enhanced to store in DB when longest streak is updated
        return _longestStreak > 0 ? _lastSessionDate : null;
    }
}
