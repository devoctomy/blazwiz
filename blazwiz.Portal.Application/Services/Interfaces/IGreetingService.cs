namespace blazwiz.Portal.Application.Services.Interfaces;

/// <summary>
/// Produces friendly, time of day aware greetings for users.
/// </summary>
public interface IGreetingService
{
    /// <summary>
    /// Gets a greeting for the specified user based on the current local time.
    /// </summary>
    /// <param name="name">The name of the user to greet. When empty a generic greeting is returned.</param>
    /// <returns>The greeting, e.g. "Good morning, Bob".</returns>
    string GetGreeting(string? name);

    /// <summary>
    /// Gets the greeting prefix (e.g. "Good morning") for the specified hour of the day.
    /// </summary>
    /// <param name="hour">The hour of the day, 0 to 23.</param>
    /// <returns>The greeting prefix.</returns>
    string GetGreetingPrefix(int hour);
}
