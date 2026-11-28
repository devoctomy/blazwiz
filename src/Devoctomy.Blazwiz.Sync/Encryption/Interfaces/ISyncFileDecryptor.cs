using Devoctomy.Blazwiz.Encryption.Exceptions;

namespace Devoctomy.Blazwiz.Sync.Encryption.Interfaces;

/// <summary>
/// Decrypts a profile's stored (encrypted) content, as reading one of its files does, e.g. a past version downloaded
/// from the bucket. Get a profile's from <c>ISyncProfileRegistry.GetFileDecryptor</c>.
/// </summary>
public interface ISyncFileDecryptor
{
    /// <summary>
    /// Decrypts content stored at a key of the profile. Content of any other key or profile does not decrypt.
    /// </summary>
    /// <param name="key">The key the content is stored at.</param>
    /// <param name="content">The stored content.</param>
    /// <returns>The plaintext. A folder marker's (empty) content is returned as it is.</returns>
    /// <exception cref="E2eLockedException">The profile is locked, or the content is encrypted with a key this device
    /// does not hold.</exception>
    /// <exception cref="InvalidDataException">The content is not an encrypted file of the profile.</exception>
    Task<byte[]> DecryptAsync(string key, byte[] content);
}
