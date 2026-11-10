using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.Auth.Sessions.Interfaces;
using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Core.Offline.Interfaces;
using Devoctomy.Blazwiz.Events.Channels.Interfaces;
using Devoctomy.Blazwiz.Events.Models;

namespace Devoctomy.Blazwiz.Events.Sessions;

/// <summary>
/// Keeps the user's event channel connected while they are signed in online, and acts on what arrives:
/// <see cref="UserEventTypes.SignedOutEverywhere"/> ends the session here, naming the device that sent it.
/// </summary>
/// <remarks>
/// Connects when the session starts; reconnects with backoff when the connection drops, and at once when the app
/// resumes and the session check (<see cref="ISessionResumeMonitor"/>) found the session still good; disconnects on
/// sign out and when offline mode is turned on (offline mode makes no network calls). An offline session never
/// connects.
/// </remarks>
public sealed class UserEventsSessionHandler : ISessionLifecycleHandler, IDisposable
{
    /// <summary>
    /// The first wait before reconnecting; it doubles after each failure, up to <see cref="MaxReconnectDelay"/>.
    /// </summary>
    public static readonly TimeSpan FirstReconnectDelay = TimeSpan.FromSeconds(2);

    /// <summary>
    /// The longest wait before reconnecting.
    /// </summary>
    public static readonly TimeSpan MaxReconnectDelay = TimeSpan.FromMinutes(2);

    private readonly IUserEventChannel channel;
    private readonly IAuthenticationService authService;
    private readonly ISessionResumeMonitor resumeMonitor;
    private readonly IOfflineModeService offlineModeService;
    private readonly IDeviceNameService deviceNameService;
    private readonly TimeProvider timeProvider;

    private bool isActive;
    private bool isConnecting;
    private int failures;
    private ITimer? reconnectTimer;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserEventsSessionHandler"/> class.
    /// </summary>
    /// <param name="channel">The user's event channel.</param>
    /// <param name="authService">Gets the session to connect with, and ends it when told to.</param>
    /// <param name="resumeMonitor">Says when the app resumed with its session still good.</param>
    /// <param name="offlineModeService">Offline mode: no connection while it is on.</param>
    /// <param name="deviceNameService">Makes other devices' names safe to show.</param>
    /// <param name="timeProvider">Times the reconnects.</param>
    public UserEventsSessionHandler(
        IUserEventChannel channel,
        IAuthenticationService authService,
        ISessionResumeMonitor resumeMonitor,
        IOfflineModeService offlineModeService,
        IDeviceNameService deviceNameService,
        TimeProvider timeProvider)
    {
        this.channel = channel ?? throw new ArgumentNullException(nameof(channel));
        this.authService = authService ?? throw new ArgumentNullException(nameof(authService));
        this.resumeMonitor = resumeMonitor ?? throw new ArgumentNullException(nameof(resumeMonitor));
        this.offlineModeService = offlineModeService ?? throw new ArgumentNullException(nameof(offlineModeService));
        this.deviceNameService = deviceNameService ?? throw new ArgumentNullException(nameof(deviceNameService));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <inheritdoc/>
    public async Task OnSessionStartedAsync(UserSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.IsOffline || offlineModeService.IsOffline)
        {
            return;
        }

        if (!isActive)
        {
            isActive = true;
            channel.Received += OnReceived;
            channel.StateChanged += OnStateChanged;
            resumeMonitor.SessionResumed += OnSessionResumed;
            offlineModeService.Changed += OnOfflineModeChanged;
        }

        await ConnectAsync(session);
    }

    /// <inheritdoc/>
    public async Task OnSigningOutAsync()
    {
        Stop();
        await channel.DisconnectAsync();
    }

    /// <summary>
    /// Connects with the current session, unless signed out, offline or already connected. On failure, tries again
    /// later. Called when the app resumes and by the reconnect timer.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task ReconnectAsync()
    {
        if (!isActive || channel.State != UserEventConnectionState.Disconnected || offlineModeService.IsOffline)
        {
            return;
        }

        var session = await authService.GetCurrentUserAsync();
        if (session == null || session.IsOffline)
        {
            return;
        }

        await ConnectAsync(session);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Stop();
    }

    private static SessionEndedReason ToSessionEndedReason(string? reason) =>
        reason == UserEventReasons.KeysRotated ? SessionEndedReason.KeysRotated : SessionEndedReason.SignedOutEverywhere;

    private async Task ConnectAsync(UserSession session)
    {
        if (isConnecting)
        {
            return;
        }

        isConnecting = true;
        try
        {
            CancelReconnect();
            await channel.ConnectAsync(session);
            failures = 0;
        }
        catch (Exception)
        {
            // Unreachable, or refused: try again later. Missing events is allowed; the resume check is the fallback.
            ScheduleReconnect();
        }
        finally
        {
            isConnecting = false;
        }
    }

    private void ScheduleReconnect()
    {
        if (!isActive)
        {
            return;
        }

        CancelReconnect();
        var delay = TimeSpan.FromTicks(Math.Min(MaxReconnectDelay.Ticks, FirstReconnectDelay.Ticks << Math.Min(failures, 16)));
        failures++;
        reconnectTimer = timeProvider.CreateTimer(_ => _ = ReconnectSafelyAsync(), null, delay, Timeout.InfiniteTimeSpan);
    }

    private void CancelReconnect()
    {
        reconnectTimer?.Dispose();
        reconnectTimer = null;
    }

    private void Stop()
    {
        if (isActive)
        {
            isActive = false;
            channel.Received -= OnReceived;
            channel.StateChanged -= OnStateChanged;
            resumeMonitor.SessionResumed -= OnSessionResumed;
            offlineModeService.Changed -= OnOfflineModeChanged;
        }

        CancelReconnect();
        failures = 0;
    }

    private async Task ReconnectSafelyAsync()
    {
        try
        {
            await ReconnectAsync();
        }
        catch (Exception)
        {
            // Timer callbacks must not throw.
        }
    }

    private async void OnReceived(object? sender, UserEvent e)
    {
        try
        {
            if (e.Type == UserEventTypes.SignedOutEverywhere)
            {
                Stop();
                await authService.EndSessionAsync(ToSessionEndedReason(e.Reason), deviceNameService.Sanitize(e.Device));
            }
        }
        catch (Exception)
        {
            // Event handlers must not throw; the session check on the next resume ends a revoked session anyway.
        }
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (isActive && !isConnecting && channel.State == UserEventConnectionState.Disconnected && reconnectTimer == null)
        {
            // The connection dropped.
            ScheduleReconnect();
        }
    }

    private void OnSessionResumed(object? sender, EventArgs e)
    {
        failures = 0;
        _ = ReconnectSafelyAsync();
    }

    private async void OnOfflineModeChanged(object? sender, bool isOffline)
    {
        try
        {
            if (isOffline)
            {
                // Offline mode makes no network calls, and its session cannot use the channel.
                Stop();
                await channel.DisconnectAsync();
            }
        }
        catch (Exception)
        {
            // Event handlers must not throw.
        }
    }
}
