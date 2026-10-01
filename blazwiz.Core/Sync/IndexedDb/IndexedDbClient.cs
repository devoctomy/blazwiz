using blazwiz.Core.Sync.Interfaces;
using Microsoft.JSInterop;

namespace blazwiz.Core.Sync.IndexedDb;

/// <summary>
/// <see cref="IIndexedDbClient"/> over the <c>wwwroot/js/indexeddb.js</c> module shipped with this library, imported
/// the first time it is needed.
/// </summary>
public sealed class IndexedDbClient(IJSRuntime jsRuntime)
    : IIndexedDbClient, IAsyncDisposable
{
    /// <summary>
    /// The path the module is imported from (Razor class library static assets).
    /// </summary>
    public const string ModulePath = "./_content/blazwiz.Core/js/indexeddb.js";

    private readonly IJSRuntime jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
    private Task<IJSObjectReference>? module;

    /// <inheritdoc/>
    public async Task<T?> GetAsync<T>(IndexedDbStore store, object key)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(key);

        var js = await GetModuleAsync();
        return await js.InvokeAsync<T?>("get", store, key);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<T>> GetAllByIndexAsync<T>(IndexedDbStore store, string indexName, object key, IReadOnlyList<string>? omitFields = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrWhiteSpace(indexName);
        ArgumentNullException.ThrowIfNull(key);

        var js = await GetModuleAsync();
        return await js.InvokeAsync<List<T>>("getAllByIndex", store, indexName, key, omitFields ?? []);
    }

    /// <inheritdoc/>
    public async Task PutAsync<T>(IndexedDbStore store, T value)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(value);

        var js = await GetModuleAsync();
        await js.InvokeVoidAsync("put", store, value);
    }

    /// <inheritdoc/>
    public async Task PutManyAsync<T>(IndexedDbStore store, IEnumerable<T> values)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(values);

        var js = await GetModuleAsync();
        await js.InvokeVoidAsync("putMany", store, values.ToArray());
    }

    /// <inheritdoc/>
    public async Task<T?> GetLastByIndexAsync<T>(IndexedDbStore store, string indexName, object lower, object upper)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrWhiteSpace(indexName);
        ArgumentNullException.ThrowIfNull(lower);
        ArgumentNullException.ThrowIfNull(upper);

        var js = await GetModuleAsync();
        return await js.InvokeAsync<T?>("getLastByIndex", store, indexName, lower, upper);
    }

    /// <inheritdoc/>
    public async Task DeleteManyAsync(IndexedDbStore store, IEnumerable<object> keys)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(keys);

        var js = await GetModuleAsync();
        await js.InvokeVoidAsync("deleteMany", store, keys.ToArray());
    }

    /// <inheritdoc/>
    public async Task<bool> StoreExistsAsync(string database, string storeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(database);
        ArgumentException.ThrowIfNullOrWhiteSpace(storeName);

        var js = await GetModuleAsync();
        return await js.InvokeAsync<bool>("storeExists", database, storeName);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<T>> GetAllFromExistingAsync<T>(string database, string storeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(database);
        ArgumentException.ThrowIfNullOrWhiteSpace(storeName);

        var js = await GetModuleAsync();
        return await js.InvokeAsync<List<T>>("getAllFromExisting", database, storeName);
    }

    /// <inheritdoc/>
    public async Task DeleteManyFromExistingAsync(string database, string storeName, IEnumerable<object> keys)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(database);
        ArgumentException.ThrowIfNullOrWhiteSpace(storeName);
        ArgumentNullException.ThrowIfNull(keys);

        var js = await GetModuleAsync();
        await js.InvokeVoidAsync("deleteManyFromExisting", database, storeName, keys.ToArray());
    }

    /// <inheritdoc/>
    public async Task DeleteStoresAsync(string database, IEnumerable<string> storeNames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(database);
        ArgumentNullException.ThrowIfNull(storeNames);

        var js = await GetModuleAsync();
        await js.InvokeVoidAsync("deleteStores", database, storeNames.ToArray());
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (module != null)
        {
            try
            {
                await (await module).DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The page is going away.
            }
        }
    }

    private Task<IJSObjectReference> GetModuleAsync()
    {
        if (module is { IsFaulted: true } or { IsCanceled: true })
        {
            // Try the import again next time rather than failing forever.
            module = null;
        }

        return module ??= jsRuntime.InvokeAsync<IJSObjectReference>("import", ModulePath).AsTask();
    }
}
