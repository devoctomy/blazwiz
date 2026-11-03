using blazwiz.Core.Configuration.Interfaces;
using blazwiz.Core.Sync.Configuration;
using blazwiz.Core.Sync.Interfaces;
using blazwiz.Core.Sync.Models;
using Microsoft.JSInterop;

namespace blazwiz.Core.Sync.Storage;

/// <summary>
/// <see cref="ISyncStorageChannel"/> over the <c>wwwroot/js/channel.js</c> module shipped with this library
/// (a BroadcastChannel named <see cref="SyncDefaults.StorageChannelName"/>).
/// </summary>
public sealed class SyncStorageChannel(IJSRuntime jsRuntime, IAppStorageKeys storageKeys)
    : ISyncStorageChannel, IAsyncDisposable
{
    /// <summary>
    /// The path the module is imported from (Razor class library static assets).
    /// </summary>
    public const string ModulePath = "./_content/blazwiz.Core/js/channel.js";

    private readonly IJSRuntime jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
    private readonly IAppStorageKeys storageKeys = storageKeys ?? throw new ArgumentNullException(nameof(storageKeys));
    private IJSObjectReference? module;
    private DotNetObjectReference<SyncStorageChannel>? selfReference;

    /// <inheritdoc/>
    public event EventHandler<SyncStorageChannelMessage>? MessageReceived;

    /// <inheritdoc/>
    public async Task StartAsync()
    {
        if (selfReference != null)
        {
            return;
        }

        var js = await GetModuleAsync();
        selfReference = DotNetObjectReference.Create(this);
        await js.InvokeVoidAsync("subscribe", storageKeys.For(SyncDefaults.StorageChannelName), selfReference);
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

    /// <inheritdoc/>
    public async Task PostAsync(SyncStorageChannelMessage message)
    {
        var js = await GetModuleAsync();
        await js.InvokeVoidAsync("post", storageKeys.For(SyncDefaults.StorageChannelName), message.ToString());
    }

    /// <summary>
    /// Called by the module for each message from another tab. Unknown messages are ignored.
    /// </summary>
    /// <param name="message">The message.</param>
    [JSInvokable]
    public void OnMessage(string message)
    {
        if (Enum.TryParse<SyncStorageChannelMessage>(message, out var parsed) && Enum.IsDefined(parsed))
        {
            MessageReceived?.Invoke(this, parsed);
        }
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
