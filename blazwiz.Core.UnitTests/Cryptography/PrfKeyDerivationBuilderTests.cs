using blazwiz.Core.Cryptography;
using blazwiz.Core.Cryptography.Configuration;

namespace blazwiz.Core.UnitTests.Cryptography;

public class PrfKeyDerivationBuilderTests
{
    private readonly PrfKeyDerivationOptions options = new();
    private readonly PrfKeyDerivationBuilder sut;

    public PrfKeyDerivationBuilderTests()
    {
        sut = new PrfKeyDerivationBuilder(options);
    }

    [Fact]
    public void GivenNullOptions_WhenConstructing_ThenThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new PrfKeyDerivationBuilder(null!));
        Assert.Equal("options", exception.ParamName);
    }

    [Fact]
    public void GivenNameAndGuid_WhenUseApplication_ThenOptionsAreSet()
    {
        // Act
        var result = sut.UseApplication("myapp", "6f0c6b1e-4b5a-4d8e-9a43-2c1d3e5f7a90");

        // Assert
        Assert.Same(sut, result);
        Assert.Equal("myapp", options.ApplicationName);
        Assert.Equal(Guid.Parse("6f0c6b1e-4b5a-4d8e-9a43-2c1d3e5f7a90"), options.ApplicationId);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void GivenInvalidId_WhenUseApplication_ThenThrowsArgumentException(string id)
    {
        Assert.Throws<ArgumentException>(() => sut.UseApplication("myapp", id));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void GivenInvalidName_WhenUseApplication_ThenThrowsArgumentException(string? name)
    {
        Assert.ThrowsAny<ArgumentException>(() => sut.UseApplication(name!, Guid.NewGuid().ToString()));
    }

    [Fact]
    public void GivenName_WhenAddKey_ThenKeyIsAddedWithDefaultLength()
    {
        // Act
        var result = sut.AddKey("apple");

        // Assert
        Assert.Same(sut, result);
        var key = Assert.Single(options.Keys);
        Assert.Equal("apple", key.Name);
        Assert.Equal(PrfKeyDerivationBuilder.DefaultKeyLength, key.Length);
    }

    [Fact]
    public void GivenNameAndLength_WhenAddKey_ThenKeyIsAddedWithLength()
    {
        // Act
        sut.AddKey("apple", 16);

        // Assert
        Assert.Equal(16, Assert.Single(options.Keys).Length);
    }

    [Fact]
    public void GivenKeyAlreadyAdded_WhenAddKey_ThenThrowsArgumentException()
    {
        // Arrange
        sut.AddKey("apple");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => sut.AddKey("apple"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(BouncyCastleHkdfKeyDerivationService.MaxKeyLength + 1)]
    public void GivenLengthOutOfRange_WhenAddKey_ThenThrowsArgumentOutOfRangeException(int length)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => sut.AddKey("apple", length));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void GivenInvalidName_WhenAddKey_ThenThrowsArgumentException(string? name)
    {
        Assert.ThrowsAny<ArgumentException>(() => sut.AddKey(name!));
    }
}
