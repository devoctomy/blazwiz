namespace Devoctomy.Blazwiz.Encryption.Interfaces;

/// <summary>
/// The format of an encrypted file: a readable header saying which key it needs, then the ciphertext and tag.
/// </summary>
/// <remarks>
/// <code>
/// "E2EE"      4 bytes, ASCII, so encrypted files are easy to spot
/// version     1 byte
/// key id len  1 byte
/// key id      UTF-8
/// nonce       12 bytes, new for every write
/// ciphertext  followed by the 16 byte tag
/// </code>
/// The whole header is the associated data, so it is readable but cannot be changed without decryption failing.
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
    /// Encrypts content into the format.
    /// </summary>
    /// <param name="plaintext">The content to encrypt.</param>
    /// <param name="keyId">The id of the key, written to the header.</param>
    /// <param name="key">The file key.</param>
    /// <returns>The encrypted file.</returns>
    byte[] Encrypt(ReadOnlySpan<byte> plaintext, string keyId, byte[] key);

    /// <summary>
    /// Decrypts an encrypted file.
    /// </summary>
    /// <param name="content">The encrypted file.</param>
    /// <param name="key">The file key named in its header.</param>
    /// <returns>The plaintext.</returns>
    /// <exception cref="InvalidDataException">The content is not an encrypted file.</exception>
    /// <exception cref="System.Security.Cryptography.AuthenticationTagMismatchException">
    /// Wrong key, or the file has been changed.
    /// </exception>
    byte[] Decrypt(ReadOnlySpan<byte> content, byte[] key);
}
