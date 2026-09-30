namespace blazwiz.Portal.Application.Services.Interfaces;

/// <summary>
/// Provides password encryption and decryption operations using a PRF-derived key.
/// </summary>
public interface IPasswordCryptoService
{
    /// <summary>
    /// Encrypts a plain-text password using a key derived from the given PRF output.
    /// </summary>
    /// <param name="password">The plain-text password to encrypt.</param>
    /// <param name="prfOutputHex">The hex-encoded PRF output used as the encryption key.</param>
    /// <returns>The encrypted password as a Base64-encoded string.</returns>
    Task<string> EncryptWithPrfAsync(string password, string prfOutputHex);

    /// <summary>
    /// Decrypts an encrypted password using a key derived from the given PRF output.
    /// </summary>
    /// <param name="encryptedPasswordBase64">The Base64-encoded encrypted password.</param>
    /// <param name="prfOutputHex">The hex-encoded PRF output used as the decryption key.</param>
    /// <returns>The decrypted plain-text password.</returns>
    Task<string> DecryptWithPrfAsync(string encryptedPasswordBase64, string prfOutputHex);
}
