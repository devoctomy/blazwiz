using blazwiz.Core.Sync.Connectivity;
using blazwiz.Core.Sync.Interfaces;
using Microsoft.JSInterop;

namespace blazwiz.Core.Sync;

/// <summary>
/// Cross tab locks using the browser's Web Locks API (<c>navigator.locks</c>) through <c>wwwroot/js/network.js</c>.
/// The browser releases a lock if its tab closes mid-sync.
/// </summary>
public sealed class WebLockSyncLockService(IJSRuntime jsRuntime)
    : ISyncLockService
{
    private readonly IJSRuntime jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
    private IJSObjectReference? module;

    /// <inheritdoc/>
    public async Task<IAsyncDisposable> AcquireAsync(string name, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var js = module ??= await jsRuntime.InvokeAsync<IJSObjectReference>("import", ct, BrowserNetworkStatus.ModulePath);
        var id = await js.InvokeAsync<int>("acquireLock", ct, name);
        return new Releaser(js, id);
    }

    private sealed class Releaser(IJSObjectReference js, int id)
        : IAsyncDisposable
    {
        private bool released;

        public async ValueTask DisposeAsync()
        {
            if (released)
            {
                return;
            }

            released = true;
            try
            {
                await js.InvokeVoidAsync("releaseLock", id);
            }
            catch (JSDisconnectedException)
            {
                // The page is going away, and the browser releases the lock with it.
            }
        }
    }
}
