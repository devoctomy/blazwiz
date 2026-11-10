using System.Globalization;
using System.Text.Json;
using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Core.Configuration.Interfaces;
using Devoctomy.Blazwiz.Events.Channels.Interfaces;
using Devoctomy.Blazwiz.Events.Models;
using Microsoft.JSInterop;

namespace Devoctomy.Blazwiz.Events.Channels;

/// <summary>
/// <see cref="IUserEventChannel"/> between the tabs of one browser, over the browser's <c>BroadcastChannel</c> API
/// (<c>js/user-events-local.js</c>). Needs no AWS: used for development with the placeholder sign in, and by the UI
/// tests. It reaches no other device.
/// </summary>
public sealed class LocalUserEventChannel(IJSRuntime jsRuntime, IAppStorageKeys storageKeys)
    : IUserEventChannel, IAsyncDisposable, IDisposable
{
    /// <summary>
    /// The module's path.
    /// </summary>
    public const string ModulePath = "./_content/Devoctomy.Blazwiz.Events/js/user-events-local.js";

    private readonly IJSRuntime jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
    private readonly IAppStorageKeys storageKeys = storageKeys ?? throw new ArgumentNullException(nameof(storageKeys));
    private IJSObjectReference? module;
    private DotNetObjectReference<LocalUserEventChannel>? selfReference;
    private string? channelName;
    private UserEventConnectionState state;

    /// <inheritdoc/>
    public event EventHandler<UserEvent>? Received;

    /// <inheritdoc/>
    public event EventHandler? StateChanged;

    /// <inheritdoc/>
    public string InstanceId { get; } = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);

    /// <inheritdoc/>
    public UserEventConnectionState State
    {
        get => state;
        private set
        {
            if (state != value)
            {
                state = value;
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <inheritdoc/>
    public async Task ConnectAsync(UserSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var name = GetChannelName(session);
        if (State == UserEventConnectionState.Connected && name == channelName)
        {
            return;
        }

        var js = await GetModuleAsync();
        selfReference ??= DotNetObjectReference.Create(this);
        State = UserEventConnectionState.Connecting;
        await js.InvokeVoidAsync("connect", name, selfReference);
        channelName = name;
        State = UserEventConnectionState.Connected;
    }

    /// <inheritdoc/>
    public async Task DisconnectAsync()
    {
        if (State == UserEventConnectionState.Disconnected)
        {
            return;
        }

        var js = await GetModuleAsync();
        await js.InvokeVoidAsync("disconnect");
        channelName = null;
        State = UserEventConnectionState.Disconnected;
    }

    /// <inheritdoc/>
    public async Task PublishAsync(UserSession session, UserEvent userEvent)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(userEvent);
        var js = await GetModuleAsync();
        await js.InvokeVoidAsync("publish", GetChannelName(session), JsonSerializer.Serialize(userEvent));
    }

    /// <summary>
    /// Called from <c>user-events-local.js</c> when another tab publishes.
    /// </summary>
    /// <param name="json">The event, as JSON.</param>
    [JSInvokable]
    public void OnMessage(string json)
    {
        UserEvent? userEvent;
        try
        {
            userEvent = JsonSerializer.Deserialize<UserEvent>(json);
        }
        catch (JsonException)
        {
            return;
        }

        if (userEvent == null || string.IsNullOrEmpty(userEvent.Type) || userEvent.From == InstanceId)
        {
            return;
        }

        Received?.Invoke(this, userEvent);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await DisconnectAsync();
            if (module != null)
            {
                await module.DisposeAsync();
            }
        }
        catch (JSDisconnectedException)
        {
            // The page is going away.
        }

        Dispose();
    }

    /// <summary>
    /// Stops .NET being called back. The browser's channel goes with the page; prefer <see cref="DisposeAsync"/>,
    /// which closes it too.
    /// </summary>
    public void Dispose()
    {
        selfReference?.Dispose();
        selfReference = null;
    }

    private string GetChannelName(UserSession session) =>
        $"{storageKeys.For("events")}:{session.Username.Trim().ToLowerInvariant()}";

    private async Task<IJSObjectReference> GetModuleAsync()
    {
        return module ??= await jsRuntime.InvokeAsync<IJSObjectReference>("import", ModulePath);
    }
}
