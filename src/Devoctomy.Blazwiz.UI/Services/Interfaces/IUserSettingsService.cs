using Devoctomy.Blazwiz.UI.Models;

namespace Devoctomy.Blazwiz.UI.Services.Interfaces;

/// <summary>
/// Loads and saves per user preferences.
/// </summary>
public interface IUserSettingsService
{
    /// <summary>
    /// Gets the current user settings.
    /// </summary>
    /// <returns>The user settings, or defaults if none have been saved.</returns>
    Task<UserSettings> GetSettingsAsync();

    /// <summary>
    /// Saves the user settings.
    /// </summary>
    /// <param name="settings">The settings to save.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task SaveSettingsAsync(UserSettings settings);
}
