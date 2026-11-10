using System.Globalization;
using System.Text.Json;
using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Events.AppSync.Models;
using Devoctomy.Blazwiz.Events.Channels.Interfaces;
using Devoctomy.Blazwiz.Events.Models;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.JSInterop;

namespace Devoctomy.Blazwiz.Events.AppSync.Channels;

/// <summary>
/// <see cref="IUserEventChannel"/> over an AWS AppSync Events API: the user's channel is <c>/users/&lt;sub&gt;</c>
/// in the <c>users</c> namespace, where <c>sub</c> is their user pool subject. The API authorises with the session's
/// Cognito access token, and its handlers refuse any other user's channel. The WebSocket protocol and publishing are
/// in <c>js/appsync-events.js</c>.
/// </summary>
public sealed class AppSyncUserEventChannel(IJSRuntime jsRuntime, AppSyncEventsConfig config)
    : IUserEventChannel, IAsyncDisposable, IDisposable
{
    /// <summary>
    /// The module's path.
    /// </summary>
    public const string ModulePath = "./_content/Devoctomy.Blazwiz.Events.AppSync/js/appsync-events.js";

    /// <summary>
    /// The channel namespace the user channels are in.
    /// </summary>
    public const string Namespace = "users";

    private readonly IJSRuntime jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
    private readonly AppSyncEventsConfig config = config ?? throw new ArgumentNullException(nameof(config));
    private IJSObjectReference? module;
    private DotNetObjectReference<AppSyncUserEventChannel>? selfReference;
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

    /// <summary>
    /// Gets the user's channel for a session's access token, or null when it has none (offline or placeholder).
    /// </summary>
    /// <param name="accessToken">The access token.</param>
    /// <returns>The channel's path.</returns>
    public static string? GetChannelPath(string? accessToken)
    {
        if (string.IsNullOrEmpty(accessToken))
        {
            return null;
        }

        try
        {
            // Read only to name the channel; AppSync validates the token, and its handler checks the channel is this
            // user's own.
            return new JsonWebToken(accessToken).TryGetPayloadValue<string>("sub", out var sub) && !string.IsNullOrEmpty(sub)
                ? $"/{Namespace}/{sub}"
                : null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    /// <remarks>A session without an access token (offline, or the placeholder sign in) leaves it disconnected.</remarks>
    public async Task ConnectAsync(UserSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var channel = GetChannelPath(session.AccessToken);
        if (channel == null || State != UserEventConnectionState.Disconnected)
        {
            return;
        }

        var js = await GetModuleAsync();
        selfReference ??= DotNetObjectReference.Create(this);
        State = UserEventConnectionState.Connecting;
        try
        {
            await js.InvokeVoidAsync("connect", config.RealtimeEndpoint, config.HttpEndpoint, session.AccessToken, channel, selfReference);
            State = UserEventConnectionState.Connected;
        }
        catch (Exception)
        {
            State = UserEventConnectionState.Disconnected;
            throw;
        }
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
        State = UserEventConnectionState.Disconnected;
    }

    /// <inheritdoc/>
    /// <remarks>Published over HTTPS with the session's access token, whether or not the WebSocket is connected.</remarks>
    public async Task PublishAsync(UserSession session, UserEvent userEvent)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(userEvent);
        var channel = GetChannelPath(session.AccessToken)
            ?? throw new InvalidOperationException("Publishing needs a session with an access token.");
        var js = await GetModuleAsync();
        await js.InvokeVoidAsync("publish", config.HttpEndpoint, session.AccessToken, channel, JsonSerializer.Serialize(userEvent));
    }

    /// <summary>
    /// Called from <c>appsync-events.js</c> when an event arrives on the user's channel.
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

    /// <summary>
    /// Called from <c>appsync-events.js</c> when the connection closed without being asked to (network lost, the
    /// service closed it, or its keep-alive stopped).
    /// </summary>
    [JSInvokable]
    public void OnClosed()
    {
        State = UserEventConnectionState.Disconnected;
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
    /// Stops .NET being called back. The browser's connection goes with the page; prefer <see cref="DisposeAsync"/>,
    /// which closes it too.
    /// </summary>
    public void Dispose()
    {
        selfReference?.Dispose();
        selfReference = null;
    }

    private async Task<IJSObjectReference> GetModuleAsync()
    {
        return module ??= await jsRuntime.InvokeAsync<IJSObjectReference>("import", ModulePath);
    }
}
