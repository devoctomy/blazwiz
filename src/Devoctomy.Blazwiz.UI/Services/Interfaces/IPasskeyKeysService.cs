namespace Devoctomy.Blazwiz.UI.Services.Interfaces;

/// <summary>
/// The keys derived from the signed in user's passkey (the WebAuthn PRF), held in memory only: they are there after a
/// sign in with the passkey, and gone after a reload. This asks for the passkey again when they are needed and not
/// there, and forgets one that should not stay in memory once it has been used. Only a user remembered on this device
/// with a passkey ("Remember me" at sign in) has one.
/// </summary>
public interface IPasskeyKeysService
{
    /// <summary>
    /// Gets whether the signed in user has a passkey on this device: they are remembered here, with a passkey.
    /// </summary>
    /// <returns>True if they have one.</returns>
    Task<bool> IsAvailableAsync();

    /// <summary>
    /// Makes sure a key derived from the passkey is in memory, asking for the passkey (fingerprint, face or PIN) if
    /// it is not.
    /// </summary>
    /// <param name="keyName">The key's name, as added to the PRF keys (e.g. <c>e2e-kek</c>).</param>
    /// <returns>True if the key is in memory now; false if there is no passkey, or the user cancelled or it failed.</returns>
    Task<bool> EnsureKeyAsync(string keyName);

    /// <summary>
    /// Wipes a key derived from the passkey from memory, so the passkey is asked for again the next time it is needed.
    /// Nothing happens if it is not there.
    /// </summary>
    /// <param name="keyName">The key's name, as added to the PRF keys (e.g. <c>e2e-kek</c>).</param>
    void ForgetKey(string keyName);
}
