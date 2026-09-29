using blazwiz.Core.Time.Interfaces;

namespace blazwiz.Core.Time;

/// <inheritdoc/>
public class SystemClock : ISystemClock
{
    /// <inheritdoc/>
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    /// <inheritdoc/>
    public DateTimeOffset Now => DateTimeOffset.Now;
}
