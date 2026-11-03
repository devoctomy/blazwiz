using Devoctomy.Blazwiz.Sync.Interfaces;
using Microsoft.JSInterop;

namespace Devoctomy.Blazwiz.Sync.Connectivity;

/// <summary>
/// <see cref="IBrowserNetworkStatus"/> over the <c>wwwroot/js/network.js</c> module shipped with this library.
/// </summary>
public sealed class BrowserNetworkStatus(IJSRuntime jsRuntime)
    : IBrowserNetworkStatus, IAsyncDisposable
{
    /// <summary>
    /// The path the module is imported from (Razor class library static assets).
    /// </summary>
    public const string ModulePath = "./_content/Devoctomy.Blazwiz.Sync/js/network.js";

    private readonly IJSRuntime jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
    private IJSObjectReference? module;
    private DotNetObjectReference<BrowserNetworkStatus>? selfReference;

    /// <inheritdoc/>
    public event EventHandler<bool>? OnlineChanged;

    /// <inheritdoc/>
    public event EventHandler? BecameVisible;

    /// <inheritdoc/>
    public async Task<bool> IsOnlineAsync()
    {
        var js = await GetModuleAsync();
        return await js.InvokeAsync<bool>("isOnline");
    }

    /// <inheritdoc/>
    public async Task StartAsync()
    {
        if (selfReference != null)
        {
            return;
        }

        var js = await GetModuleAsync();
        selfReference = DotNetObjectReference.Create(this);
        await js.InvokeVoidAsync("subscribe", selfReference);
    }

    /// <inheritdoc/>
    public async Task StopAsync()
    {
        if (selfReference == null)
        {
            return;
        }

        var js = await GetModuleAsync();
        await js.InvokeVoidAsync("unsubscribe");
        selfReference.Dispose();
        selfReference = null;
    }

    /// <summary>
    /// Called from <c>network.js</c> when the browser goes online or offline.
    /// </summary>
    /// <param name="isOnline">True if the browser is now online.</param>
    [JSInvokable]
    public void OnOnlineChanged(bool isOnline)
    {
        OnlineChanged?.Invoke(this, isOnline);
    }

    /// <summary>
    /// Called from <c>network.js</c> when the page becomes visible again.
    /// </summary>
    [JSInvokable]
    public void OnBecameVisible()
    {
        BecameVisible?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await StopAsync();
            if (module != null)
            {
                await module.DisposeAsync();
            }
        }
        catch (JSDisconnectedException)
        {
            // The page is going away.
        }

        selfReference?.Dispose();
    }

    private async Task<IJSObjectReference> GetModuleAsync()
    {
        return module ??= await jsRuntime.InvokeAsync<IJSObjectReference>("import", ModulePath);
    }
}
