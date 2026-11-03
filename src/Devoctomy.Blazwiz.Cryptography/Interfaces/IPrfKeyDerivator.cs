namespace Devoctomy.Blazwiz.Cryptography.Interfaces;

/// <summary>
/// Derives the configured keys from a WebAuthn PRF output and puts them in the <see cref="IDerivedKeyStore"/>, so
/// nothing else needs the PRF output. The rest of the app gets its keys from the store by name.
/// </summary>
public interface IPrfKeyDerivator
{
    /// <summary>
    /// Derives every configured key from the PRF output, replacing any keys already in the store.
    /// </summary>
    /// <param name="prfOutput">The PRF output. The caller still owns it, and should wipe it afterwards.</param>
    void DeriveKeys(byte[] prfOutput);
}
