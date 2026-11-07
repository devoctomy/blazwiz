using Devoctomy.Blazwiz.UI.Models;
using Microsoft.Extensions.Configuration;

namespace Devoctomy.Blazwiz.UI.UnitTests.Models;

public class AppSettingsTests
{
    [Fact]
    public void GivenNoLogo_WhenCreated_ThenTheTemplatesIconIsTheLogo()
    {
        // Act
        var settings = new AppSettings();

        // Assert
        Assert.Equal("images/icon.svg", settings.LogoUrl);
        Assert.Equal(AppSettings.DefaultLogoUrl, settings.LogoUrl);
    }

    [Fact]
    public void GivenALogoInConfiguration_WhenBound_ThenItIsTheLogo()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AppSettings:LogoUrl"] = "images/logo.png" })
            .Build();

        // Act
        var settings = configuration.GetSection("AppSettings").Get<AppSettings>()!;

        // Assert
        Assert.Equal("images/logo.png", settings.LogoUrl);
    }
}
