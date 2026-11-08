namespace Devoctomy.Blazwiz.Auth.Services.Interfaces;

/// <summary>
/// Encrypts and decrypts remembered passwords with the remember me key derived from WebAuthn. Each is bound to the
/// email address of the user it was saved for, so it cannot be moved to another user's entry.
/// </summary>
public interface IPasswordCryptoService
{
    /// <summary>
    /// Encrypts a plain-text password with the remember me key.
    /// </summary>
    /// <param name="password">The plain-text password to encrypt.</param>
    /// <param name="email">The email address of the user the password is remembered for.</param>
    /// <returns>The encrypted password as a Base64-encoded string.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the remember me key has not been derived.</exception>
    Task<string> EncryptAsync(string password, string email);

    /// <summary>
    /// Decrypts an encrypted password with the remember me key.
    /// </summary>
    /// <param name="encryptedPasswordBase64">The Base64-encoded encrypted password.</param>
    /// <param name="email">The email address of the user it was remembered for.</param>
    /// <returns>The decrypted plain-text password.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the remember me key has not been derived.</exception>
    /// <exception cref="System.Security.Cryptography.AuthenticationTagMismatchException">
    /// Another key, another user, or the stored value has been changed.
    /// </exception>
    Task<string> DecryptAsync(string encryptedPasswordBase64, string email);
}
