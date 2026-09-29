namespace blazwiz.Portal.Application.Services.Interfaces;

/// <summary>
/// Manages the light / dark theme for the application.
/// </summary>
public interface IThemeService
{
    /// <summary>
    /// Raised when the theme changes.
    /// </summary>
    event EventHandler? ThemeChanged;

    /// <summary>
    /// Gets the current theme name.
    /// </summary>
    string CurrentTheme { get; }

    /// <summary>
    /// Gets a value indicating whether the dark theme is active.
    /// </summary>
    bool IsDark { get; }

    /// <summary>
    /// Loads the saved theme (falling back to the operating system preference) and applies it. Safe to call multiple times.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task InitializeAsync();

    /// <summary>
    /// Applies and saves the given theme.
    /// </summary>
    /// <param name="theme">The theme name.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task SetThemeAsync(string theme);

    /// <summary>
    /// Switches between the light and dark themes.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task ToggleThemeAsync();
}
