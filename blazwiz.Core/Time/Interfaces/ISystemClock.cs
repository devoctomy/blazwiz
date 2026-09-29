namespace blazwiz.Core.Time.Interfaces;

/// <summary>
/// Abstraction over the system clock so that time dependent logic can be unit tested.
/// </summary>
public interface ISystemClock
{
    /// <summary>
    /// Gets the current UTC date and time.
    /// </summary>
    DateTimeOffset UtcNow { get; }

    /// <summary>
    /// Gets the current local date and time.
    /// </summary>
    DateTimeOffset Now { get; }
}
