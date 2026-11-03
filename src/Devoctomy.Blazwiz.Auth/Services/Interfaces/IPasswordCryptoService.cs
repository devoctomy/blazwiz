namespace Devoctomy.Blazwiz.Auth.Services.Interfaces;

/// <summary>
/// Encrypts and decrypts remembered passwords with the remember me key derived from WebAuthn.
/// </summary>
public interface IPasswordCryptoService
{
    /// <summary>
    /// Encrypts a plain-text password with the remember me key.
    /// </summary>
    /// <param name="password">The plain-text password to encrypt.</param>
    /// <returns>The encrypted password as a Base64-encoded string.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the remember me key has not been derived.</exception>
    Task<string> EncryptAsync(string password);

    /// <summary>
    /// Decrypts an encrypted password with the remember me key.
    /// </summary>
    /// <param name="encryptedPasswordBase64">The Base64-encoded encrypted password.</param>
    /// <returns>The decrypted plain-text password.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the remember me key has not been derived.</exception>
    Task<string> DecryptAsync(string encryptedPasswordBase64);
}
