using Devoctomy.Blazwiz.Encryption.Models;

namespace Devoctomy.Blazwiz.UI.Sync.Services.Interfaces;

/// <summary>
/// Unlocks a protected profile with biometrics (the signed in user's passkey) from anywhere in the app, such as a list
/// of the user's profiles, so it opens straight onto its content. Biometrics are turned on for a profile when it is
/// unlocked with its passphrase in the unlock panel; they are only offered to a user remembered on this device with a
/// passkey ("Remember me" at sign in). Whatever happens here, the passphrase still unlocks the profile.
/// </summary>
public interface IE2eDeviceUnlockService
{
    /// <summary>
    /// Gets whether a profile can be unlocked with biometrics on this device: its settings allow it, it was turned on
    /// here, and the signed in user has a passkey here. False when this device's copy of its keys cannot be read.
    /// </summary>
    /// <param name="scope">The profile's scope.</param>
    /// <returns>True when biometrics can unlock it.</returns>
    Task<bool> CanUnlockAsync(E2eKeyScope scope);

    /// <summary>
    /// Unlocks a profile with biometrics: online, it is synced first, so what it shows once open is up to date; then
    /// the passkey is asked for if its key is not in memory. Does nothing for a profile already unlocked.
    /// </summary>
    /// <param name="scope">The profile's scope.</param>
    /// <returns>
    /// True when it is unlocked; false when the passkey was cancelled or could not unlock it (e.g. another passkey
    /// than the one it was turned on with), so its passphrase is needed.
    /// </returns>
    Task<bool> UnlockAsync(E2eKeyScope scope);

    /// <summary>
    /// Stops unlocking a profile with biometrics on this device, forgetting its passkey-wrapped key here. Its
    /// passphrase still unlocks it, and biometrics can be turned on again when it is next unlocked with it.
    /// </summary>
    /// <param name="scope">The profile's scope.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task ForgetAsync(E2eKeyScope scope);
}
