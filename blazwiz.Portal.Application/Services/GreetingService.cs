using blazwiz.Portal.Application.Services.Interfaces;

namespace blazwiz.Portal.Application.Services;

/// <inheritdoc/>
public class GreetingService(TimeProvider timeProvider)
    : IGreetingService
{
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc/>
    public string GetGreeting(string? name)
    {
        var prefix = GetGreetingPrefix(timeProvider.GetLocalNow().Hour);
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
