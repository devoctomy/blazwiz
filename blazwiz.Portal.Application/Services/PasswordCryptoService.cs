using blazwiz.Core.Cryptography.Interfaces;
using blazwiz.Portal.Application.Services.Interfaces;

namespace blazwiz.Portal.Application.Services;

/// <inheritdoc/>
public class PasswordCryptoService(ICryptoService cryptoService)
    : IPasswordCryptoService
{
    private readonly ICryptoService cryptoService = cryptoService ?? throw new ArgumentNullException(nameof(cryptoService));

    /// <inheritdoc/>
    public async Task<string> EncryptWithPrfAsync(string password, string prfOutputHex)
    {
        var prfBytes = Convert.FromHexString(prfOutputHex);
        var base64Key = Convert.ToBase64String(prfBytes);
        var passwordBytes = System.Text.Encoding.UTF8.GetBytes(password);

        using var inputStream = new MemoryStream(passwordBytes);
        using var outputStream = new MemoryStream();
        await cryptoService.EncryptAsync(inputStream, outputStream, base64Key);
        return Convert.ToBase64String(outputStream.ToArray());
    }

    /// <inheritdoc/>
    public async Task<string> DecryptWithPrfAsync(string encryptedPasswordBase64, string prfOutputHex)
    {
        var prfBytes = Convert.FromHexString(prfOutputHex);
        var base64Key = Convert.ToBase64String(prfBytes);
        var encryptedBytes = Convert.FromBase64String(encryptedPasswordBase64);

        using var inputStream = new MemoryStream(encryptedBytes);
        using var outputStream = new MemoryStream();
        await cryptoService.DecryptAsync(inputStream, outputStream, base64Key);
        return System.Text.Encoding.UTF8.GetString(outputStream.ToArray());
    }
}
