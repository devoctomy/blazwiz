using System.Globalization;
using Devoctomy.Blazwiz.UI.Services.Interfaces;
using Devoctomy.Blazwiz.UI.ViewModels;
using Moq;

namespace Devoctomy.Blazwiz.UI.UnitTests.ViewModels;

public class LanguageSettingsViewModelTests
{
    private readonly Mock<ICultureService> mockCultures = new();

    public LanguageSettingsViewModelTests()
    {
        mockCultures.SetupGet(x => x.CurrentCulture).Returns(CultureInfo.GetCultureInfo("en-GB"));
    }

    [Fact]
    public void GivenNullCultureService_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("cultureService", Assert.Throws<ArgumentNullException>(() => new LanguageSettingsViewModel(null!)).ParamName);
    }

    [Fact]
    public void GivenTwoCultures_WhenConstructed_ThenVisibleWithEachNamedInItsOwnLanguage()
    {
        // Arrange
        Supported("en-GB", "fr-FR");

        // Act
        var sut = new LanguageSettingsViewModel(mockCultures.Object);

        // Assert
        Assert.True(sut.IsVisible);
        Assert.Equal(["en-GB", "fr-FR"], sut.Options.Select(o => o.Name));
        Assert.StartsWith("Français", sut.Options[1].DisplayName, StringComparison.Ordinal);
        Assert.Equal("en-GB", sut.Current);
    }

    [Theory]
    [InlineData]
    [InlineData("en-GB")]
    public void GivenNoChoice_WhenConstructed_ThenHidden(params string[] cultures)
    {
        // Arrange
        Supported(cultures);

        // Act
        var sut = new LanguageSettingsViewModel(mockCultures.Object);

        // Assert
        Assert.False(sut.IsVisible);
    }

    [Fact]
    public async Task GivenAnotherCulture_WhenChangeAsync_ThenTheAppSwitches()
    {
        // Arrange
        Supported("en-GB", "fr-FR");
        var sut = new LanguageSettingsViewModel(mockCultures.Object);

        // Act
        await sut.ChangeAsync("fr-FR");

        // Assert
        mockCultures.Verify(x => x.ChangeCultureAsync("fr-FR"), Times.Once);
    }

    [Theory]
    [InlineData("en-GB")]
    [InlineData("")]
    [InlineData(null)]
    public async Task GivenTheCurrentCultureOrNone_WhenChangeAsync_ThenNothingHappens(string? name)
    {
        // Arrange
        Supported("en-GB", "fr-FR");
        var sut = new LanguageSettingsViewModel(mockCultures.Object);

        // Act
        await sut.ChangeAsync(name);

        // Assert
        mockCultures.Verify(x => x.ChangeCultureAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void GivenCultureWithoutNativeName_WhenConstructed_ThenEmptyDisplayName()
    {
        // Arrange
        mockCultures.SetupGet(x => x.SupportedCultures).Returns(new List<CultureInfo> { new UnnamedCulture() });

        // Act
        var sut = new LanguageSettingsViewModel(mockCultures.Object);

        // Assert
        Assert.Equal(string.Empty, Assert.Single(sut.Options).DisplayName);
    }

    private void Supported(params string[] cultures) =>
        mockCultures.SetupGet(x => x.SupportedCultures).Returns(cultures.Select(CultureInfo.GetCultureInfo).ToList());

    private sealed class UnnamedCulture : CultureInfo
    {
        public UnnamedCulture()
            : base("en-GB")
        {
        }

        public override string NativeName => string.Empty;
    }
}
