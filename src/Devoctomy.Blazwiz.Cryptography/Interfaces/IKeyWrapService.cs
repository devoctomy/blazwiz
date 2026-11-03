using Devoctomy.Blazwiz.Cryptography.Models;

namespace Devoctomy.Blazwiz.Cryptography.Interfaces;

/// <summary>
/// Wraps (encrypts) keys with a key encryption key, using an <see cref="IAeadCipher"/>, so a key can be stored where
/// only the holder of the key encryption key can use it. Also makes the random keys to wrap.
/// </summary>
public interface IKeyWrapService
{
    /// <summary>
    /// Makes a new key from a cryptographically secure random number generator.
    /// </summary>
    /// <param name="length">The length in bytes, by default the cipher's key size.</param>
    /// <returns>The key. The caller should wipe it when done.</returns>
    byte[] CreateKey(int? length = null);

    /// <summary>
    /// Wraps a key with a new random nonce.
    /// </summary>
    /// <param name="keyEncryptionKey">The key to wrap with.</param>
    /// <param name="key">The key to wrap.</param>
    /// <param name="associatedData">
    /// Data bound to the wrapped key, e.g. the id it is stored under, so it cannot be moved to another entry. The same
    /// data must be passed to unwrap it.
    /// </param>
    /// <returns>The wrapped key.</returns>
    WrappedKey Wrap(byte[] keyEncryptionKey, byte[] key, byte[]? associatedData = null);

    /// <summary>
    /// Unwraps a key.
    /// </summary>
    /// <param name="keyEncryptionKey">The key it was wrapped with.</param>
    /// <param name="wrappedKey">The wrapped key.</param>
    /// <param name="associatedData">The associated data it was wrapped with.</param>
    /// <returns>The key. The caller should wipe it when done.</returns>
    /// <exception cref="System.Security.Cryptography.AuthenticationTagMismatchException">
    /// Wrong key encryption key or associated data, or the wrapped key has been changed.
    /// </exception>
    /// <exception cref="NotSupportedException">The key was wrapped with another algorithm.</exception>
    /// <exception cref="FormatException">The nonce or wrapped key is not valid base64.</exception>
    byte[] Unwrap(byte[] keyEncryptionKey, WrappedKey wrappedKey, byte[]? associatedData = null);
}
