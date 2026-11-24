using Devoctomy.Blazwiz.Auth.Exceptions;
using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Core.Exceptions;

namespace Devoctomy.Blazwiz.Auth.Services;

/// <summary>
/// Placeholder two-step verification, used with <see cref="LocalAuthenticationService"/> (no identity provider, e.g.
/// local development): remembers in local storage who turned it on, and accepts any six-digit code. Once it is on,
/// the placeholder sign in asks for a code.
/// </summary>
public class LocalMfaService(IAuthenticationService authenticationService, ILocalStorageService localStorageService)
    : IMfaService
{
    /// <summary>
    /// The local storage key holding the usernames that have it on.
    /// </summary>
    public const string StorageKey = "placeholder_mfa";

    /// <summary>
    /// The secret handed out for the authenticator app. Any code is accepted, so it is the same for everyone.
    /// </summary>
    public const string PlaceholderSecret = "JBSWY3DPEHPK3PXP";

    private readonly IAuthenticationService authenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));
    private readonly ILocalStorageService localStorageService = localStorageService ?? throw new ArgumentNullException(nameof(localStorageService));

    /// <inheritdoc/>
    public async Task<bool> IsEnabledAsync()
    {
        var username = await GetUsernameAsync();
        return IsEnabledFor(await LoadAsync(localStorageService), username);
    }

    /// <inheritdoc/>
    public async Task<string> BeginSetupAsync()
    {
        await GetUsernameAsync();
        return PlaceholderSecret;
    }

    /// <inheritdoc/>
    public async Task CompleteSetupAsync(string code)
    {
        if (!IsValidCode(code))
        {
            throw new BlazwizException(string.IsNullOrWhiteSpace(code) ? AuthErrorCodes.MfaCodeRequired : AuthErrorCodes.CodeMismatch);
        }

        var username = await GetUsernameAsync();
        var users = await LoadAsync(localStorageService);
        if (!IsEnabledFor(users, username))
        {
            users.Add(username);
            await localStorageService.SetObjectAsync(StorageKey, users);
        }
    }

    /// <inheritdoc/>
    public async Task DisableAsync()
    {
        var username = await GetUsernameAsync();
        var users = await LoadAsync(localStorageService);
        users.RemoveAll(u => string.Equals(u, username, StringComparison.OrdinalIgnoreCase));
        await localStorageService.SetObjectAsync(StorageKey, users);
    }

    /// <summary>
    /// Gets whether a code is one the placeholder accepts: six digits.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <returns>True when it is.</returns>
    internal static bool IsValidCode(string? code)
    {
        var trimmed = code?.Trim() ?? string.Empty;
        return trimmed.Length == 6 && trimmed.All(char.IsAsciiDigit);
    }

    /// <summary>
    /// Gets whether a username has it on.
    /// </summary>
    /// <param name="users">The usernames that have it on.</param>
    /// <param name="username">The username.</param>
    /// <returns>True when it is on.</returns>
    internal static bool IsEnabledFor(IEnumerable<string> users, string username)
    {
        return users.Any(u => string.Equals(u, username, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Loads the usernames that have it on.
    /// </summary>
    /// <param name="storage">Local storage.</param>
    /// <returns>The usernames.</returns>
    internal static async Task<List<string>> LoadAsync(ILocalStorageService storage)
    {
        return await storage.GetObjectAsync<List<string>>(StorageKey) ?? [];
    }

    private async Task<string> GetUsernameAsync()
    {
        var session = await authenticationService.GetCurrentUserAsync();
        if (session == null)
        {
            throw new BlazwizException(AuthErrorCodes.NotSignedIn);
        }

        return session.Username;
    }
}
