using blazwiz.Core.Greetings.Interfaces;
using blazwiz.Core.Time.Interfaces;

namespace blazwiz.Core.Greetings;

/// <inheritdoc/>
public class GreetingService(ISystemClock systemClock)
    : IGreetingService
{
    private readonly ISystemClock systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));

    /// <inheritdoc/>
    public string GetGreeting(string? name)
    {
        var prefix = GetGreetingPrefix(systemClock.Now.Hour);
        return string.IsNullOrWhiteSpace(name)
            ? prefix
            : $"{prefix}, {name.Trim()}";
    }

    /// <inheritdoc/>
    public string GetGreetingPrefix(int hour)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(hour);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(hour, 23);

        return hour switch
        {
            < 5 => "Good evening",
            < 12 => "Good morning",
            < 18 => "Good afternoon",
            _ => "Good evening",
        };
    }
}
