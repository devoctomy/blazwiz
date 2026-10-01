using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Models.Settings;

namespace blazwiz.Portal.Application.Services;

/// <inheritdoc/>
public class UserSettingsService(ILocalStorageService localStorageService)
    : IUserSettingsService
{
    /// <summary>
    /// The local storage key user settings are stored under.
    /// </summary>
    public const string StorageKey = "blazwiz_user_settings";

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
