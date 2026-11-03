using Devoctomy.Blazwiz.Core.Configuration;

namespace Devoctomy.Blazwiz.Core.UnitTests.Configuration;

public class AppStorageKeysTests
{
    [Fact]
    public void GivenNullOptions_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("options", Assert.Throws<ArgumentNullException>(() => new AppStorageKeys(null!)).ParamName);
    }

    [Fact]
    public void GivenAppName_WhenPrefix_ThenNameAndUnderscore()
    {
        // Arrange
        var sut = new AppStorageKeys(new BlazwizAppOptions { Name = "acmeportal" });

        // Act & Assert
        Assert.Equal("acmeportal_", sut.Prefix);
    }

    [Fact]
    public void GivenShortName_WhenFor_ThenPrefixed()
    {
        // Arrange
        var sut = new AppStorageKeys(new BlazwizAppOptions { Name = "acmeportal" });

        // Act & Assert
        Assert.Equal("acmeportal_session", sut.For("session"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void GivenBlankName_WhenFor_ThenThrowsArgumentException(string name)
    {
        // Arrange
        var sut = new AppStorageKeys(new BlazwizAppOptions { Name = "acmeportal" });

        // Act & Assert
        Assert.Throws<ArgumentException>(() => sut.For(name));
    }
}
