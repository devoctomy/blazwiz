using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Interfaces;

namespace Devoctomy.Blazwiz.Sync.Encryption;

/// <summary>
/// Keeps this device's end to end encryption keys (the master key wrapped by the passkey, and a copy of keys.json) in
/// local storage, one entry per user. The wrapped master key is useless without the passkey.
/// </summary>
public class LocalStorageE2eLocalKeyStore(ILocalStorageService localStorageService, ISyncUserContext userContext)
    : IE2eLocalKeyStore
{
    /// <summary>
    /// The local storage key prefix, followed by the user id.
    /// </summary>
    public const string StorageKeyPrefix = "e2e_keys_";

    private readonly ILocalStorageService localStorageService = localStorageService ?? throw new ArgumentNullException(nameof(localStorageService));
    private readonly ISyncUserContext userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));

    /// <inheritdoc/>
    public async Task<E2eLocalKeys?> GetAsync()
    {
        return await localStorageService.GetObjectAsync<E2eLocalKeys>(await GetStorageKeyAsync());
    }

    /// <inheritdoc/>
    public async Task SaveAsync(E2eLocalKeys keys)
    {
        ArgumentNullException.ThrowIfNull(keys);

        await localStorageService.SetObjectAsync(await GetStorageKeyAsync(), keys);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync()
    {
        await localStorageService.RemoveItemAsync(await GetStorageKeyAsync());
    }

    private async Task<string> GetStorageKeyAsync()
    {
        var owner = await userContext.GetUserIdAsync()
            ?? throw new InvalidOperationException("Sign in to use end to end encryption.");
        return StorageKeyPrefix + owner;
    }
}
