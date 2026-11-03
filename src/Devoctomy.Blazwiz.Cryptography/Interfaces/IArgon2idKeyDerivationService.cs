using Devoctomy.Blazwiz.Cryptography.Models;

namespace Devoctomy.Blazwiz.Cryptography.Interfaces;

/// <summary>
/// Derives a key from a passphrase with Argon2id, the memory hard password hashing function (RFC 9106). It is slow
/// and memory hungry on purpose, so guessing passphrases is expensive.
/// </summary>
public interface IArgon2idKeyDerivationService
{
    /// <summary>
    /// Derives a key from a passphrase.
    /// </summary>
    /// <remarks>
    /// WebAssembly is single threaded, so this blocks the UI while it runs. It yields once first, so the caller can show
    /// that it is working before it starts.
    /// </remarks>
    /// <param name="passphrase">The passphrase, encoded as UTF-8.</param>
    /// <param name="salt">A random salt, stored with whatever the key protects.</param>
    /// <param name="parameters">The cost settings.</param>
    /// <param name="ct">A cancellation token, checked before it starts.</param>
    /// <returns>The key, <see cref="Argon2idParameters.KeyLength"/> bytes. The caller should wipe it when done.</returns>
    Task<byte[]> DeriveKeyAsync(string passphrase, byte[] salt, Argon2idParameters parameters, CancellationToken ct = default);
}
