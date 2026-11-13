using System.Security.Cryptography;
using System.Text;
using Devoctomy.Blazwiz.Cryptography;
using Devoctomy.Blazwiz.Encryption;
using Devoctomy.Blazwiz.Encryption.Models;

namespace Devoctomy.Blazwiz.Encryption.UnitTests;

public class EncryptedFileFormatTests
{
    private const string KeyId = "k_APPLE";
    private const string Scope = "0f8fad5b-d9cb-469f-a165-70867728950e";
    private const string Path = "profiles/0f8fad5b-d9cb-469f-a165-70867728950e/a.txt";

    private readonly EncryptedFileFormat sut = new(new BouncyCastleAesGcmAeadCipher());
    private readonly byte[] key = RandomNumberGenerator.GetBytes(32);
    private readonly byte[] plaintext = Encoding.UTF8.GetBytes("secret");

    [Fact]
    public void GivenABinding_WhenEncrypted_ThenTheHeaderHasTheKeyAndScopeAndItRoundTrips()
    {
        // Act
        var encrypted = sut.Encrypt(plaintext, KeyId, key, new E2eFileBinding(Scope, Path));

        // Assert
        Assert.Equal(EncryptedFileFormat.Version, encrypted[4]);
        Assert.True(sut.TryGetKeyId(encrypted, out var keyId));
        Assert.Equal(KeyId, keyId);
        Assert.Contains(Scope, Encoding.UTF8.GetString(encrypted), StringComparison.Ordinal);
        Assert.DoesNotContain("a.txt", Encoding.UTF8.GetString(encrypted), StringComparison.Ordinal);
        Assert.Equal(plaintext, sut.Decrypt(encrypted, key, new E2eFileBinding(Scope, Path)));
    }

    [Fact]
    public void GivenABoundFile_WhenDecryptedAtAnotherPath_ThenFails()
    {
        // Arrange
        var encrypted = sut.Encrypt(plaintext, KeyId, key, new E2eFileBinding(Scope, Path));

        // Act & Assert
        Assert.ThrowsAny<CryptographicException>(() => sut.Decrypt(encrypted, key, new E2eFileBinding(Scope, Path + ".moved")));
    }

    [Fact]
    public void GivenABoundFile_WhenDecryptedForAnotherScope_ThenThrowsInvalidData()
    {
        // Arrange
        var encrypted = sut.Encrypt(plaintext, KeyId, key, new E2eFileBinding(Scope, Path));

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => sut.Decrypt(encrypted, key, new E2eFileBinding(Guid.NewGuid().ToString("D"), Path)));
    }

    [Fact]
    public void GivenAnEditedHeaderScope_WhenDecrypted_ThenFails()
    {
        // Arrange: the same length scope id, so the header still parses.
        var other = "1f8fad5b-d9cb-469f-a165-70867728950e";
        var encrypted = sut.Encrypt(plaintext, KeyId, key, new E2eFileBinding(Scope, Path));
        var text = Encoding.Latin1.GetString(encrypted).Replace(Scope, other, StringComparison.Ordinal);
        var edited = Encoding.Latin1.GetBytes(text);

        // Act & Assert
        Assert.ThrowsAny<CryptographicException>(() => sut.Decrypt(edited, key, new E2eFileBinding(other, Path)));
    }

    [Fact]
    public void GivenContentThatIsNotAnEncryptedFile_WhenRead_ThenNoKeyIdAndDecryptThrows()
    {
        // Act & Assert
        Assert.False(sut.TryGetKeyId(plaintext, out var keyId));
        Assert.Null(keyId);
        Assert.Throws<InvalidDataException>(() => sut.Decrypt(plaintext, key, new E2eFileBinding(Scope, Path)));
    }

    [Theory]
    [InlineData("4532454501")] // Magic and version only: no key id length.
    [InlineData("453245450100")] // A zero length key id.
    [InlineData("45324545010561")] // A key id longer than the content.
    [InlineData("4532454501016B")] // A key id but no scope id.
    [InlineData("4532454501016B00")] // A key id and a zero length scope id.
    [InlineData("4532454501016BFF")] // A scope id longer than the content.
    [InlineData("4532454501FF")] // A key id length with nothing after it.
    [InlineData("45324545010180")] // A key id that is not valid UTF-8.
    [InlineData("4532454501016B0180")] // A scope id that is not valid UTF-8.
    public void GivenAMalformedHeader_WhenRead_ThenNoKeyIdAndDecryptThrowsInvalidData(string hex)
    {
        // Arrange
        var content = Convert.FromHexString(hex);

        // Act
        var found = sut.TryGetKeyId(content, out var keyId);

        // Assert
        Assert.False(found);
        Assert.Null(keyId);
        Assert.Throws<InvalidDataException>(() => sut.Decrypt(content, key, new E2eFileBinding(Scope, Path)));
    }

    [Fact]
    public void GivenAFileCutShortBeforeItsNonceAndTag_WhenRead_ThenNoKeyIdAndDecryptThrowsInvalidData()
    {
        // Arrange: the header's strings are whole, but there is not room for the nonce and tag after them.
        var encrypted = sut.Encrypt(plaintext, KeyId, key, new E2eFileBinding(Scope, Path));
        var headerStrings = 4 + 1 + 1 + Encoding.UTF8.GetByteCount(KeyId) + 1 + Encoding.UTF8.GetByteCount(Scope);
        var truncated = encrypted.AsSpan(0, headerStrings + 4).ToArray();

        // Act
        var found = sut.TryGetKeyId(truncated, out var keyId);

        // Assert
        Assert.False(found);
        Assert.Null(keyId);
        Assert.Throws<InvalidDataException>(() => sut.Decrypt(truncated, key, new E2eFileBinding(Scope, Path)));
    }

    [Fact]
    public void GivenNullCipher_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("cipher", Assert.Throws<ArgumentNullException>(() => new EncryptedFileFormat(null!)).ParamName);
    }

    [Fact]
    public void GivenAnInvalidBinding_WhenUsed_ThenThrows()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => sut.Encrypt(plaintext, KeyId, key, null!));
        Assert.ThrowsAny<ArgumentException>(() => sut.Encrypt(plaintext, new string('k', 256), key, new E2eFileBinding(Scope, Path)));
        Assert.Throws<ArgumentNullException>(() => sut.Decrypt(plaintext, key, null!));
        Assert.ThrowsAny<ArgumentException>(() => new E2eFileBinding(" ", Path));
        Assert.Throws<ArgumentNullException>(() => new E2eFileBinding(Scope, null!));
    }
}
