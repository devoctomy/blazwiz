using System.Security.Cryptography;
using System.Text;
using blazwiz.Core.Cryptography;
using blazwiz.Core.Encryption;

namespace blazwiz.Core.UnitTests.Encryption;

public class EncryptedFileFormatTests
{
    private const string KeyId = "k_APPLE";

    private readonly EncryptedFileFormat sut = new(new BouncyCastleAesGcmAeadCipher());
    private readonly byte[] key = RandomNumberGenerator.GetBytes(32);

    [Fact]
    public void GivenNullCipher_WhenConstructing_ThenThrowsArgumentNullException()
    {
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
        Assert.NotEqual(sut.Encrypt([1, 2, 3], KeyId, key), sut.Encrypt([1, 2, 3], KeyId, key));
    }

    [Fact]
    public void GivenEmptyPlaintext_WhenEncrypt_ThenRoundTrips()
    {
        var encrypted = sut.Encrypt([], KeyId, key);
        Assert.True(sut.TryGetKeyId(encrypted, out _));
        Assert.Empty(sut.Decrypt(encrypted, key));
    }

    [Theory]
    [InlineData("")]
    [InlineData("# Just a note")]
    [InlineData("E2EE but not really")]
    public void GivenPlainContent_WhenTryGetKeyId_ThenFalse(string content)
    {
        Assert.False(sut.TryGetKeyId(Encoding.UTF8.GetBytes(content), out var keyId));
        Assert.Null(keyId);
    }

    [Fact]
    public void GivenTruncatedFile_WhenTryGetKeyId_ThenFalse()
    {
        var encrypted = sut.Encrypt([], KeyId, key);
        Assert.False(sut.TryGetKeyId(encrypted.AsSpan(0, encrypted.Length - 1), out _));
    }

    [Fact]
    public void GivenOtherVersion_WhenTryGetKeyId_ThenFalse()
    {
        var encrypted = sut.Encrypt([1], KeyId, key);
        encrypted[4] = 2;
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
        var encrypted = sut.Encrypt([1, 2, 3], KeyId, key);
        encrypted[6 + KeyId.Length] ^= 1;
        Assert.Throws<AuthenticationTagMismatchException>(() => sut.Decrypt(encrypted, key));
    }

    [Fact]
    public void GivenWrongKey_WhenDecrypt_ThenThrowsAuthenticationTagMismatchException()
    {
        var encrypted = sut.Encrypt([1, 2, 3], KeyId, key);
        Assert.Throws<AuthenticationTagMismatchException>(() => sut.Decrypt(encrypted, RandomNumberGenerator.GetBytes(32)));
    }

    [Fact]
    public void GivenPlainContent_WhenDecrypt_ThenThrowsInvalidDataException()
    {
        Assert.Throws<InvalidDataException>(() => sut.Decrypt(Encoding.UTF8.GetBytes("# Just a note"), key));
    }

    [Fact]
    public void GivenInvalidKeyId_WhenEncrypt_ThenThrowsArgumentException()
    {
        Assert.ThrowsAny<ArgumentException>(() => sut.Encrypt([1], " ", key));
        Assert.Throws<ArgumentException>(() => sut.Encrypt([1], new string('k', 256), key));
    }
}
