namespace Devoctomy.Blazwiz.UI.Sync.Services.Interfaces;

/// <summary>
/// Ends a passphrase change: signs the user out on every device, this one included, so every device forgets the old
/// keys, then returns to the login page. While they are signed out, the rotation mark of each profile whose files were
/// encrypted again is cleared.
/// </summary>
public interface IE2eRotationSignOut
{
    /// <summary>
    /// Signs out everywhere with the reason <see cref="Auth.Models.SignOutReason.KeysRotated"/>, finishes signing out
    /// here and opens the login page.
    /// </summary>
    /// <returns>True when signed out everywhere; false when the sign-in service could not be reached and nothing
    /// changed, so the passphrase change is not finished and the sign out must be tried again.</returns>
    Task<bool> SignOutEverywhereAsync();
}
