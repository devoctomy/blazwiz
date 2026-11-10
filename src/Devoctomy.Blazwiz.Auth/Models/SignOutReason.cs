namespace Devoctomy.Blazwiz.Auth.Models;

/// <summary>
/// Why the user is signed out on every device (see
/// <see cref="Services.Interfaces.IAuthenticationService.SignOutEverywhereAsync"/>).
/// </summary>
public enum SignOutReason
{
    /// <summary>
    /// The user asked to sign out everywhere.
    /// </summary>
    UserRequested,

    /// <summary>
    /// The user's keys were rotated (their passphrase changed), so every device must unlock again with the new ones.
    /// </summary>
    KeysRotated,
}
