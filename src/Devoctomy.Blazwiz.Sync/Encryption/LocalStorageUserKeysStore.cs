using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Interfaces;

namespace Devoctomy.Blazwiz.Sync.Encryption;

/// <summary>
/// <see cref="IUserKeysStore"/> and <see cref="IE2eProfileIndexStore"/> in the browser's local storage, for local
/// development without Cognito (no Secrets Manager to keep them in), so profiles are still encrypted. Partitioned by
/// user. The keys documents are only as safe as their passphrases, as in Secrets Manager.
/// </summary>
/// <param name="localStorageService">The local storage.</param>
/// <param name="userContext">The signed in user.</param>
public class LocalStorageUserKeysStore(ILocalStorageService localStorageService, ISyncUserContext userContext)
    : IUserKeysStore, IE2eProfileIndexStore
{
    /// <summary>
    /// The local storage key prefix of a profile's keys document, followed by its scope id, <c>_</c> and the user id.
    /// </summary>
    public const string KeysPrefix = "e2e_browser_keys_";

    /// <summary>
    /// The local storage key prefix of the index, followed by the user id.
    /// </summary>
    public const string IndexPrefix = "e2e_browser_index_";

    private readonly ILocalStorageService localStorageService = localStorageService ?? throw new ArgumentNullException(nameof(localStorageService));
    private readonly ISyncUserContext userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));

    /// <inheritdoc/>
    public async Task<string?> GetAsync(E2eKeyScope scope, CancellationToken ct = default)
    {
        return await localStorageService.GetItemAsync(await KeysKeyAsync(scope));
    }

    /// <inheritdoc/>
    public async Task SetAsync(E2eKeyScope scope, string value, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(value);

        await localStorageService.SetItemAsync(await KeysKeyAsync(scope), value);
    }

    /// <inheritdoc/>
    public async Task<bool> TryCreateAsync(E2eKeyScope scope, string value, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(value);

        var key = await KeysKeyAsync(scope);
        if (await localStorageService.GetItemAsync(key) != null)
        {
            return false;
        }

        await localStorageService.SetItemAsync(key, value);
        return true;
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(E2eKeyScope scope, CancellationToken ct = default)
    {
        await localStorageService.RemoveItemAsync(await KeysKeyAsync(scope));
    }

    /// <inheritdoc/>
    public async Task<string?> GetIndexAsync(CancellationToken ct = default)
    {
        return await localStorageService.GetItemAsync(IndexPrefix + await OwnerAsync());
    }

    /// <inheritdoc/>
    public async Task SetIndexAsync(string value, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(value);

        await localStorageService.SetItemAsync(IndexPrefix + await OwnerAsync(), value);
    }

    private async Task<string> KeysKeyAsync(E2eKeyScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        return $"{KeysPrefix}{scope.Id}_{await OwnerAsync()}";
    }

    private async Task<string> OwnerAsync()
    {
        return await userContext.GetUserIdAsync()
            ?? throw new InvalidOperationException("Sign in to use your keys.");
    }
}
