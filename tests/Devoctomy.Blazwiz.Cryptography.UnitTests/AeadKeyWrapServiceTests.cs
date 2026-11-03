using System.Security.Cryptography;
using Devoctomy.Blazwiz.Cryptography;

namespace Devoctomy.Blazwiz.Cryptography.UnitTests;

public class AeadKeyWrapServiceTests
{
    private readonly AeadKeyWrapService sut = new(new BouncyCastleAesGcmAeadCipher());
    private readonly byte[] kek = RandomNumberGenerator.GetBytes(32);

    [Fact]
    public void GivenNullCipher_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("cipher", Assert.Throws<ArgumentNullException>(() => new AeadKeyWrapService(null!)).ParamName);
    }

    [Fact]
    public void GivenNoLength_WhenCreateKey_ThenRandom32ByteKeys()
    {
        // Act
        var first = sut.CreateKey();
        var second = sut.CreateKey();

        // Assert
        Assert.Equal(32, first.Length);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void GivenLength_WhenCreateKey_ThenKeyHasThatLength()
    {
        // Act & Assert
        Assert.Equal(16, sut.CreateKey(16).Length);
        Assert.Throws<ArgumentOutOfRangeException>(() => sut.CreateKey(0));
    }

    [Fact]
    public void GivenKey_WhenWrapThenUnwrap_ThenRoundTripsWithBase64Fields()
    {
        // Arrange
        var key = sut.CreateKey();

        // Act
        var wrapped = sut.Wrap(kek, key, [7]);
        var unwrapped = sut.Unwrap(kek, wrapped, [7]);

        // Assert
        Assert.Equal("aes-256-gcm", wrapped.Algorithm);
        Assert.Equal(12, Convert.FromBase64String(wrapped.Nonce).Length);
        Assert.Equal(48, Convert.FromBase64String(wrapped.Wrapped).Length);
        Assert.Equal(key, unwrapped);
    }

    [Fact]
    public void GivenSameKey_WhenWrapTwice_ThenNewNonceEachTime()
    {
        // Act
        var key = sut.CreateKey();

        // Assert
        Assert.NotEqual(sut.Wrap(kek, key).Nonce, sut.Wrap(kek, key).Nonce);
    }

    [Fact]
    public void GivenWrongKekOrAssociatedData_WhenUnwrap_ThenThrowsAuthenticationTagMismatchException()
    {
        // Arrange
        var wrapped = sut.Wrap(kek, sut.CreateKey(), [1]);

        // Act & Assert
        Assert.Throws<AuthenticationTagMismatchException>(() => sut.Unwrap(sut.CreateKey(), wrapped, [1]));
        Assert.Throws<AuthenticationTagMismatchException>(() => sut.Unwrap(kek, wrapped, [2]));
    }

    [Fact]
    public void GivenOtherAlgorithm_WhenUnwrap_ThenThrowsNotSupportedException()
    {
        // Arrange
        var wrapped = sut.Wrap(kek, sut.CreateKey());
        wrapped.Algorithm = "rot13";

        // Act & Assert
        Assert.Throws<NotSupportedException>(() => sut.Unwrap(kek, wrapped));
    }

    [Fact]
    public void GivenBadNonce_WhenUnwrap_ThenThrowsFormatException()
    {
        // Arrange
        var wrapped = sut.Wrap(kek, sut.CreateKey());
        wrapped.Nonce = Convert.ToBase64String(new byte[8]);

        // Act & Assert
        Assert.Throws<FormatException>(() => sut.Unwrap(kek, wrapped));
    }
}
