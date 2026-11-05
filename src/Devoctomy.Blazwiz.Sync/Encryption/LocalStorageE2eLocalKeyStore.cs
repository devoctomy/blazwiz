using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Interfaces;

namespace Devoctomy.Blazwiz.Sync.Encryption;

/// <summary>
/// Keeps this device's end to end encryption keys (the master key wrapped by the passkey, and a copy of the keys
/// document) in local storage, one entry per user and key scope. The wrapped master key is useless without the passkey.
/// </summary>
public class LocalStorageE2eLocalKeyStore(ILocalStorageService localStorageService, ISyncUserContext userContext)
    : IE2eLocalKeyStore
{
    /// <summary>
    /// The local storage key prefix, followed by the key scope id, <c>_</c> and the user id.
    /// </summary>
    public const string StorageKeyPrefix = "e2e_keys_";

    private readonly ILocalStorageService localStorageService = localStorageService ?? throw new ArgumentNullException(nameof(localStorageService));
    private readonly ISyncUserContext userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));

    /// <inheritdoc/>
    public async Task<E2eLocalKeys?> GetAsync(E2eKeyScope scope)
    {
        return await localStorageService.GetObjectAsync<E2eLocalKeys>(await GetStorageKeyAsync(scope));
    }

    /// <inheritdoc/>
    public async Task SaveAsync(E2eKeyScope scope, E2eLocalKeys keys)
    {
        ArgumentNullException.ThrowIfNull(keys);

        await localStorageService.SetObjectAsync(await GetStorageKeyAsync(scope), keys);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(E2eKeyScope scope)
    {
        await localStorageService.RemoveItemAsync(await GetStorageKeyAsync(scope));
    }

    private async Task<string> GetStorageKeyAsync(E2eKeyScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var owner = await userContext.GetUserIdAsync()
            ?? throw new InvalidOperationException("Sign in to use end to end encryption.");
        return $"{StorageKeyPrefix}{scope.Id}_{owner}";
    }
}
