namespace blazwiz.Core.Cryptography.Interfaces;

/// <summary>
/// Interface for cryptographic services providing encryption and decryption capabilities.
/// </summary>
public interface ICryptoService
{
    /// <summary>
    /// Encrypts data from the input stream and writes to the output stream.
    /// </summary>
    /// <param name="inputStream">Stream containing data to encrypt.</param>
    /// <param name="outputStream">Stream to write encrypted data to (includes IV at the beginning).</param>
    /// <param name="base64Key">Base64 encoded encryption key.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    Task EncryptAsync(
        Stream inputStream,
        Stream outputStream,
        string? base64Key);

    /// <summary>
    /// Decrypts data from the input stream and writes to the output stream.
    /// </summary>
    /// <param name="inputStream">Stream containing encrypted data (including IV at the beginning).</param>
    /// <param name="outputStream">Stream to write decrypted data to.</param>
    /// <param name="base64Key">Base64 encoded encryption key.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    Task DecryptAsync(
        Stream inputStream,
        Stream outputStream,
        string? base64Key);
}
