using Devoctomy.Blazwiz.Encryption.Models;

namespace Devoctomy.Blazwiz.Sync.Encryption.Interfaces;

/// <summary>
/// Told when an unlocked profile meets a file encrypted with a key its key ring does not hold, before the read fails:
/// most likely the profile's keys were rotated on another device, which also signed every device out. A module that
/// knows about sessions (e.g. the sync UI) checks the session here, so a device that missed the sign out finds out
/// now. Register any number; a handler that throws does not stop the others.
/// </summary>
public interface ISyncKeyMissingHandler
{
    /// <summary>
    /// Called when a file of an unlocked profile needs a key its key ring does not hold.
    /// </summary>
    /// <param name="scope">The profile's key scope.</param>
    /// <param name="keyId">The key the file names.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task OnKeyMissingAsync(E2eKeyScope scope, string keyId);
}
