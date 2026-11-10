using System.Globalization;
using System.Text.Json;
using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Encryption.Exceptions;
using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Interfaces;

namespace Devoctomy.Blazwiz.Sync.Encryption;

/// <summary>
/// <see cref="IUserKeysStore"/> and <see cref="IE2eProfileIndexStore"/> in the browser's local storage, for local
/// development without a data bucket, so profiles are still encrypted. Partitioned by user. Each value is kept with a
/// counter as its version, so writes are conditional as they are in the data bucket. The keys documents are only as
/// safe as their passphrases, as in the data bucket.
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
    public async Task<StoredDocument?> GetAsync(E2eKeyScope scope, CancellationToken ct = default)
    {
        return await ReadAsync(await KeysKeyAsync(scope));
    }

    /// <inheritdoc/>
    public async Task<string> SetAsync(E2eKeyScope scope, string value, string ifMatch, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentException.ThrowIfNullOrEmpty(ifMatch);

        return await WriteAsync(await KeysKeyAsync(scope), value, ifMatch);
    }

    /// <inheritdoc/>
    public async Task<string?> TryCreateAsync(E2eKeyScope scope, string value, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(value);

        try
        {
            return await WriteAsync(await KeysKeyAsync(scope), value, null);
        }
        catch (E2eDocumentChangedException)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(E2eKeyScope scope, CancellationToken ct = default)
    {
        await localStorageService.RemoveItemAsync(await KeysKeyAsync(scope));
    }

    /// <inheritdoc/>
    public async Task<StoredDocument?> GetIndexAsync(CancellationToken ct = default)
    {
        return await ReadAsync(IndexPrefix + await OwnerAsync());
    }

    /// <inheritdoc/>
    public async Task<string> SetIndexAsync(string value, string? ifMatch, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(value);

        return await WriteAsync(IndexPrefix + await OwnerAsync(), value, ifMatch);
    }

    private static Entry? Parse(string? json)
    {
        if (json == null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<Entry>(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("A keys document kept in local storage is not valid.", ex);
        }
    }

    private async Task<StoredDocument?> ReadAsync(string key)
    {
        var entry = Parse(await localStorageService.GetItemAsync(key));
        return entry?.Value == null ? null : new StoredDocument(entry.Value, entry.Version.ToString(CultureInfo.InvariantCulture));
    }

    // The check and the write are two calls, so two tabs writing at the same instant can still both succeed. This store
    // is for local development, where that does not matter; the data bucket's conditional writes are atomic.
    private async Task<string> WriteAsync(string key, string value, string? ifMatch)
    {
        var current = Parse(await localStorageService.GetItemAsync(key));
        var currentETag = current?.Version.ToString(CultureInfo.InvariantCulture);
        if (!string.Equals(currentETag, ifMatch, StringComparison.Ordinal))
        {
            throw new E2eDocumentChangedException();
        }

        var next = new Entry { Version = (current?.Version ?? 0) + 1, Value = value };
        await localStorageService.SetItemAsync(key, JsonSerializer.Serialize(next));
        return next.Version.ToString(CultureInfo.InvariantCulture);
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

    private sealed class Entry
    {
        public long Version { get; set; }

        public string? Value { get; set; }
    }
}
