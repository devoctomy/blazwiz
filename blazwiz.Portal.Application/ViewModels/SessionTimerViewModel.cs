using System.Globalization;
using Blazing.Mvvm.ComponentModel;
using blazwiz.Portal.Application.Services;
using blazwiz.Portal.Application.Services.Interfaces;

namespace blazwiz.Portal.Application.ViewModels;

/// <summary>
/// ViewModel for the countdown under the user's name: how long until the session's current token expires.
/// </summary>
/// <remarks>
/// The session is read again every <see cref="ReadInterval"/>, but only while that has no side effects: reading it in
/// the last <see cref="CognitoAuthenticationService.RefreshMargin"/> refreshes the tokens (or ends the session without
/// a refresh token), so it is then only read again once the token has expired, as anything else using it would.
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
    private readonly TimeProvider timeProvider;

    private ITimer? timer;
    private DateTimeOffset? expiresAt;
    private DateTimeOffset nextReadAt;
    private bool isReading;
    private string text = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionTimerViewModel"/> class.
    /// </summary>
    /// <param name="authService">The authentication service.</param>
    /// <param name="timeProvider">The clock and timers.</param>
    public SessionTimerViewModel(IAuthenticationService authService, TimeProvider timeProvider)
    {
        this.authService = authService ?? throw new ArgumentNullException(nameof(authService));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary>
    /// Gets the countdown text, e.g. "Session 54:12", or "Session expired".
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

    private bool ShouldRead()
    {
        if (expiresAt == null || isReading)
        {
            return false;
        }

        var now = timeProvider.GetUtcNow();
        if (now >= expiresAt)
        {
            // Expired: reading now refreshes or ends the session, as any other use would.
            return now >= nextReadAt;
        }

        // Read again while it has no side effects, to pick up tokens refreshed elsewhere.
        return now >= nextReadAt && expiresAt - CognitoAuthenticationService.RefreshMargin > now;
    }

    private async Task ReadSessionAsync()
    {
        isReading = true;
        try
        {
            expiresAt = (await authService.GetCurrentUserAsync())?.ExpiresAt;
        }
        finally
        {
            isReading = false;
            nextReadAt = timeProvider.GetUtcNow() + ReadInterval;
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
            ? $"Session {Format(TimeSpan.FromSeconds(Math.Ceiling(remaining.TotalSeconds)))}"
            : "Session expired";
    }
}
