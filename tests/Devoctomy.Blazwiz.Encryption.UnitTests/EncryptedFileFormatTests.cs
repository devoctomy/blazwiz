using System.Security.Cryptography;
using System.Text;
using Devoctomy.Blazwiz.Cryptography;
using Devoctomy.Blazwiz.Encryption;

namespace Devoctomy.Blazwiz.Encryption.UnitTests;

public class EncryptedFileFormatTests
{
    private const string KeyId = "k_APPLE";

    private readonly EncryptedFileFormat sut = new(new BouncyCastleAesGcmAeadCipher());
    private readonly byte[] key = RandomNumberGenerator.GetBytes(32);

    [Fact]
    public void GivenNullCipher_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("cipher", Assert.Throws<ArgumentNullException>(() => new EncryptedFileFormat(null!)).ParamName);
    }

    [Fact]
    public void GivenPlaintext_WhenEncrypt_ThenHeaderIsReadableAndDecryptRoundTrips()
    {
        // Arrange
        var plaintext = Encoding.UTF8.GetBytes("# Shopping\n\n- apples\n- oranges");

        // Act
        var encrypted = sut.Encrypt(plaintext, KeyId, key);

        // Assert
        Assert.Equal("E2EE"u8.ToArray(), encrypted.Take(4));
        Assert.Equal(EncryptedFileFormat.Version, encrypted[4]);
        Assert.Equal(KeyId.Length, encrypted[5]);
        Assert.Equal(KeyId, Encoding.UTF8.GetString(encrypted, 6, KeyId.Length));
        Assert.Equal(6 + KeyId.Length + 12 + plaintext.Length + 16, encrypted.Length);
        Assert.True(sut.TryGetKeyId(encrypted, out var keyId));
        Assert.Equal(KeyId, keyId);
        Assert.Equal(plaintext, sut.Decrypt(encrypted, key));
    }

    [Fact]
    public void GivenSamePlaintext_WhenEncryptTwice_ThenDifferentNonceAndCiphertext()
    {
        // Act & Assert
        Assert.NotEqual(sut.Encrypt([1, 2, 3], KeyId, key), sut.Encrypt([1, 2, 3], KeyId, key));
    }

    [Fact]
    public void GivenEmptyPlaintext_WhenEncrypt_ThenRoundTrips()
    {
        // Act
        var encrypted = sut.Encrypt([], KeyId, key);

        // Assert
        Assert.True(sut.TryGetKeyId(encrypted, out _));
        Assert.Empty(sut.Decrypt(encrypted, key));
    }

    [Theory]
    [InlineData("")]
    [InlineData("# Just a note")]
    [InlineData("E2EE but not really")]
    public void GivenPlainContent_WhenTryGetKeyId_ThenFalse(string content)
    {
        // Act & Assert
        Assert.False(sut.TryGetKeyId(Encoding.UTF8.GetBytes(content), out var keyId));
        Assert.Null(keyId);
    }

    [Fact]
    public void GivenTruncatedFile_WhenTryGetKeyId_ThenFalse()
    {
        // Act
        var encrypted = sut.Encrypt([], KeyId, key);

        // Assert
        Assert.False(sut.TryGetKeyId(encrypted.AsSpan(0, encrypted.Length - 1), out _));
    }

    [Fact]
    public void GivenOtherVersion_WhenTryGetKeyId_ThenFalse()
    {
        // Arrange
        var encrypted = sut.Encrypt([1], KeyId, key);

        // Act
        encrypted[4] = 2;

        // Assert
        Assert.False(sut.TryGetKeyId(encrypted, out _));
    }

    [Fact]
    public void GivenHeaderChanged_WhenDecrypt_ThenThrowsAuthenticationTagMismatchException()
    {
        // Arrange: the key id is readable, but changing it breaks the tag.
        var encrypted = sut.Encrypt([1, 2, 3], KeyId, key);
        encrypted[6] = (byte)'x';

        // Act & Assert
        Assert.Throws<AuthenticationTagMismatchException>(() => sut.Decrypt(encrypted, key));
    }

    [Fact]
    public void GivenNonceChanged_WhenDecrypt_ThenThrowsAuthenticationTagMismatchException()
    {
        // Arrange
        var encrypted = sut.Encrypt([1, 2, 3], KeyId, key);
        encrypted[6 + KeyId.Length] ^= 1;

        // Act & Assert
        Assert.Throws<AuthenticationTagMismatchException>(() => sut.Decrypt(encrypted, key));
    }

    [Fact]
    public void GivenWrongKey_WhenDecrypt_ThenThrowsAuthenticationTagMismatchException()
    {
        // Arrange
        var encrypted = sut.Encrypt([1, 2, 3], KeyId, key);

        // Act & Assert
        Assert.Throws<AuthenticationTagMismatchException>(() => sut.Decrypt(encrypted, RandomNumberGenerator.GetBytes(32)));
    }

    [Fact]
    public void GivenPlainContent_WhenDecrypt_ThenThrowsInvalidDataException()
    {
        // Act & Assert
        Assert.Throws<InvalidDataException>(() => sut.Decrypt(Encoding.UTF8.GetBytes("# Just a note"), key));
    }

    [Fact]
    public void GivenInvalidKeyId_WhenEncrypt_ThenThrowsArgumentException()
    {
        // Act & Assert
        Assert.ThrowsAny<ArgumentException>(() => sut.Encrypt([1], " ", key));
        Assert.Throws<ArgumentException>(() => sut.Encrypt([1], new string('k', 256), key));
    }
}
