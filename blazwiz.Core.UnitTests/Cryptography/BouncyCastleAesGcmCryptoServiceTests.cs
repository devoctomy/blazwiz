using System.Security.Cryptography;
using System.Text;
using blazwiz.Core.Cryptography;

namespace blazwiz.Core.UnitTests.Cryptography;

public class BouncyCastleAesGcmCryptoServiceTests
{
    private readonly BouncyCastleAesGcmCryptoService sut;
    private readonly string testKey;

    public BouncyCastleAesGcmCryptoServiceTests()
    {
        this.sut = new BouncyCastleAesGcmCryptoService();
        byte[] keyBytes = RandomNumberGenerator.GetBytes(32);
        this.testKey = Convert.ToBase64String(keyBytes);
    }

    [Fact]
    public async Task GivenPlainTextData_WhenEncryptingAndDecrypting_ThenOriginalDataIsReturned()
    {
        // Arrange
        string originalText = "Hello, World!";
        byte[] originalData = Encoding.UTF8.GetBytes(originalText);

        using var inputStream = new MemoryStream(originalData);
        using var encryptedStream = new MemoryStream();
        using var decryptedStream = new MemoryStream();

        // Act
        await this.sut.EncryptAsync(inputStream, encryptedStream, this.testKey);
        encryptedStream.Position = 0;
        await this.sut.DecryptAsync(encryptedStream, decryptedStream, this.testKey);

        // Assert
        string decryptedText = Encoding.UTF8.GetString(decryptedStream.ToArray());
        Assert.Equal(originalText, decryptedText);
    }

    [Fact]
    public async Task GivenPlainTextData_AndWrongDecryptionKey_WhenEncryptingAndDecrypting_ThenExceptionThrown()
    {
        // Arrange
        string originalText = "Hello, World!";
        byte[] originalData = Encoding.UTF8.GetBytes(originalText);
        byte[] wrongKeyBytes = new byte[32];

        using var inputStream = new MemoryStream(originalData);
        using var encryptedStream = new MemoryStream();
        using var decryptedStream = new MemoryStream();

        // Assert
        await this.sut.EncryptAsync(inputStream, encryptedStream, this.testKey);
        encryptedStream.Position = 0;
        await Assert.ThrowsAnyAsync<CryptographicException>(async () =>
        {
            await this.sut.DecryptAsync(encryptedStream, decryptedStream, Convert.ToBase64String(wrongKeyBytes));
        });
    }

    [Fact]
    public async Task GivenLargeDataStream_WhenEncryptingAndDecrypting_ThenDataIsPreservedCorrectly()
    {
        // Arrange
        byte[] largeData = RandomNumberGenerator.GetBytes(1024 * 1024);
        using var inputStream = new MemoryStream(largeData);
        using var encryptedStream = new MemoryStream();
        using var decryptedStream = new MemoryStream();

        // Act
        await this.sut.EncryptAsync(inputStream, encryptedStream, this.testKey);
        encryptedStream.Position = 0;
        await this.sut.DecryptAsync(encryptedStream, decryptedStream, this.testKey);

        // Assert
        Assert.Equal(largeData, decryptedStream.ToArray());
    }

    [Fact]
    public async Task GivenTamperedData_WhenDecrypting_ThenThrowsAuthenticationTagMismatchException()
    {
        // Arrange
        string originalText = "Test Data";
        byte[] originalData = Encoding.UTF8.GetBytes(originalText);

        using var inputStream = new MemoryStream(originalData);
        using var encryptedStream = new MemoryStream();
        using var decryptedStream = new MemoryStream();

        // Act - First encrypt the data
        await this.sut.EncryptAsync(inputStream, encryptedStream, this.testKey);

        // Tamper with the encrypted data
        byte[] tamperedData = encryptedStream.ToArray();
        if (tamperedData.Length > 30)
        {
            tamperedData[29] ^= 0x01;
        }

        using var tamperedStream = new MemoryStream(tamperedData);

        // Assert
        await Assert.ThrowsAsync<AuthenticationTagMismatchException>(
            async () => await this.sut.DecryptAsync(tamperedStream, decryptedStream, this.testKey));
    }

    [Theory]
    [InlineData(null)]
    public async Task GivenInvalidKey_WhenEncrypting_ThenThrowsArgumentNullException(string? invalidKey)
    {
        // Arrange
        using var inputStream = new MemoryStream();
        using var outputStream = new MemoryStream();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await this.sut.EncryptAsync(inputStream, outputStream, invalidKey));
    }

    [Theory]
    [InlineData("")]
    public async Task GivenInvalidKey_WhenEncrypting_ThenThrowsArgumentException(string? invalidKey)
    {
        // Arrange
        using var inputStream = new MemoryStream();
        using var outputStream = new MemoryStream();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await this.sut.EncryptAsync(inputStream, outputStream, invalidKey));
    }

    [Theory]
    [InlineData(null)]
    public async Task GivenInvalidKey_WhenDecrypting_ThenThrowsArgumentNullException(string? invalidKey)
    {
        // Arrange
        using var inputStream = new MemoryStream();
        using var outputStream = new MemoryStream();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await this.sut.DecryptAsync(inputStream, outputStream, invalidKey));
    }

    [Theory]
    [InlineData("")]
    public async Task GivenInvalidKey_WhenDecrypting_ThenThrowsArgumentException(string? invalidKey)
    {
        // Arrange
        using var inputStream = new MemoryStream();
        using var outputStream = new MemoryStream();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await this.sut.DecryptAsync(inputStream, outputStream, invalidKey));
    }

    [Fact]
    public async Task GivenShortNonceStream_WhenDecrypting_ThenThrowsInvalidOperationException()
    {
        // Arrange
        byte[] shortData = new byte[5];
        using var inputStream = new MemoryStream(shortData);
        using var outputStream = new MemoryStream();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await this.sut.DecryptAsync(inputStream, outputStream, this.testKey));

        Assert.Equal("Failed to read nonce from input stream. Expected 12 bytes but got 5.", ex.Message);
    }

    [Fact]
    public async Task GivenEmptyStream_WhenDecrypting_ThenThrowsInvalidOperationException()
    {
        // Arrange
        using var inputStream = new MemoryStream();
        using var outputStream = new MemoryStream();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await this.sut.DecryptAsync(inputStream, outputStream, this.testKey));

        Assert.Equal("Failed to read nonce from input stream. Expected 12 bytes but got 0.", ex.Message);
    }

    [Fact]
    public async Task GivenDataTooShortForTag_WhenDecrypting_ThenThrowsInvalidOperationException()
    {
        // Arrange
        // Create data that has valid nonce (12 bytes) but not enough data for tag (16 bytes)
        byte[] shortData = new byte[20]; // 12 bytes nonce + 8 bytes data (less than 16 byte tag)
        using var inputStream = new MemoryStream(shortData);
        using var outputStream = new MemoryStream();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await this.sut.DecryptAsync(inputStream, outputStream, this.testKey));

        Assert.Equal("Input data is too short to contain authentication tag.", ex.Message);
    }
}
