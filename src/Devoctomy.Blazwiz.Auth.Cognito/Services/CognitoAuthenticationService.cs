using Devoctomy.Blazwiz.Auth.Cognito.Exceptions;
using Devoctomy.Blazwiz.Auth.Cognito.Models;
using Devoctomy.Blazwiz.Auth.Cognito.Services.Interfaces;
using Devoctomy.Blazwiz.Auth.Exceptions;
using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.Auth.Sessions.Interfaces;
using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Core.Offline.Interfaces;
using Devoctomy.Blazwiz.Core.Offline.Models;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Devoctomy.Blazwiz.Auth.Cognito.Services;

/// <summary>
/// Signs users in to the Cognito user pool (via <see cref="ICognitoUserPoolClient"/>), handles the new
/// password challenge for invited users, and refreshes the tokens when they expire. The session, with the
/// short-lived ID and access tokens, is kept in local storage so it survives page reloads and works offline.
/// </summary>
/// <remarks>
/// The refresh token is never stored: it is held in memory, so only this tab can renew the session, and only until
/// it is closed or reloaded. After that the user signs in again (one touch with passkey quick login). Anything that
/// can run script in the app's origin can read local storage, and a stored refresh token would let it act as the user
/// for the token's whole validity (<c>RefreshTokenValidity</c> in <c>cloudformation/stack.yaml</c>), from anywhere.
/// To keep sessions across restarts, at least: keep that validity short, add a Content-Security-Policy, and consider
/// a backend that holds the tokens (backend for frontend) rather than the browser.
/// <para>
/// In offline mode nothing here calls Cognito: the session is an offline one held in <see cref="IOfflineSessionStore"/>,
/// with no tokens, and it ends at its expiry or when offline mode is turned off.
/// </para>
/// </remarks>
public class CognitoAuthenticationService(
    ICognitoUserPoolClient cognitoClient,
    ILocalStorageService localStorageService,
    IUserDisplayService userDisplayService,
    IOfflineModeService offlineModeService,
    IOfflineSessionStore offlineSessionStore,
    ISignOutEverywhereNotifier signOutEverywhereNotifier,
    TimeProvider timeProvider)
    : IAuthenticationService, IDisposable
{
    /// <summary>
    /// The local storage key the session is stored under.
    /// </summary>
    public const string SessionStorageKey = "cognito_session";

    /// <summary>
    /// How long before the tokens expire they are refreshed.
    /// </summary>
    public static readonly TimeSpan TokenRefreshMargin = TimeSpan.FromMinutes(5);

    private const string NewPasswordRequiredChallenge = "NEW_PASSWORD_REQUIRED";
    private const string NotAuthorized = "NotAuthorizedException";

    private readonly ICognitoUserPoolClient cognitoClient = cognitoClient ?? throw new ArgumentNullException(nameof(cognitoClient));
    private readonly ILocalStorageService localStorageService = localStorageService ?? throw new ArgumentNullException(nameof(localStorageService));
    private readonly IUserDisplayService userDisplayService = userDisplayService ?? throw new ArgumentNullException(nameof(userDisplayService));
    private readonly IOfflineModeService offlineModeService = offlineModeService ?? throw new ArgumentNullException(nameof(offlineModeService));
    private readonly IOfflineSessionStore offlineSessionStore = offlineSessionStore ?? throw new ArgumentNullException(nameof(offlineSessionStore));
    private readonly ISignOutEverywhereNotifier signOutEverywhereNotifier = signOutEverywhereNotifier ?? throw new ArgumentNullException(nameof(signOutEverywhereNotifier));
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    private PendingNewPassword? pendingNewPassword;
    private string? refreshToken;
    private bool isListening;

    /// <inheritdoc/>
    public event EventHandler<SessionEndedEventArgs>? SessionEnded;

    /// <inheritdoc/>
    public bool IsPlaceholder => false;

    /// <inheritdoc/>
    public TimeSpan RefreshMargin => TokenRefreshMargin;

    /// <summary>
    /// Gets a value indicating whether enough Cognito settings are present to sign in with Cognito.
    /// </summary>
    /// <param name="config">The Cognito settings.</param>
    /// <returns>True when the region, user pool id and app client id are all set.</returns>
    public static bool IsConfigured(AwsCognitoConfig? config)
    {
        return config != null
            && !string.IsNullOrWhiteSpace(config.Region)
            && !string.IsNullOrWhiteSpace(config.UserPoolId)
            && !string.IsNullOrWhiteSpace(config.UserPoolClientId);
    }

    /// <inheritdoc/>
    public async Task<LoginResult> LoginAsync(string username, string password)
    {
        pendingNewPassword = null;
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return Failed(AuthErrorCodes.CredentialsRequired);
        }

        var trimmedUsername = username.Trim();
        try
        {
            var result = await cognitoClient.SignInAsync(trimmedUsername, password);
            return await HandleSignInResultAsync(result, trimmedUsername);
        }
        catch (CognitoException ex)
        {
            return Failed(GetErrorCode(ex));
        }
        catch (HttpRequestException)
        {
            return Unreachable();
        }
        catch (TaskCanceledException)
        {
            // The request timed out.
            return Unreachable();
        }
    }

    /// <inheritdoc/>
    public async Task<LoginResult> SignInOfflineAsync(string username, OfflineEntryReason reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        pendingNewPassword = null;
        refreshToken = null;
        await localStorageService.RemoveItemAsync(SessionStorageKey);
        var trimmed = username.Trim();
        var session = offlineSessionStore.Start(trimmed, userDisplayService.GetDisplayNameFromUsername(trimmed), trimmed.Contains('@') ? trimmed : null);
        ListenForOfflineModeChanges();
        offlineModeService.Enter(reason);
        return new LoginResult
        {
            Success = true,
            Session = session,
        };
    }

    /// <inheritdoc/>
    public async Task GoOfflineAsync()
    {
        var current = await GetCurrentUserAsync()
            ?? throw new InvalidOperationException("Sign in before turning offline mode on.");
        if (current.IsOffline)
        {
            return;
        }

        offlineSessionStore.Start(current.Username, current.DisplayName, current.Email);
        var token = refreshToken;
        refreshToken = null;
        await localStorageService.RemoveItemAsync(SessionStorageKey);
        await RevokeAsync(token);
        ListenForOfflineModeChanges();
        offlineModeService.Enter(OfflineEntryReason.Chosen);
    }

    /// <inheritdoc/>
    public async Task<LoginResult> CompleteNewPasswordAsync(string newPassword)
    {
        var pending = pendingNewPassword;
        if (pending == null)
        {
            return Failed(AuthErrorCodes.SignInAgain);
        }

        if (string.IsNullOrWhiteSpace(newPassword))
        {
            return Failed(AuthErrorCodes.NewPasswordRequired);
        }

        try
        {
            var result = await cognitoClient.CompleteNewPasswordAsync(pending.SessionId, newPassword);
            pendingNewPassword = null;
            return await HandleSignInResultAsync(result, pending.Username);
        }
        catch (CognitoException ex) when (ex.ErrorType == "InvalidPasswordException")
        {
            // Let the user try a different password.
            return Failed(AuthErrorCodes.InvalidPassword);
        }
        catch (CognitoException ex)
        {
            pendingNewPassword = null;
            return Failed(ex.ErrorType == "NotAuthorizedException"
                ? AuthErrorCodes.SignInExpired
                : GetErrorCode(ex));
        }
        catch (HttpRequestException)
        {
            return Failed(AuthErrorCodes.Unreachable);
        }
    }

    /// <inheritdoc/>
    public async Task LogoutAsync()
    {
        pendingNewPassword = null;
        offlineSessionStore.Clear();
        var token = refreshToken;
        refreshToken = null;
        await localStorageService.RemoveItemAsync(SessionStorageKey);
        await RevokeAsync(token);
    }

    /// <inheritdoc/>
    public async Task<UserSession?> GetCurrentUserAsync()
    {
        var offline = offlineSessionStore.Session;
        if (offline != null)
        {
            if (offline.ExpiresAt <= timeProvider.GetUtcNow())
            {
                await EndSessionAsync();
                return null;
            }

            return offline;
        }

        if (offlineModeService.IsOffline)
        {
            // Offline mode with no offline session: nobody is signed in, and nothing may call Cognito.
            return null;
        }

        var session = await localStorageService.GetObjectAsync<UserSession>(SessionStorageKey);
        if (session == null)
        {
            return null;
        }

        if (string.IsNullOrEmpty(session.IdToken))
        {
            await localStorageService.RemoveItemAsync(SessionStorageKey);
            return null;
        }

        if (session.ExpiresAt - TokenRefreshMargin > timeProvider.GetUtcNow())
        {
            return session;
        }

        if (session.ExpiresAt <= timeProvider.GetUtcNow())
        {
            // Not used while it was valid, so it is not brought back to life: renewing only happens before expiry.
            await EndSessionAsync();
            return null;
        }

        if (string.IsNullOrEmpty(refreshToken))
        {
            // Signed in from another tab, or before a reload: this tab cannot renew it.
            await EndSessionAsync();
            return null;
        }

        try
        {
            var result = await cognitoClient.RefreshAsync(session.Username, refreshToken);
            if (string.IsNullOrEmpty(result.IdToken))
            {
                await EndSessionAsync();
                return null;
            }

            refreshToken = result.RefreshToken ?? refreshToken;
            var refreshed = CreateSession(result, session.Username, session.SignedInAt);
            await localStorageService.SetObjectAsync(SessionStorageKey, refreshed);
            return refreshed;
        }
        catch (CognitoException)
        {
            // The refresh token has expired or been revoked.
            await EndSessionAsync();
            return null;
        }
        catch (HttpRequestException)
        {
            // Offline: keep the stored session so it can be refreshed once back online, while it is still valid.
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> SignOutEverywhereAsync(SignOutReason reason)
    {
        if (offlineModeService.IsOffline)
        {
            return false;
        }

        var session = await GetCurrentUserAsync();
        if (session == null || session.IsOffline || string.IsNullOrEmpty(session.AccessToken))
        {
            return false;
        }

        try
        {
            await cognitoClient.GlobalSignOutAsync(session.AccessToken);
        }
        catch (CognitoException ex) when (ex.ErrorType == NotAuthorized)
        {
            // Already revoked: the user was signed out everywhere from another device meanwhile. Carry on.
        }
        catch (Exception ex) when (ex is CognitoException or HttpRequestException or TaskCanceledException)
        {
            return false;
        }

        // Every refresh token is revoked: there is nothing left to revoke on the way out.
        refreshToken = null;
        await signOutEverywhereNotifier.NotifyAsync(session, reason);
        await LogoutAsync();
        return true;
    }

    /// <inheritdoc/>
    public async Task EndSessionAsync(SessionEndedReason reason, string? deviceName)
    {
        await LogoutAsync();
        SessionEnded?.Invoke(this, new SessionEndedEventArgs(reason, deviceName));
    }

    /// <inheritdoc/>
    public async Task<bool> CheckSessionAsync()
    {
        // Reading the session renews it when due, or ends it when it cannot be renewed.
        var session = await GetCurrentUserAsync();
        if (session == null)
        {
            return false;
        }

        if (session.IsOffline || offlineModeService.IsOffline || string.IsNullOrEmpty(session.AccessToken))
        {
            // Offline mode makes no calls to Cognito: checked once it is turned off.
            return true;
        }

        try
        {
            await cognitoClient.CheckAccessTokenAsync(session.AccessToken);
            return true;
        }
        catch (CognitoException ex) when (ex.ErrorType == NotAuthorized)
        {
            await EndSessionAsync(SessionEndedReason.Revoked, null);
            return false;
        }
        catch (Exception ex) when (ex is CognitoException or HttpRequestException or TaskCanceledException)
        {
            // Throttled or unreachable: keep the session and check again next time.
            return true;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (isListening)
        {
            offlineModeService.Changed -= OnOfflineModeChanged;
            isListening = false;
        }

        GC.SuppressFinalize(this);
    }

    private static LoginResult Failed(string errorCode, params object[] arguments)
    {
        return new LoginResult
        {
            Success = false,
            ErrorCode = errorCode,
            ErrorArguments = arguments,
        };
    }

    private static LoginResult Unreachable()
    {
        return new LoginResult
        {
            Success = false,
            ErrorCode = AuthErrorCodes.Unreachable,
            ServiceUnreachable = true,
        };
    }

    private static string GetErrorCode(CognitoException ex)
    {
        return ex.ErrorType switch
        {
            "PasswordResetRequiredException" => AuthErrorCodes.PasswordResetRequired,
            "UserNotConfirmedException" => AuthErrorCodes.UserNotConfirmed,
            "TooManyRequestsException" or "LimitExceededException" => AuthErrorCodes.TooManyAttempts,

            // Wrong email or password, or too many failed attempts.
            "NotAuthorizedException" or "UserNotFoundException" => AuthErrorCodes.NotAuthorized,
            _ => AuthErrorCodes.SignInFailed,
        };
    }

    private static string? GetClaim(string? jwt, string claimName)
    {
        if (string.IsNullOrEmpty(jwt))
        {
            return null;
        }

        try
        {
            // Only read for display. The token came straight from Cognito, and AWS validates it wherever it grants access.
            return new JsonWebToken(jwt).TryGetPayloadValue<string>(claimName, out var value) ? value : null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    // Revokes a refresh token, best effort. Never while offline: offline mode makes no calls to Cognito.
    private async Task RevokeAsync(string? token)
    {
        if (string.IsNullOrEmpty(token) || offlineModeService.IsOffline)
        {
            return;
        }

        try
        {
            await cognitoClient.RevokeAsync(token);
        }
        catch (Exception ex) when (ex is CognitoException or HttpRequestException)
        {
            // Best effort: the session is already gone from this browser.
        }
    }

    private void ListenForOfflineModeChanges()
    {
        if (!isListening)
        {
            offlineModeService.Changed += OnOfflineModeChanged;
            isListening = true;
        }
    }

    // Turning offline mode off ends an offline session: there are no tokens to carry on with, so the user signs in
    // online again.
    private async void OnOfflineModeChanged(object? sender, bool isOffline)
    {
        try
        {
            if (!isOffline && offlineSessionStore.Session != null)
            {
                await EndSessionAsync();
            }
        }
        catch (Exception)
        {
            // Event handlers must not throw.
        }
    }

    // Signs out (revoking the refresh token, if this tab has one) and tells the app, so it returns to the login page.
    private Task EndSessionAsync() => EndSessionAsync(SessionEndedReason.Expired, null);

    private async Task<LoginResult> HandleSignInResultAsync(CognitoSignInResult result, string username)
    {
        if (!string.IsNullOrEmpty(result.IdToken))
        {
            refreshToken = result.RefreshToken;
            var session = CreateSession(result, username, timeProvider.GetUtcNow());
            await localStorageService.SetObjectAsync(SessionStorageKey, session);
            return new LoginResult
            {
                Success = true,
                Session = session,
            };
        }

        if (result.ChallengeName == NewPasswordRequiredChallenge && !string.IsNullOrEmpty(result.SessionId))
        {
            pendingNewPassword = new PendingNewPassword(username, result.SessionId);
            return new LoginResult
            {
                Success = false,
                NewPasswordRequired = true,
            };
        }

        return Failed(AuthErrorCodes.ChallengeUnsupported, result.ChallengeName ?? string.Empty);
    }

    private UserSession CreateSession(CognitoSignInResult tokens, string fallbackUsername, DateTimeOffset signedInAt)
    {
        var email = GetClaim(tokens.IdToken, "email");
        var username = email ?? fallbackUsername;
        var name = GetClaim(tokens.IdToken, "name");

        return new UserSession
        {
            Username = username,
            DisplayName = string.IsNullOrWhiteSpace(name) ? userDisplayService.GetDisplayNameFromUsername(username) : name,
            Email = email,
            SignedInAt = signedInAt,
            ExpiresAt = timeProvider.GetUtcNow().AddSeconds(tokens.ExpiresIn),
            IdToken = tokens.IdToken,
            AccessToken = tokens.AccessToken,
        };
    }

    private sealed class PendingNewPassword(string username, string sessionId)
    {
        public string Username { get; } = username;

        public string SessionId { get; } = sessionId;
    }
}
