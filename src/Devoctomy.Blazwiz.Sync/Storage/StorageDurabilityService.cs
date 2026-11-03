using Devoctomy.Blazwiz.Sync.Interfaces;
using Microsoft.JSInterop;

namespace Devoctomy.Blazwiz.Sync.Storage;

/// <summary>
/// <see cref="IStorageDurabilityService"/> over the <c>wwwroot/js/storage.js</c> module shipped with this library.
/// </summary>
public sealed class StorageDurabilityService(IJSRuntime jsRuntime)
    : IStorageDurabilityService
{
    /// <summary>
    /// The path the module is imported from (Razor class library static assets).
    /// </summary>
    public const string ModulePath = "./_content/Devoctomy.Blazwiz.Sync/js/storage.js";

    private readonly IJSRuntime jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
    private IJSObjectReference? module;

    /// <inheritdoc/>
    public async Task<StorageDurabilityStatus> GetStatusAsync()
    {
        var js = await GetModuleAsync();
        return await js.InvokeAsync<StorageDurabilityStatus>("getStatus");
    }

    /// <inheritdoc/>
    public async Task<bool> RequestPersistenceAsync()
    {
        var js = await GetModuleAsync();
        return await js.InvokeAsync<bool>("persist");
    }

    private async Task<IJSObjectReference> GetModuleAsync()
    {
        return module ??= await jsRuntime.InvokeAsync<IJSObjectReference>("import", ModulePath);
    }
}
