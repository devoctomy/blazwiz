using blazwiz.Core.Configuration.Interfaces;
using blazwiz.Core.Sync.FileTree;
using blazwiz.Core.Sync.Interfaces;
using blazwiz.Core.Sync.Models;
using Microsoft.JSInterop;

namespace blazwiz.Core.Sync.Folder;

/// <summary>
/// <see cref="IFolderAccessClient"/> over the <c>wwwroot/js/folder.js</c> module shipped with this library, imported
/// the first time it is needed.
/// </summary>
public sealed class FolderAccessClient(IJSRuntime jsRuntime, IAppStorageKeys storageKeys)
    : IFolderAccessClient, IAsyncDisposable
{
    /// <summary>
    /// The path the module is imported from (Razor class library static assets).
    /// </summary>
    public const string ModulePath = "./_content/blazwiz.Core/js/folder.js";

    private readonly IJSRuntime jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
    private readonly IAppStorageKeys storageKeys = storageKeys ?? throw new ArgumentNullException(nameof(storageKeys));
    private Task<IJSObjectReference>? module;

    /// <inheritdoc/>
    public async Task<bool> IsSupportedAsync()
    {
        var js = await GetModuleAsync();
        return await js.InvokeAsync<bool>("isSupported");
    }

    /// <inheritdoc/>
    public async Task<string?> PickAsync(string handleKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handleKey);

        var js = await GetModuleAsync();
        return await js.InvokeAsync<string?>("pick", handleKey);
    }

    /// <inheritdoc/>
    public async Task<SyncFolderStatus?> GetStatusAsync(string handleKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handleKey);

        var js = await GetModuleAsync();
        return await js.InvokeAsync<SyncFolderStatus?>("getStatus", handleKey);
    }

    /// <inheritdoc/>
    public async Task<bool> ReconnectAsync(string handleKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handleKey);

        var js = await GetModuleAsync();
        return await js.InvokeAsync<bool>("reconnect", handleKey);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<FileTreeEntry>> ListAsync(string handleKey, string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handleKey);
        ArgumentNullException.ThrowIfNull(prefix);

        var js = await GetModuleAsync();
        return await js.InvokeAsync<List<FileTreeEntry>>("list", handleKey, prefix);
    }

    /// <inheritdoc/>
    public async Task<FileTreeFile?> ReadAsync(string handleKey, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handleKey);
        ArgumentException.ThrowIfNullOrEmpty(key);

        var js = await GetModuleAsync();
        return await js.InvokeAsync<FileTreeFile?>("read", handleKey, key);
    }

    /// <inheritdoc/>
    public async Task<long> WriteAsync(string handleKey, string key, byte[] content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handleKey);
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(content);

        var js = await GetModuleAsync();
        return await js.InvokeAsync<long>("write", handleKey, key, content);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string handleKey, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handleKey);
        ArgumentException.ThrowIfNullOrEmpty(key);

        var js = await GetModuleAsync();
        await js.InvokeVoidAsync("remove", handleKey, key);
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

        return module ??= ImportAsync();
    }

    // The module keeps the folder handles in a database named after the app.
    private async Task<IJSObjectReference> ImportAsync()
    {
        var js = await jsRuntime.InvokeAsync<IJSObjectReference>("import", ModulePath);
        await js.InvokeVoidAsync("configure", storageKeys.Prefix);
        return js;
    }
}
