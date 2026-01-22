namespace ScreenTimeTracker.Utilities;

/// <summary>
/// Abstraction over DateTime for testability.
/// </summary>
public interface ITimeProvider
{
    DateTime Now { get; }
    DateOnly Today { get; }
}

/// <summary>
/// Default time provider using system time.
/// </summary>
public class SystemTimeProvider : ITimeProvider
{
    public DateTime Now => DateTime.Now;
    public DateOnly Today => DateOnly.FromDateTime(DateTime.Now);
}

/// <summary>
/// Test time provider with controllable time.
/// </summary>
public class TestTimeProvider : ITimeProvider
{
    private DateTime _currentTime;

    public TestTimeProvider(DateTime? initialTime = null)
    {
        _currentTime = initialTime ?? DateTime.Now;
    }

    public DateTime Now => _currentTime;
    public DateOnly Today => DateOnly.FromDateTime(_currentTime);

    /// <summary>
    /// Sets the current time.
    /// </summary>
    public void SetTime(DateTime time)
    {
        _currentTime = time;
    }

    /// <summary>
    /// Advances time by the specified duration.
    /// </summary>
    public void Advance(TimeSpan duration)
    {
        _currentTime = _currentTime.Add(duration);
    }
}
