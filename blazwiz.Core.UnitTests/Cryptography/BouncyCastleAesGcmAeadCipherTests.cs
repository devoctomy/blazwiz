using System.Security.Cryptography;
using System.Text;
using blazwiz.Core.Cryptography;

namespace blazwiz.Core.UnitTests.Cryptography;

public class BouncyCastleAesGcmAeadCipherTests
{
    private readonly BouncyCastleAesGcmAeadCipher sut = new();
    private readonly byte[] key = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
    private readonly byte[] nonce = Enumerable.Range(100, 12).Select(i => (byte)i).ToArray();

    [Fact]
    public void GivenCipher_WhenGettingSizes_ThenAes256GcmSizes()
    {
        Assert.Equal(32, sut.KeySize);
        Assert.Equal(12, sut.NonceSize);
        Assert.Equal(16, sut.TagSize);
    }

    [Fact]
    public void GivenPlaintextAndAssociatedData_WhenEncryptThenDecrypt_ThenRoundTrips()
    {
        // Arrange
        var plaintext = Encoding.UTF8.GetBytes("apple orange banana");
        var associatedData = Encoding.UTF8.GetBytes("header");

        // Act
        var ciphertext = sut.Encrypt(key, nonce, plaintext, associatedData);
        var decrypted = sut.Decrypt(key, nonce, ciphertext, associatedData);

        // Assert
        Assert.Equal(plaintext.Length + 16, ciphertext.Length);
        Assert.NotEqual(plaintext, ciphertext.Take(plaintext.Length));
        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void GivenSameInputs_WhenEncrypt_ThenMatchesDotNetAesGcm()
    {
        // Arrange
        var plaintext = Encoding.UTF8.GetBytes("apple");
        var associatedData = Encoding.UTF8.GetBytes("orange");
        var expectedCiphertext = new byte[plaintext.Length];
        var expectedTag = new byte[16];
        using (var aes = new AesGcm(key, 16))
        {
            aes.Encrypt(nonce, plaintext, expectedCiphertext, expectedTag, associatedData);
        }

        // Act
        var result = sut.Encrypt(key, nonce, plaintext, associatedData);

        // Assert
        Assert.Equal(expectedCiphertext.Concat(expectedTag), result);
    }

    [Fact]
    public void GivenEmptyPlaintext_WhenEncryptThenDecrypt_ThenRoundTrips()
    {
        var ciphertext = sut.Encrypt(key, nonce, []);
        Assert.Equal(16, ciphertext.Length);
        Assert.Empty(sut.Decrypt(key, nonce, ciphertext));
    }

    [Fact]
    public void GivenWrongKey_WhenDecrypt_ThenThrowsAuthenticationTagMismatchException()
    {
        // Arrange
        var ciphertext = sut.Encrypt(key, nonce, [1, 2, 3]);
        var wrongKey = (byte[])key.Clone();
        wrongKey[0] ^= 1;

        // Act & Assert
        Assert.Throws<AuthenticationTagMismatchException>(() => sut.Decrypt(wrongKey, nonce, ciphertext));
    }

    [Fact]
    public void GivenDifferentAssociatedData_WhenDecrypt_ThenThrowsAuthenticationTagMismatchException()
    {
        // Arrange
        var ciphertext = sut.Encrypt(key, nonce, [1, 2, 3], [9]);

        // Act & Assert
        Assert.Throws<AuthenticationTagMismatchException>(() => sut.Decrypt(key, nonce, ciphertext, [8]));
        Assert.Throws<AuthenticationTagMismatchException>(() => sut.Decrypt(key, nonce, ciphertext));
    }

    [Fact]
    public void GivenChangedCiphertext_WhenDecrypt_ThenThrowsAuthenticationTagMismatchException()
    {
        // Arrange
        var ciphertext = sut.Encrypt(key, nonce, [1, 2, 3]);
        ciphertext[1] ^= 1;

        // Act & Assert
        Assert.Throws<AuthenticationTagMismatchException>(() => sut.Decrypt(key, nonce, ciphertext));
    }

    [Fact]
    public void GivenCiphertextShorterThanTag_WhenDecrypt_ThenThrowsAuthenticationTagMismatchException()
    {
        Assert.Throws<AuthenticationTagMismatchException>(() => sut.Decrypt(key, nonce, new byte[15]));
    }

    [Theory]
    [InlineData(16)]
    [InlineData(31)]
    [InlineData(33)]
    public void GivenWrongKeyLength_WhenEncrypt_ThenThrowsArgumentException(int length)
    {
        Assert.Throws<ArgumentException>(() => sut.Encrypt(new byte[length], nonce, [1]));
    }

    [Theory]
    [InlineData(8)]
    [InlineData(16)]
    public void GivenWrongNonceLength_WhenEncrypt_ThenThrowsArgumentException(int length)
    {
        Assert.Throws<ArgumentException>(() => sut.Encrypt(key, new byte[length], [1]));
    }
}
