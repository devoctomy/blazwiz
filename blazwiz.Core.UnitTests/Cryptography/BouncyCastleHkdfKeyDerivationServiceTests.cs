using blazwiz.Core.Cryptography;

namespace blazwiz.Core.UnitTests.Cryptography;

public class BouncyCastleHkdfKeyDerivationServiceTests
{
    private readonly BouncyCastleHkdfKeyDerivationService sut = new();

    [Theory]
    // RFC 5869 test case 1: basic test case with SHA-256.
    [InlineData(
        "0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b",
        "000102030405060708090a0b0c",
        "f0f1f2f3f4f5f6f7f8f9",
        42,
        "3cb25f25faacd57a90434f64d0362f2a2d2d0a90cf1a5a4c5db02d56ecc4c5bf34007208d5b887185865")]
    // RFC 5869 test case 3: SHA-256 with zero-length salt and info.
    [InlineData(
        "0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b",
        "",
        "",
        42,
        "8da4e775a563c18f715f802a063c5a31b8a11f5c5ee1879ec3454e5f3c738d2d9d201395faa4b61a96c8")]
    public void GivenRfc5869TestVector_WhenDeriveKey_ThenMatchesExpectedOutput(string ikmHex, string saltHex, string infoHex, int length, string expectedHex)
    {
        // Act
        var key = sut.DeriveKey(Convert.FromHexString(ikmHex), Convert.FromHexString(saltHex), Convert.FromHexString(infoHex), length);

        // Assert
        Assert.Equal(expectedHex, Convert.ToHexString(key), ignoreCase: true);
    }

    [Fact]
    public void GivenDifferentInfo_WhenDeriveKey_ThenKeysDiffer()
    {
        // Arrange
        var ikm = new byte[32];
        var salt = new byte[16];

        // Act
        var apple = sut.DeriveKey(ikm, salt, "apple"u8.ToArray(), 32);
        var orange = sut.DeriveKey(ikm, salt, "orange"u8.ToArray(), 32);

        // Assert
        Assert.NotEqual(apple, orange);
    }

    [Fact]
    public void GivenNullInputKeyMaterial_WhenDeriveKey_ThenThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => sut.DeriveKey(null!, [], [], 32));
    }

    [Fact]
    public void GivenEmptyInputKeyMaterial_WhenDeriveKey_ThenThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => sut.DeriveKey([], [], [], 32));
    }

    [Fact]
    public void GivenNullSalt_WhenDeriveKey_ThenThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => sut.DeriveKey([1], null!, [], 32));
    }

    [Fact]
    public void GivenNullInfo_WhenDeriveKey_ThenThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => sut.DeriveKey([1], [], null!, 32));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(BouncyCastleHkdfKeyDerivationService.MaxKeyLength + 1)]
    public void GivenLengthOutOfRange_WhenDeriveKey_ThenThrowsArgumentOutOfRangeException(int length)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => sut.DeriveKey([1], [], [], length));
    }
}
