using System.Security.Cryptography;
using Devoctomy.Blazwiz.Auth.Services;
using Devoctomy.Blazwiz.Cryptography;

namespace Devoctomy.Blazwiz.Auth.UnitTests.Services;

public class PasswordCryptoServiceTests
{
    private readonly InMemoryDerivedKeyStore keyStore = new();
    private readonly PasswordCryptoService sut;

    public PasswordCryptoServiceTests()
    {
        sut = new PasswordCryptoService(new BouncyCastleAesGcmCryptoService(), keyStore);
    }

    [Fact]
    public void GivenNullCryptoService_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => new PasswordCryptoService(null!, keyStore));

        // Assert
        Assert.Equal("cryptoService", exception.ParamName);
    }

    [Fact]
    public void GivenNullKeyStore_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => new PasswordCryptoService(new BouncyCastleAesGcmCryptoService(), null!));

        // Assert
        Assert.Equal("keyStore", exception.ParamName);
    }

    [Fact]
    public async Task GivenRememberMeKey_WhenEncryptingAndDecrypting_ThenOriginalPasswordIsReturned()
    {
        // Arrange
        keyStore.SetKey(PasswordCryptoService.RememberMeKeyName, RandomNumberGenerator.GetBytes(32));

        // Act
        var encrypted = await sut.EncryptAsync("P@ssw0rd £€ 🔑");
        var decrypted = await sut.DecryptAsync(encrypted);

        // Assert
        Assert.NotEqual("P@ssw0rd £€ 🔑", encrypted);
        Assert.Equal("P@ssw0rd £€ 🔑", decrypted);
    }

    [Fact]
    public async Task GivenEncryptedPassword_WhenDecryptingWithDifferentRememberMeKey_ThenThrows()
    {
        // Arrange
        keyStore.SetKey(PasswordCryptoService.RememberMeKeyName, RandomNumberGenerator.GetBytes(32));
        var encrypted = await sut.EncryptAsync("secret");
        keyStore.SetKey(PasswordCryptoService.RememberMeKeyName, RandomNumberGenerator.GetBytes(32));

        // Act & Assert
        await Assert.ThrowsAsync<AuthenticationTagMismatchException>(() => sut.DecryptAsync(encrypted));
    }

    [Fact]
    public async Task GivenNoRememberMeKey_WhenEncrypting_ThenThrowsInvalidOperationException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.EncryptAsync("secret"));
    }

    [Fact]
    public async Task GivenKeysCleared_WhenDecrypting_ThenThrowsInvalidOperationException()
    {
        // Arrange
        keyStore.SetKey(PasswordCryptoService.RememberMeKeyName, RandomNumberGenerator.GetBytes(32));
        var encrypted = await sut.EncryptAsync("secret");
        keyStore.Clear();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.DecryptAsync(encrypted));
    }

    [Fact]
    public async Task GivenOnlyAnotherKey_WhenEncrypting_ThenThrowsInvalidOperationException()
    {
        // Arrange
        keyStore.SetKey("apple", RandomNumberGenerator.GetBytes(32));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.EncryptAsync("secret"));
    }
}
