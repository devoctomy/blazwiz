using blazwiz.Core.Time.Interfaces;
using blazwiz.Portal.Application.Helpers;
using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Models.Auth;

namespace blazwiz.Portal.Application.Services;

/// <summary>
/// Placeholder authentication which accepts any non-empty username and password and keeps the
/// session in local storage. Replace with a Cognito backed <see cref="IAuthenticationService"/>
/// once the AWS resources are deployed.
/// </summary>
public class LocalAuthenticationService(
    ILocalStorageService localStorageService,
    ISystemClock systemClock)
    : IAuthenticationService
{
    /// <summary>
    /// The local storage key the session is stored under.
    /// </summary>
    public const string SessionStorageKey = "blazwiz_session";

    /// <summary>
    /// How long a session lasts before the user must sign in again.
    /// </summary>
    public static readonly TimeSpan SessionDuration = TimeSpan.FromHours(8);

    private readonly ILocalStorageService localStorageService = ValidationHelpers.VerifiedAssignment(localStorageService);
    private readonly ISystemClock systemClock = ValidationHelpers.VerifiedAssignment(systemClock);

    /// <inheritdoc/>
    public async Task<LoginResult> LoginAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return new LoginResult
            {
                Success = false,
                Error = "Please enter your username and password.",
            };
        }

        var now = systemClock.UtcNow;
        var trimmedUsername = username.Trim();
        var session = new UserSession
        {
            Username = trimmedUsername,
            DisplayName = UserDisplayHelpers.GetDisplayNameFromUsername(trimmedUsername),
            Email = trimmedUsername.Contains('@') ? trimmedUsername : null,
            SignedInAt = now,
            ExpiresAt = now.Add(SessionDuration),
        };

        await localStorageService.SetObjectAsync(SessionStorageKey, session);

        return new LoginResult
        {
            Success = true,
            Session = session,
        };
    }

    /// <inheritdoc/>
    public async Task LogoutAsync()
    {
        await localStorageService.RemoveItemAsync(SessionStorageKey);
    }

    /// <inheritdoc/>
    public async Task<UserSession?> GetCurrentUserAsync()
    {
        var session = await localStorageService.GetObjectAsync<UserSession>(SessionStorageKey);
        if (session == null)
        {
            return null;
        }

        if (session.ExpiresAt <= systemClock.UtcNow)
        {
            await LogoutAsync();
            return null;
        }

        return session;
    }
}
