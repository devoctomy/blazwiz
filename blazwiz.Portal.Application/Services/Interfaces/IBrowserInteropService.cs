namespace blazwiz.Portal.Application.Services.Interfaces;

/// <summary>
/// Abstraction over browser specific functionality implemented in JavaScript.
/// </summary>
public interface IBrowserInteropService
{
    /// <summary>
    /// Applies the given theme to the document.
    /// </summary>
    /// <param name="theme">The theme name.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task SetThemeAsync(string theme);

    /// <summary>
    /// Gets the operating system preferred theme (<c>light</c> or <c>dark</c>).
    /// </summary>
    /// <returns>The preferred theme name.</returns>
    Task<string> GetPreferredThemeAsync();
}
