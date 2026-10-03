namespace blazwiz.Core.Cryptography.Interfaces;

/// <summary>
/// Authenticated Encryption with Associated Data (AEAD), e.g. AES-GCM. Encrypting adds an authentication tag, so any
/// change to the ciphertext, nonce or key makes decryption fail rather than return wrong data.
/// </summary>
/// <remarks>
/// The associated data is not encrypted and not stored in the output, but it is covered by the tag: decryption only
/// succeeds when the same associated data is passed again. Use it for data that must stay readable but must not be
/// changed or swapped, such as a file header or the id a key is stored under.
/// </remarks>
public interface IAeadCipher
{
    /// <summary>
    /// Gets the key length in bytes.
    /// </summary>
    int KeySize { get; }

    /// <summary>
    /// Gets the nonce length in bytes. A nonce must never be used twice with the same key.
    /// </summary>
    int NonceSize { get; }

    /// <summary>
    /// Gets the authentication tag length in bytes, added to the end of the ciphertext.
    /// </summary>
    int TagSize { get; }

    /// <summary>
    /// Encrypts the plaintext.
    /// </summary>
    /// <param name="key">The key, <see cref="KeySize"/> bytes.</param>
    /// <param name="nonce">A new random nonce, <see cref="NonceSize"/> bytes.</param>
    /// <param name="plaintext">The data to encrypt.</param>
    /// <param name="associatedData">Data to authenticate but not encrypt, or null.</param>
    /// <returns>The ciphertext followed by the tag.</returns>
    byte[] Encrypt(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> associatedData = default);

    /// <summary>
    /// Decrypts and authenticates the ciphertext.
    /// </summary>
    /// <param name="key">The key, <see cref="KeySize"/> bytes.</param>
    /// <param name="nonce">The nonce it was encrypted with.</param>
    /// <param name="ciphertext">The ciphertext followed by the tag.</param>
    /// <param name="associatedData">The associated data it was encrypted with, or null.</param>
    /// <returns>The plaintext.</returns>
    /// <exception cref="System.Security.Cryptography.AuthenticationTagMismatchException">
    /// The key, nonce, ciphertext or associated data is wrong.
    /// </exception>
    byte[] Decrypt(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> associatedData = default);
}
