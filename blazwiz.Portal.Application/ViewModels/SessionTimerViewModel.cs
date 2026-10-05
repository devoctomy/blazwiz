using System.Globalization;
using Blazing.Mvvm.ComponentModel;
using blazwiz.Core.Offline.Interfaces;
using blazwiz.Portal.Application.Services;
using blazwiz.Portal.Application.Services.Interfaces;

namespace blazwiz.Portal.Application.ViewModels;

/// <summary>
/// ViewModel for the countdown under the user's name: how long until the session's current token expires, or, in
/// offline mode, until the offline session ends.
/// </summary>
/// <remarks>
/// The session is read again every <see cref="ReadInterval"/>, but only while that has no side effects: reading it in
/// the last <see cref="CognitoAuthenticationService.RefreshMargin"/> refreshes the tokens (or ends the session without
/// a refresh token), so it is then only read again once the token has expired: straight away, which ends a session
/// left idle at expiry (an expired token is never renewed), then every <see cref="ReadInterval"/>.
/// </remarks>
public partial class SessionTimerViewModel : ViewModelBase
{
    /// <summary>
    /// How often the countdown is updated.
    /// </summary>
    public static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// How often the session is read again, to pick up tokens refreshed elsewhere.
    /// </summary>
    public static readonly TimeSpan ReadInterval = TimeSpan.FromSeconds(30);

    private readonly IAuthenticationService authService;
    private readonly IOfflineModeService offlineModeService;
    private readonly TimeProvider timeProvider;

    private ITimer? timer;
    private DateTimeOffset? expiresAt;
    private bool isOffline;
    private DateTimeOffset lastReadAt;
    private DateTimeOffset nextReadAt;
    private bool isReading;
    private string text = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionTimerViewModel"/> class.
    /// </summary>
    /// <param name="authService">The authentication service.</param>
    /// <param name="offlineModeService">Offline mode: turning it on or off changes the session, so it is read again.</param>
    /// <param name="timeProvider">The clock and timers.</param>
    public SessionTimerViewModel(IAuthenticationService authService, IOfflineModeService offlineModeService, TimeProvider timeProvider)
    {
        this.authService = authService ?? throw new ArgumentNullException(nameof(authService));
        this.offlineModeService = offlineModeService ?? throw new ArgumentNullException(nameof(offlineModeService));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.offlineModeService.Changed += OnOfflineModeChanged;
    }

    /// <summary>
    /// Gets the countdown text, e.g. "Session 54:12", "Offline 12:34", or "Session expired".
    /// </summary>
    public string Text
    {
        get => text;
        private set => SetProperty(ref text, value);
    }

    /// <inheritdoc/>
    public override async Task OnInitializedAsync()
    {
        await ReadSessionAsync();
        timer = timeProvider.CreateTimer(state => _ = TickAsync(), null, TickInterval, TickInterval);
    }

    /// <summary>
    /// Updates the countdown, reading the session again when it is due. Called by the timer.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task TickAsync()
    {
        try
        {
            if (ShouldRead())
            {
                await ReadSessionAsync();
                return;
            }

            UpdateText();
        }
        catch (Exception)
        {
            // Timer callbacks must not throw; the next tick tries again.
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            offlineModeService.Changed -= OnOfflineModeChanged;
            timer?.Dispose();
            timer = null;
        }

        base.Dispose(disposing);
    }

    private static string Format(TimeSpan remaining)
    {
        return remaining.TotalHours >= 1
            ? remaining.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
            : remaining.ToString(@"mm\:ss", CultureInfo.InvariantCulture);
    }

    private async void OnOfflineModeChanged(object? sender, bool isOffline)
    {
        try
        {
            await ReadSessionAsync();
        }
        catch (Exception)
        {
            // Event handlers must not throw; the next tick reads again.
        }
    }

    private bool ShouldRead()
    {
        if (expiresAt == null || isReading)
        {
            return false;
        }

        var now = timeProvider.GetUtcNow();
        if (now >= expiresAt)
        {
            // Expired: read straight away, which ends the session, then as often as before.
            return lastReadAt < expiresAt || now >= nextReadAt;
        }

        // Read again while it has no side effects, to pick up tokens refreshed elsewhere.
        return now >= nextReadAt && expiresAt - CognitoAuthenticationService.RefreshMargin > now;
    }

    private async Task ReadSessionAsync()
    {
        isReading = true;
        try
        {
            var session = await authService.GetCurrentUserAsync();
            expiresAt = session?.ExpiresAt;
            isOffline = session?.IsOffline == true;
        }
        finally
        {
            isReading = false;
            lastReadAt = timeProvider.GetUtcNow();
            nextReadAt = lastReadAt + ReadInterval;
        }

        UpdateText();
    }

    private void UpdateText()
    {
        if (expiresAt == null)
        {
            Text = "Session expired";
            return;
        }

        var remaining = expiresAt.Value - timeProvider.GetUtcNow();
        Text = remaining > TimeSpan.Zero
            ? $"{(isOffline ? "Offline" : "Session")} {Format(TimeSpan.FromSeconds(Math.Ceiling(remaining.TotalSeconds)))}"
            : "Session expired";
    }
}
