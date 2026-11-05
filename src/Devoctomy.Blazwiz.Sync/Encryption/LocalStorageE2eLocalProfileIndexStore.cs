using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Sync.Interfaces;

namespace Devoctomy.Blazwiz.Sync.Encryption;

/// <summary>
/// Keeps this device's copy of the signed in user's index of protected profiles in local storage, one entry per user.
/// It holds only the profiles' ids.
/// </summary>
/// <param name="localStorageService">The local storage.</param>
/// <param name="userContext">The signed in user.</param>
public class LocalStorageE2eLocalProfileIndexStore(ILocalStorageService localStorageService, ISyncUserContext userContext)
    : IE2eLocalProfileIndexStore
{
    /// <summary>
    /// The local storage key prefix, followed by the user id.
    /// </summary>
    public const string StorageKeyPrefix = "e2e_index_";

    private readonly ILocalStorageService localStorageService = localStorageService ?? throw new ArgumentNullException(nameof(localStorageService));
    private readonly ISyncUserContext userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));

    /// <inheritdoc/>
    public async Task<string?> GetAsync()
    {
        return await localStorageService.GetItemAsync(await GetStorageKeyAsync());
    }

    /// <inheritdoc/>
    public async Task SaveAsync(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        await localStorageService.SetItemAsync(await GetStorageKeyAsync(), value);
    }

    private async Task<string> GetStorageKeyAsync()
    {
        var owner = await userContext.GetUserIdAsync()
            ?? throw new InvalidOperationException("Sign in to use protected profiles.");
        return StorageKeyPrefix + owner;
    }
}
