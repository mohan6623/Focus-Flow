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
    /// Records a completed focus session and updates streak.
    /// </summary>
    public void RecordSessionCompletion()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);

        // Already completed a session today - no streak change
        if (_lastSessionDate == today)
        {
            Logger.Debug("Session completed, but streak already recorded for today.");
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
}
