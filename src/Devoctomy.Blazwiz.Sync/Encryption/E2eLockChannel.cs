using Devoctomy.Blazwiz.Core.Configuration.Interfaces;
using Devoctomy.Blazwiz.Sync.Configuration;
using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;
using Devoctomy.Blazwiz.Sync.Storage;
using Microsoft.JSInterop;

namespace Devoctomy.Blazwiz.Sync.Encryption;

/// <summary>
/// <see cref="IE2eLockChannel"/> over a BroadcastChannel named after the app (<see cref="SyncDefaults.LockChannelName"/>).
/// Each message is a locked profile's id.
/// </summary>
/// <param name="jsRuntime">The JS runtime.</param>
/// <param name="storageKeys">Names the channel after the app.</param>
public sealed class E2eLockChannel(IJSRuntime jsRuntime, IAppStorageKeys storageKeys)
    : IE2eLockChannel, IAsyncDisposable
{
    private readonly IJSRuntime jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
    private readonly IAppStorageKeys storageKeys = storageKeys ?? throw new ArgumentNullException(nameof(storageKeys));

    private IJSObjectReference? module;
    private DotNetObjectReference<E2eLockChannel>? selfReference;

    /// <inheritdoc/>
    public event EventHandler<string>? LockReceived;

    private string ChannelName => storageKeys.For(SyncDefaults.LockChannelName);

    /// <inheritdoc/>
    public async Task StartAsync()
    {
        if (selfReference != null)
        {
            return;
        }

        var js = await GetModuleAsync();
        selfReference = DotNetObjectReference.Create(this);
        await js.InvokeVoidAsync("subscribe", ChannelName, selfReference);
    }

    /// <inheritdoc/>
    public async Task PostLockAsync(string profileId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);

        var js = await GetModuleAsync();
        await js.InvokeVoidAsync("post", ChannelName, profileId);
    }

    /// <summary>
    /// Called by <c>channel.js</c> for each message from another tab.
    /// </summary>
    /// <param name="message">The locked profile's id.</param>
    [JSInvokable]
    public void OnMessage(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            LockReceived?.Invoke(this, message);
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (module != null)
            {
                if (selfReference != null)
                {
                    await module.InvokeVoidAsync("unsubscribe", ChannelName);
                }

                await module.DisposeAsync();
            }
        }
        catch (JSDisconnectedException)
        {
            // The page is going away.
        }

        selfReference?.Dispose();
        selfReference = null;
    }

    private async Task<IJSObjectReference> GetModuleAsync()
    {
        return module ??= await jsRuntime.InvokeAsync<IJSObjectReference>("import", SyncStorageChannel.ModulePath);
    }
}
