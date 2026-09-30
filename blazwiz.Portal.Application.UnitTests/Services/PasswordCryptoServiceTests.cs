using System.Security.Cryptography;
using blazwiz.Core.Cryptography;
using blazwiz.Portal.Application.Services;

namespace blazwiz.Portal.Application.UnitTests.Services;

public class PasswordCryptoServiceTests
{
    private readonly PasswordCryptoService sut = new(new BouncyCastleAesGcmCryptoService());

    [Fact]
    public void GivenNullCryptoService_WhenConstructing_ThenThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new PasswordCryptoService(null!));
        Assert.Equal("cryptoService", exception.ParamName);
    }

    [Fact]
    public async Task GivenPassword_WhenEncryptingAndDecryptingWithSamePrf_ThenOriginalPasswordIsReturned()
    {
        // Arrange
        var prfHex = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

        // Act
        var encrypted = await sut.EncryptWithPrfAsync("P@ssw0rd £€ 🔑", prfHex);
        var decrypted = await sut.DecryptWithPrfAsync(encrypted, prfHex);

        // Assert
        Assert.NotEqual("P@ssw0rd £€ 🔑", encrypted);
        Assert.Equal("P@ssw0rd £€ 🔑", decrypted);
    }

    [Fact]
    public async Task GivenEncryptedPassword_WhenDecryptingWithDifferentPrf_ThenThrows()
    {
        // Arrange
        var encrypted = await sut.EncryptWithPrfAsync("secret", Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
        var otherPrfHex = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

        // Act & Assert
        await Assert.ThrowsAsync<AuthenticationTagMismatchException>(() => sut.DecryptWithPrfAsync(encrypted, otherPrfHex));
    }
}
