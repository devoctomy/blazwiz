using System.Globalization;
using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Core.Text;
using Devoctomy.Blazwiz.UI.Services;
using Moq;

namespace Devoctomy.Blazwiz.UI.UnitTests.Services;

/// <summary>
/// The culture is process-wide, so every test puts it back.
/// </summary>
public sealed class CultureServiceTests : IDisposable
{
    private readonly CultureInfo culture = CultureInfo.CurrentCulture;
    private readonly CultureInfo uiCulture = CultureInfo.CurrentUICulture;
    private readonly CultureInfo? defaultCulture = CultureInfo.DefaultThreadCurrentCulture;
    private readonly CultureInfo? defaultUiCulture = CultureInfo.DefaultThreadCurrentUICulture;
    private readonly TextOptions options = new();
    private readonly Mock<ILocalStorageService> mockStorage = new();
    private readonly Mock<IBrowserInteropService> mockBrowser = new();
    private readonly Mock<INavigationService> mockNavigation = new();

    public void Dispose()
    {
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = uiCulture;
        CultureInfo.DefaultThreadCurrentCulture = defaultCulture;
        CultureInfo.DefaultThreadCurrentUICulture = defaultUiCulture;
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("textOptions", Assert.Throws<ArgumentNullException>(() => new CultureService(null!, mockStorage.Object, mockBrowser.Object, mockNavigation.Object)).ParamName);
        Assert.Equal("localStorageService", Assert.Throws<ArgumentNullException>(() => new CultureService(options, null!, mockBrowser.Object, mockNavigation.Object)).ParamName);
        Assert.Equal("browserInteropService", Assert.Throws<ArgumentNullException>(() => new CultureService(options, mockStorage.Object, null!, mockNavigation.Object)).ParamName);
        Assert.Equal("navigationService", Assert.Throws<ArgumentNullException>(() => new CultureService(options, mockStorage.Object, mockBrowser.Object, null!)).ParamName);
    }

    [Fact]
    public async Task GivenNoSupportedCultures_WhenApplySavedCultureAsync_ThenNothingChanges()
    {
        // Arrange
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");
        var sut = CreateSut();

        // Act
        await sut.ApplySavedCultureAsync();

        // Assert
        Assert.Equal("de-DE", sut.CurrentCulture.Name);
        Assert.Equal(defaultUiCulture, CultureInfo.DefaultThreadCurrentUICulture);
        mockStorage.VerifyNoOtherCalls();
        mockBrowser.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GivenSavedChoice_WhenApplySavedCultureAsync_ThenItIsApplied()
    {
        // Arrange
        Supported("en-GB", "fr-FR");
        mockStorage.Setup(x => x.GetItemAsync(CultureService.StorageKey)).ReturnsAsync("fr-fr");
        var sut = CreateSut();

        // Act
        await sut.ApplySavedCultureAsync();

        // Assert: the app's default culture, which every part of the app started afterwards uses.
        Assert.Equal("fr-FR", CultureInfo.DefaultThreadCurrentUICulture!.Name);
        Assert.Equal("fr-FR", CultureInfo.DefaultThreadCurrentCulture!.Name);
        mockBrowser.Verify(x => x.SetDocumentLanguageAsync("fr-FR"), Times.Once);
    }

    [Fact]
    public async Task GivenNoChoiceAndASupportedBrowserLanguage_WhenApplySavedCultureAsync_ThenTheBrowsersLanguage()
    {
        // Arrange
        Supported("en-GB", "fr-FR");
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-CA");
        var sut = CreateSut();

        // Act
        await sut.ApplySavedCultureAsync();

        // Assert
        Assert.Equal("fr-FR", CultureInfo.DefaultThreadCurrentUICulture!.Name);
    }

    [Fact]
    public async Task GivenUnsupportedChoiceAndBrowserLanguage_WhenApplySavedCultureAsync_ThenTheDefault()
    {
        // Arrange
        Supported("en-GB", "fr-FR");
        mockStorage.Setup(x => x.GetItemAsync(CultureService.StorageKey)).ReturnsAsync("es-ES");
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");
        var sut = CreateSut();

        // Act
        await sut.ApplySavedCultureAsync();

        // Assert
        Assert.Equal("en-GB", CultureInfo.DefaultThreadCurrentUICulture!.Name);
        mockBrowser.Verify(x => x.SetDocumentLanguageAsync("en-GB"), Times.Once);
    }

    [Fact]
    public async Task GivenSupportedCulture_WhenChangeCultureAsync_ThenKeptAndTheAppReloads()
    {
        // Arrange
        Supported("en-GB", "fr-FR");
        var sut = CreateSut();

        // Act
        await sut.ChangeCultureAsync("FR-fr");

        // Assert
        mockStorage.Verify(x => x.SetItemAsync(CultureService.StorageKey, "fr-FR"), Times.Once);
        mockNavigation.Verify(x => x.Reload(), Times.Once);
    }

    [Fact]
    public async Task GivenUnsupportedCulture_WhenChangeCultureAsync_ThenThrowsAndKeepsNothing()
    {
        // Arrange
        Supported("en-GB");
        var sut = CreateSut();

        // Act
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => sut.ChangeCultureAsync("fr-FR"));

        // Assert
        Assert.Equal("name", exception.ParamName);
        mockStorage.VerifyNoOtherCalls();
        mockNavigation.VerifyNoOtherCalls();
    }

    private void Supported(params string[] cultures) => new TextBuilder(options).SupportedCultures(cultures);

    private CultureService CreateSut() => new(options, mockStorage.Object, mockBrowser.Object, mockNavigation.Object);
}
