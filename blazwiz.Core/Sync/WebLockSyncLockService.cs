using blazwiz.Core.Configuration.Interfaces;
using blazwiz.Core.Sync.Connectivity;
using blazwiz.Core.Sync.Interfaces;
using Microsoft.JSInterop;

namespace blazwiz.Core.Sync;

/// <summary>
/// Cross tab locks using the browser's Web Locks API (<c>navigator.locks</c>) through <c>wwwroot/js/network.js</c>.
/// The browser releases a lock if its tab closes mid-sync. A cancelled wait is cancelled in the browser too, so a lock
/// granted after giving up is released straight away rather than held until the tab closes.
/// </summary>
public sealed class WebLockSyncLockService(IJSRuntime jsRuntime, IAppStorageKeys storageKeys)
    : ISyncLockService
{
    private readonly IJSRuntime jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
    private readonly IAppStorageKeys storageKeys = storageKeys ?? throw new ArgumentNullException(nameof(storageKeys));
    private IJSObjectReference? module;

    /// <inheritdoc/>
    public async Task<IAsyncDisposable> AcquireAsync(string name, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var js = module ??= await jsRuntime.InvokeAsync<IJSObjectReference>("import", ct, BrowserNetworkStatus.ModulePath);

        // Unique across every instance, as the module (and its locks) is shared by the whole page.
        var requestId = Guid.NewGuid().ToString("N");
        string id;
        try
        {
            id = await js.InvokeAsync<string>("acquireLock", ct, storageKeys.For(name), requestId);
        }
        catch (OperationCanceledException)
        {
            await CancelAsync(js, requestId);
            throw;
        }

        return new Releaser(js, id);
    }

    private static async Task CancelAsync(IJSObjectReference js, string requestId)
    {
        try
        {
            await js.InvokeVoidAsync("cancelLock", requestId);
        }
        catch (JSDisconnectedException)
        {
            // The page is going away, and the browser drops the request with it.
        }
    }

    private sealed class Releaser(IJSObjectReference js, string id)
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
