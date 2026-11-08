using System.Security.Cryptography;
using System.Text;
using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.Cryptography.Interfaces;

namespace Devoctomy.Blazwiz.Auth.Services;

/// <summary>
/// Implementation of <see cref="IPasswordCryptoService"/> with the <see cref="IAeadCipher"/> (AES-GCM) and the
/// <see cref="RememberMeKeyName"/> key derived from the passkey. The user's email is the associated data, so a
/// remembered password only decrypts for the user it was saved for. Stored as base64 of the nonce followed by the
/// ciphertext and tag.
/// </summary>
public class PasswordCryptoService(IAeadCipher cipher, IDerivedKeyStore keyStore)
    : IPasswordCryptoService
{
    /// <summary>
    /// The name of the key derived from WebAuthn for remembered passwords.
    /// </summary>
    public const string RememberMeKeyName = "rememberme";

    private readonly IAeadCipher cipher = cipher ?? throw new ArgumentNullException(nameof(cipher));
    private readonly IDerivedKeyStore keyStore = keyStore ?? throw new ArgumentNullException(nameof(keyStore));

    /// <inheritdoc/>
    public Task<string> EncryptAsync(string password, string email)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var key = GetKey();
        var plaintext = Encoding.UTF8.GetBytes(password);
        try
        {
            var nonce = RandomNumberGenerator.GetBytes(cipher.NonceSize);
            var ciphertext = cipher.Encrypt(key, nonce, plaintext, Binding(email));
            var output = new byte[nonce.Length + ciphertext.Length];
            nonce.CopyTo(output, 0);
            ciphertext.CopyTo(output, nonce.Length);
            return Task.FromResult(Convert.ToBase64String(output));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    /// <inheritdoc/>
    public Task<string> DecryptAsync(string encryptedPasswordBase64, string email)
    {
        ArgumentException.ThrowIfNullOrEmpty(encryptedPasswordBase64);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var stored = Convert.FromBase64String(encryptedPasswordBase64);
        if (stored.Length < cipher.NonceSize + cipher.TagSize)
        {
            throw new AuthenticationTagMismatchException("The remembered password is too short to be one.");
        }

        var key = GetKey();
        byte[]? plaintext = null;
        try
        {
            plaintext = cipher.Decrypt(key, stored.AsSpan(0, cipher.NonceSize), stored.AsSpan(cipher.NonceSize), Binding(email));
            return Task.FromResult(Encoding.UTF8.GetString(plaintext));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            if (plaintext != null)
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }
        }
    }

    // Email addresses are matched without regard to case elsewhere, so the binding is too.
    private static byte[] Binding(string email) => Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant());

    private byte[] GetKey()
    {
        if (!keyStore.TryGetKey(RememberMeKeyName, out var key))
        {
            throw new InvalidOperationException("The remember me key has not been derived. Sign in with WebAuthn first.");
        }

        return key;
    }
}
