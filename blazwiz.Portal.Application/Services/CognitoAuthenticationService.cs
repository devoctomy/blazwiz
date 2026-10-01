//#if (IncludeNotes)
using blazwiz.Core.Sync.Interfaces;
//#endif
using blazwiz.Core.Time.Interfaces;
using blazwiz.Portal.Application.Exceptions;
using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Models.Auth;
using blazwiz.Portal.Models.Aws;
using Microsoft.IdentityModel.JsonWebTokens;

namespace blazwiz.Portal.Application.Services;

/// <summary>
/// Signs users in to the Cognito user pool (via <see cref="ICognitoUserPoolClient"/>), handles the new
/// password challenge for invited users, and refreshes the tokens when they expire. The session,
/// including the tokens, is kept in local storage so it survives page reloads.
/// </summary>
public class CognitoAuthenticationService(
    ICognitoUserPoolClient cognitoClient,
    ILocalStorageService localStorageService,
    IUserDisplayService userDisplayService,
//#if (IncludeNotes)
    IOfflineModeStore offlineModeStore,
//#endif
    ISystemClock systemClock)
    : IAuthenticationService
{
    /// <summary>
    /// The local storage key the session is stored under.
    /// </summary>
    public const string SessionStorageKey = "blazwiz_cognito_session";

    /// <summary>
    /// How long before the tokens expire they are refreshed.
    /// </summary>
    public static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(5);

    private const string NewPasswordRequiredChallenge = "NEW_PASSWORD_REQUIRED";
    private const string ConnectionError = "Could not reach the sign-in service. Check your connection and try again.";

    private readonly ICognitoUserPoolClient cognitoClient = cognitoClient ?? throw new ArgumentNullException(nameof(cognitoClient));
    private readonly ILocalStorageService localStorageService = localStorageService ?? throw new ArgumentNullException(nameof(localStorageService));
    private readonly IUserDisplayService userDisplayService = userDisplayService ?? throw new ArgumentNullException(nameof(userDisplayService));
//#if (IncludeNotes)
    private readonly IOfflineModeStore offlineModeStore = offlineModeStore ?? throw new ArgumentNullException(nameof(offlineModeStore));
//#endif
    private readonly ISystemClock systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));

    private PendingNewPassword? pendingNewPassword;

    /// <inheritdoc/>
    public bool IsPlaceholder => false;

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
            return Failed("Please enter your email and password.");
        }

        var trimmedUsername = username.Trim();
        try
        {
            var result = await cognitoClient.SignInAsync(trimmedUsername, password);
            return await HandleSignInResultAsync(result, trimmedUsername);
        }
        catch (CognitoException ex)
        {
            return Failed(GetFriendlyError(ex));
        }
        catch (HttpRequestException)
        {
            return Failed(ConnectionError);
        }
    }

    /// <inheritdoc/>
    public async Task<LoginResult> CompleteNewPasswordAsync(string newPassword)
    {
        var pending = pendingNewPassword;
        if (pending == null)
        {
            return Failed("Please sign in again.");
        }

        if (string.IsNullOrWhiteSpace(newPassword))
        {
            return Failed("Please enter a new password.");
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
            return Failed(ex.Message);
        }
        catch (CognitoException ex)
        {
            pendingNewPassword = null;
            return Failed(ex.ErrorType == "NotAuthorizedException"
                ? "Your sign-in has expired. Please sign in again."
                : GetFriendlyError(ex));
        }
        catch (HttpRequestException)
        {
            return Failed(ConnectionError);
        }
    }

    /// <inheritdoc/>
    public async Task LogoutAsync()
    {
        pendingNewPassword = null;
        var session = await localStorageService.GetObjectAsync<UserSession>(SessionStorageKey);
        await localStorageService.RemoveItemAsync(SessionStorageKey);

        if (!string.IsNullOrEmpty(session?.RefreshToken))
        {
            try
            {
                await cognitoClient.RevokeAsync(session.RefreshToken);
            }
            catch (Exception ex) when (ex is CognitoException or HttpRequestException)
            {
                // Best effort: the session is already gone from this browser.
            }
        }
    }

    /// <inheritdoc/>
    public async Task<UserSession?> GetCurrentUserAsync()
    {
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

        if (session.ExpiresAt - RefreshMargin > systemClock.UtcNow)
        {
            return session;
        }

//#if (IncludeNotes)
        if (await offlineModeStore.GetAsync())
        {
            // Offline mode makes no network calls: keep the stored session and refresh it once back online.
            return session;
        }

//#endif
        if (string.IsNullOrEmpty(session.RefreshToken))
        {
            await localStorageService.RemoveItemAsync(SessionStorageKey);
            return null;
        }

        try
        {
            var result = await cognitoClient.RefreshAsync(session.Username, session.RefreshToken);
            if (string.IsNullOrEmpty(result.IdToken))
            {
                await localStorageService.RemoveItemAsync(SessionStorageKey);
                return null;
            }

            var refreshed = CreateSession(result, session.Username, session.RefreshToken, session.SignedInAt);
            await localStorageService.SetObjectAsync(SessionStorageKey, refreshed);
            return refreshed;
        }
        catch (CognitoException)
        {
            // The refresh token has expired or been revoked.
            await localStorageService.RemoveItemAsync(SessionStorageKey);
            return null;
        }
        catch (HttpRequestException)
        {
            // Offline: keep the stored session so it can be refreshed once back online.
            return null;
        }
    }

    private static LoginResult Failed(string error)
    {
        return new LoginResult
        {
            Success = false,
            Error = error,
        };
    }

    private static string GetFriendlyError(CognitoException ex)
    {
        return ex.ErrorType switch
        {
            "PasswordResetRequiredException" => "Your password must be reset before you can sign in. Please contact an administrator.",
            "UserNotConfirmedException" => "Your account has not been confirmed yet.",
            "TooManyRequestsException" or "LimitExceededException" => "Too many attempts. Please wait a moment and try again.",

            // E.g. NotAuthorizedException: "Incorrect username or password." or "Password attempts exceeded".
            _ => ex.Message,
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

    private async Task<LoginResult> HandleSignInResultAsync(CognitoSignInResult result, string username)
    {
        if (!string.IsNullOrEmpty(result.IdToken))
        {
            var session = CreateSession(result, username, null, systemClock.UtcNow);
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

        return Failed($"Sign-in needs '{result.ChallengeName}', which this portal does not support yet.");
    }

    private UserSession CreateSession(CognitoSignInResult tokens, string fallbackUsername, string? existingRefreshToken, DateTimeOffset signedInAt)
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
            ExpiresAt = systemClock.UtcNow.AddSeconds(tokens.ExpiresIn),
            IdToken = tokens.IdToken,
            AccessToken = tokens.AccessToken,
            RefreshToken = tokens.RefreshToken ?? existingRefreshToken,
        };
    }

    private sealed class PendingNewPassword(string username, string sessionId)
    {
        public string Username { get; } = username;

        public string SessionId { get; } = sessionId;
    }
}
