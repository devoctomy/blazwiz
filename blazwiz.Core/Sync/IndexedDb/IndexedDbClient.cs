using blazwiz.Core.Configuration.Interfaces;
using blazwiz.Core.Sync.Interfaces;
using Microsoft.JSInterop;

namespace blazwiz.Core.Sync.IndexedDb;

/// <summary>
/// <see cref="IIndexedDbClient"/> over the <c>wwwroot/js/indexeddb.js</c> module shipped with this library, imported
/// the first time it is needed.
/// </summary>
public sealed class IndexedDbClient(IJSRuntime jsRuntime, IAppStorageKeys storageKeys)
    : IIndexedDbClient, IAsyncDisposable
{
    /// <summary>
    /// The path the module is imported from (Razor class library static assets).
    /// </summary>
    public const string ModulePath = "./_content/blazwiz.Core/js/indexeddb.js";

    private readonly IJSRuntime jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
    private readonly IAppStorageKeys storageKeys = storageKeys ?? throw new ArgumentNullException(nameof(storageKeys));
    private Task<IJSObjectReference>? module;

    /// <inheritdoc/>
    public async Task<T?> GetAsync<T>(IndexedDbStore store, object key)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(key);

        var js = await GetModuleAsync();
        return await js.InvokeAsync<T?>("get", Scope(store), key);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<T>> GetAllByIndexAsync<T>(IndexedDbStore store, string indexName, object key, IReadOnlyList<string>? omitFields = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrWhiteSpace(indexName);
        ArgumentNullException.ThrowIfNull(key);

        var js = await GetModuleAsync();
        return await js.InvokeAsync<List<T>>("getAllByIndex", Scope(store), indexName, key, omitFields ?? []);
    }

    /// <inheritdoc/>
    public async Task PutAsync<T>(IndexedDbStore store, T value)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(value);

        var js = await GetModuleAsync();
        await js.InvokeVoidAsync("put", Scope(store), value);
    }

    /// <inheritdoc/>
    public async Task PutManyAsync<T>(IndexedDbStore store, IEnumerable<T> values)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(values);

        var js = await GetModuleAsync();
        await js.InvokeVoidAsync("putMany", Scope(store), values.ToArray());
    }

    /// <inheritdoc/>
    public async Task<T?> GetLastByIndexAsync<T>(IndexedDbStore store, string indexName, object lower, object upper)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrWhiteSpace(indexName);
        ArgumentNullException.ThrowIfNull(lower);
        ArgumentNullException.ThrowIfNull(upper);

        var js = await GetModuleAsync();
        return await js.InvokeAsync<T?>("getLastByIndex", Scope(store), indexName, lower, upper);
    }

    /// <inheritdoc/>
    public async Task DeleteManyAsync(IndexedDbStore store, IEnumerable<object> keys)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(keys);

        var js = await GetModuleAsync();
        await js.InvokeVoidAsync("deleteMany", Scope(store), keys.ToArray());
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

    // The database is named after the app, so apps on the same origin keep their data apart.
    private IndexedDbStore Scope(IndexedDbStore store) =>
        new IndexedDbStore(storageKeys.For(store.Database), store.Name, store.KeyPath, store.Indexes);

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
