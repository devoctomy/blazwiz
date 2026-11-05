using Devoctomy.Blazwiz.Encryption.Models;

namespace Devoctomy.Blazwiz.Encryption.Interfaces;

/// <summary>
/// The format of an encrypted file: a readable header saying which key and profile it needs, then the ciphertext and
/// tag.
/// </summary>
/// <remarks>
/// <code>
/// "E2EE"      4 bytes, ASCII, so encrypted files are easy to spot
/// version     1 byte
/// key id len  1 byte
/// key id      UTF-8
/// scope len   1 byte
/// scope id    UTF-8: the profile's key scope
/// nonce       12 bytes, new for every write
/// ciphertext  followed by the 16 byte tag
/// </code>
/// The associated data is the whole header followed by the file's path, so the header is readable but cannot be
/// changed, and the file only decrypts at its own path in its own profile.
/// </remarks>
public interface IEncryptedFileFormat
{
    /// <summary>
    /// Gets the key id from an encrypted file's header.
    /// </summary>
    /// <param name="content">The file content.</param>
    /// <param name="keyId">The key id, when the content is an encrypted file.</param>
    /// <returns>True when the content is an encrypted file with a well formed header.</returns>
    bool TryGetKeyId(ReadOnlySpan<byte> content, out string? keyId);

    /// <summary>
    /// Encrypts content, bound to its profile's key scope and its path.
    /// </summary>
    /// <param name="plaintext">The content to encrypt.</param>
    /// <param name="keyId">The id of the key, written to the header.</param>
    /// <param name="key">The file key.</param>
    /// <param name="binding">The key scope and path the file is bound to.</param>
    /// <returns>The encrypted file.</returns>
    byte[] Encrypt(ReadOnlySpan<byte> plaintext, string keyId, byte[] key, E2eFileBinding binding);

    /// <summary>
    /// Decrypts an encrypted file, checking it is bound to the given key scope and path.
    /// </summary>
    /// <param name="content">The encrypted file.</param>
    /// <param name="key">The file key named in its header.</param>
    /// <param name="binding">The key scope and path the file must be bound to.</param>
    /// <returns>The plaintext.</returns>
    /// <exception cref="InvalidDataException">The content is not an encrypted file of that key scope.</exception>
    /// <exception cref="System.Security.Cryptography.AuthenticationTagMismatchException">
    /// Wrong key, another path, or the file has been changed.
    /// </exception>
    byte[] Decrypt(ReadOnlySpan<byte> content, byte[] key, E2eFileBinding binding);
}
