using blazwiz.Core.Configuration;

namespace blazwiz.Core.UnitTests.Configuration;

public class AppStorageKeysTests
{
    [Fact]
    public void GivenNullOptions_WhenConstructing_ThenThrowsArgumentNullException()
    {
        Assert.Equal("options", Assert.Throws<ArgumentNullException>(() => new AppStorageKeys(null!)).ParamName);
    }

    [Fact]
    public void GivenAppName_WhenPrefix_ThenNameAndUnderscore()
    {
        var sut = new AppStorageKeys(new BlazwizAppOptions { Name = "acmeportal" });

        Assert.Equal("acmeportal_", sut.Prefix);
    }

    [Fact]
    public void GivenShortName_WhenFor_ThenPrefixed()
    {
        var sut = new AppStorageKeys(new BlazwizAppOptions { Name = "acmeportal" });

        Assert.Equal("acmeportal_session", sut.For("session"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void GivenBlankName_WhenFor_ThenThrowsArgumentException(string name)
    {
        var sut = new AppStorageKeys(new BlazwizAppOptions { Name = "acmeportal" });

        Assert.Throws<ArgumentException>(() => sut.For(name));
    }
}
