using System.Security.Cryptography;
using blazwiz.Core.Cryptography.Interfaces;
using blazwiz.Portal.Application.Services.Interfaces;

namespace blazwiz.Portal.Application.Services;

/// <inheritdoc/>
public class PasswordCryptoService(ICryptoService cryptoService, IDerivedKeyStore keyStore)
    : IPasswordCryptoService
{
    /// <summary>
    /// The name of the key derived from WebAuthn for remembered passwords.
    /// </summary>
    public const string RememberMeKeyName = "rememberme";

    private readonly ICryptoService cryptoService = cryptoService ?? throw new ArgumentNullException(nameof(cryptoService));
    private readonly IDerivedKeyStore keyStore = keyStore ?? throw new ArgumentNullException(nameof(keyStore));

    /// <inheritdoc/>
    public async Task<string> EncryptAsync(string password)
    {
        var passwordBytes = System.Text.Encoding.UTF8.GetBytes(password);

        using var inputStream = new MemoryStream(passwordBytes);
        using var outputStream = new MemoryStream();
        await cryptoService.EncryptAsync(inputStream, outputStream, GetBase64Key());
        return Convert.ToBase64String(outputStream.ToArray());
    }

    /// <inheritdoc/>
    public async Task<string> DecryptAsync(string encryptedPasswordBase64)
    {
        var encryptedBytes = Convert.FromBase64String(encryptedPasswordBase64);

        using var inputStream = new MemoryStream(encryptedBytes);
        using var outputStream = new MemoryStream();
        await cryptoService.DecryptAsync(inputStream, outputStream, GetBase64Key());
        return System.Text.Encoding.UTF8.GetString(outputStream.ToArray());
    }

    private string GetBase64Key()
    {
        if (!keyStore.TryGetKey(RememberMeKeyName, out var key))
        {
            throw new InvalidOperationException("The remember me key has not been derived. Sign in with WebAuthn first.");
        }

        try
        {
            return Convert.ToBase64String(key);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }
}
