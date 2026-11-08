using System.Security.Cryptography;
using Devoctomy.Blazwiz.Auth.Services;
using Devoctomy.Blazwiz.Cryptography;

namespace Devoctomy.Blazwiz.Auth.UnitTests.Services;

public class PasswordCryptoServiceTests
{
    private const string Email = "bob@example.com";

    private readonly InMemoryDerivedKeyStore keyStore = new();
    private readonly PasswordCryptoService sut;

    public PasswordCryptoServiceTests()
    {
        sut = new PasswordCryptoService(new BouncyCastleAesGcmAeadCipher(), keyStore);
    }

    [Fact]
    public void GivenNullCipher_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => new PasswordCryptoService(null!, keyStore));

        // Assert
        Assert.Equal("cipher", exception.ParamName);
    }

    [Fact]
    public void GivenNullKeyStore_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => new PasswordCryptoService(new BouncyCastleAesGcmAeadCipher(), null!));

        // Assert
        Assert.Equal("keyStore", exception.ParamName);
    }

    [Fact]
    public async Task GivenRememberMeKey_WhenEncryptingAndDecrypting_ThenOriginalPasswordIsReturned()
    {
        // Arrange
        SetKey();

        // Act
        var encrypted = await sut.EncryptAsync("P@ssw0rd £€ 🔑", Email);
        var decrypted = await sut.DecryptAsync(encrypted, Email);

        // Assert
        Assert.NotEqual("P@ssw0rd £€ 🔑", encrypted);
        Assert.Equal("P@ssw0rd £€ 🔑", decrypted);
    }

    [Fact]
    public async Task GivenTheSamePassword_WhenEncryptedTwice_ThenTheCiphertextsDiffer()
    {
        // Arrange
        SetKey();

        // Act
        var first = await sut.EncryptAsync("secret", Email);
        var second = await sut.EncryptAsync("secret", Email);

        // Assert
        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task GivenEncryptedPassword_WhenDecryptingWithDifferentRememberMeKey_ThenThrows()
    {
        // Arrange
        SetKey();
        var encrypted = await sut.EncryptAsync("secret", Email);
        SetKey();

        // Act & Assert
        await Assert.ThrowsAsync<AuthenticationTagMismatchException>(() => sut.DecryptAsync(encrypted, Email));
    }

    [Fact]
    public async Task GivenEncryptedPassword_WhenDecryptingForAnotherUser_ThenThrows()
    {
        // Arrange: the password is bound to the user it was saved for.
        SetKey();
        var encrypted = await sut.EncryptAsync("secret", Email);

        // Act & Assert
        await Assert.ThrowsAsync<AuthenticationTagMismatchException>(() => sut.DecryptAsync(encrypted, "alice@example.com"));
    }

    [Fact]
    public async Task GivenEncryptedPassword_WhenDecryptingWithTheEmailInAnotherCaseOrPadded_ThenDecrypts()
    {
        // Arrange: email addresses are matched without regard to case everywhere else.
        SetKey();
        var encrypted = await sut.EncryptAsync("secret", Email);

        // Act & Assert
        Assert.Equal("secret", await sut.DecryptAsync(encrypted, " Bob@Example.COM "));
    }

    [Theory]
    [InlineData(10)]
    [InlineData(27)]
    public async Task GivenEncryptedPassword_WhenAByteIsChanged_ThenThrows(int index)
    {
        // Arrange
        SetKey();
        var bytes = Convert.FromBase64String(await sut.EncryptAsync("secret", Email));
        bytes[index] ^= 0x01;

        // Act & Assert
        await Assert.ThrowsAsync<AuthenticationTagMismatchException>(() => sut.DecryptAsync(Convert.ToBase64String(bytes), Email));
    }

    [Fact]
    public async Task GivenTooShortAValue_WhenDecrypting_ThenThrows()
    {
        // Arrange
        SetKey();

        // Act & Assert
        await Assert.ThrowsAsync<AuthenticationTagMismatchException>(() => sut.DecryptAsync(Convert.ToBase64String(new byte[5]), Email));
    }

    [Fact]
    public async Task GivenNoRememberMeKey_WhenEncrypting_ThenThrowsInvalidOperationException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.EncryptAsync("secret", Email));
    }

    [Fact]
    public async Task GivenNoRememberMeKey_WhenDecrypting_ThenThrowsInvalidOperationException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.DecryptAsync(Convert.ToBase64String(new byte[64]), Email));
    }

    [Fact]
    public async Task GivenMissingArguments_WhenCalled_ThenThrows()
    {
        // Arrange
        SetKey();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.EncryptAsync(null!, Email));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => sut.EncryptAsync("secret", " "));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => sut.DecryptAsync(string.Empty, Email));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => sut.DecryptAsync("AAAA", null!));
    }

    private void SetKey() => keyStore.SetKey(PasswordCryptoService.RememberMeKeyName, RandomNumberGenerator.GetBytes(32));
}
