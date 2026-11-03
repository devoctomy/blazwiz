using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.UI.Models;
using Devoctomy.Blazwiz.UI.Services.Interfaces;

namespace Devoctomy.Blazwiz.UI.Services;

/// <inheritdoc/>
public class UserSettingsService(ILocalStorageService localStorageService)
    : IUserSettingsService
{
    /// <summary>
    /// The local storage key user settings are stored under.
    /// </summary>
    public const string StorageKey = "user_settings";

    private readonly ILocalStorageService localStorageService = localStorageService ?? throw new ArgumentNullException(nameof(localStorageService));
    private UserSettings? cachedSettings;

    /// <inheritdoc/>
    public async Task<UserSettings> GetSettingsAsync()
    {
        cachedSettings ??= await localStorageService.GetObjectAsync<UserSettings>(StorageKey) ?? new UserSettings();
        return cachedSettings;
    }

    /// <inheritdoc/>
    public async Task SaveSettingsAsync(UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await localStorageService.SetObjectAsync(StorageKey, settings);
        cachedSettings = settings;
    }
}
