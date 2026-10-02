using blazwiz.Core.Sync.Interfaces;
using Microsoft.JSInterop;

namespace blazwiz.Core.Sync.Opfs;

/// <summary>
/// <see cref="IOpfsClient"/> over the <c>wwwroot/js/opfs.js</c> module shipped with this library, imported the first
/// time it is needed.
/// </summary>
public sealed class OpfsClient(IJSRuntime jsRuntime)
    : IOpfsClient, IAsyncDisposable
{
    /// <summary>
    /// The path the module is imported from (Razor class library static assets).
    /// </summary>
    public const string ModulePath = "./_content/blazwiz.Core/js/opfs.js";

    private readonly IJSRuntime jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
    private Task<IJSObjectReference>? module;

    /// <inheritdoc/>
    public async Task<bool> IsSupportedAsync()
    {
        var js = await GetModuleAsync();
        return await js.InvokeAsync<bool>("isSupported");
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<OpfsEntry>> ListAsync(string root, string owner)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);

        var js = await GetModuleAsync();
        return await js.InvokeAsync<List<OpfsEntry>>("list", root, owner);
    }

    /// <inheritdoc/>
    public async Task<OpfsFile?> ReadAsync(string root, string owner, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrEmpty(key);

        var js = await GetModuleAsync();
        return await js.InvokeAsync<OpfsFile?>("read", root, owner, key);
    }

    /// <inheritdoc/>
    public async Task WriteAsync(string root, string owner, string key, byte[] content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(content);

        var js = await GetModuleAsync();
        await js.InvokeVoidAsync("write", root, owner, key, content);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string root, string owner, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrEmpty(key);

        var js = await GetModuleAsync();
        await js.InvokeVoidAsync("remove", root, owner, key);
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
