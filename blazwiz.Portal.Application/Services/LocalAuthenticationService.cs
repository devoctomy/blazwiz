using blazwiz.Core.Offline.Interfaces;
using blazwiz.Core.Offline.Models;
using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Models.Auth;

namespace blazwiz.Portal.Application.Services;

/// <summary>
/// Placeholder authentication which accepts any non-empty username and password and keeps the
/// session in local storage. Used when no Cognito user pool is configured (e.g. local development),
/// otherwise <see cref="CognitoAuthenticationService"/> is used.
/// </summary>
public class LocalAuthenticationService(
    ILocalStorageService localStorageService,
    IUserDisplayService userDisplayService,
    IOfflineModeService offlineModeService,
    IOfflineSessionStore offlineSessionStore,
    TimeProvider timeProvider)
    : IAuthenticationService, IDisposable
{
    /// <summary>
    /// The local storage key the session is stored under.
    /// </summary>
    public const string SessionStorageKey = "session";

    /// <summary>
    /// How long a session lasts before the user must sign in again.
    /// </summary>
    public static readonly TimeSpan SessionDuration = TimeSpan.FromHours(8);

    private readonly ILocalStorageService localStorageService = localStorageService ?? throw new ArgumentNullException(nameof(localStorageService));
    private readonly IUserDisplayService userDisplayService = userDisplayService ?? throw new ArgumentNullException(nameof(userDisplayService));
    private readonly IOfflineModeService offlineModeService = offlineModeService ?? throw new ArgumentNullException(nameof(offlineModeService));
    private readonly IOfflineSessionStore offlineSessionStore = offlineSessionStore ?? throw new ArgumentNullException(nameof(offlineSessionStore));
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    private bool isListening;

    /// <inheritdoc/>
    public event EventHandler? SessionEnded;

    /// <inheritdoc/>
    public bool IsPlaceholder => true;

    /// <inheritdoc/>
    public Task<LoginResult> CompleteNewPasswordAsync(string newPassword)
    {
        // The placeholder never asks for a new password.
        return Task.FromResult(new LoginResult
        {
            Success = false,
            Error = "Please sign in first.",
        });
    }

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

        var now = timeProvider.GetUtcNow();
        var trimmedUsername = username.Trim();
        var session = new UserSession
        {
            Username = trimmedUsername,
            DisplayName = userDisplayService.GetDisplayNameFromUsername(trimmedUsername),
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
        offlineSessionStore.Clear();
        await localStorageService.RemoveItemAsync(SessionStorageKey);
    }

    /// <inheritdoc/>
    public async Task<LoginResult> SignInOfflineAsync(string username, OfflineEntryReason reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

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
        await localStorageService.RemoveItemAsync(SessionStorageKey);
        ListenForOfflineModeChanges();
        offlineModeService.Enter(OfflineEntryReason.Chosen);
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
            return null;
        }

        var session = await localStorageService.GetObjectAsync<UserSession>(SessionStorageKey);
        if (session == null)
        {
            return null;
        }

        if (session.ExpiresAt <= timeProvider.GetUtcNow())
        {
            await EndSessionAsync();
            return null;
        }

        return session;
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

    private async Task EndSessionAsync()
    {
        await LogoutAsync();
        SessionEnded?.Invoke(this, EventArgs.Empty);
    }

    private void ListenForOfflineModeChanges()
    {
        if (!isListening)
        {
            offlineModeService.Changed += OnOfflineModeChanged;
            isListening = true;
        }
    }

    // Turning offline mode off ends an offline session, so the user signs in online again.
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
}
