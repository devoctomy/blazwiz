using Devoctomy.Blazwiz.Browser.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Moq;

namespace Devoctomy.Blazwiz.Browser.UnitTests.Services;

public class BrowserInteropServiceTests
{
    private readonly Mock<IJSRuntime> mockJs = new();
    private readonly Mock<IJSObjectReference> mockModule = new();

    public BrowserInteropServiceTests()
    {
        mockJs.Setup(x => x.InvokeAsync<IJSObjectReference>("import", It.Is<object?[]>(a => (string?)a[0] == BrowserInteropService.ModulePath)))
            .ReturnsAsync(mockModule.Object);
    }

    [Fact]
    public void GivenNullJsRuntime_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("jsRuntime", Assert.Throws<ArgumentNullException>(() => new BrowserInteropService(null!)).ParamName);
    }

    [Fact]
    public async Task GivenTheme_WhenSetThemeAsync_ThenModuleSetsIt()
    {
        // Arrange
        var sut = new BrowserInteropService(mockJs.Object);

        // Act
        await sut.SetThemeAsync("dark");

        // Assert
        mockModule.Verify(x => x.InvokeAsync<IJSVoidResult>("setTheme", It.Is<object?[]>(a => (string?)a[0] == "dark")), Times.Once);
    }

    [Fact]
    public async Task GivenBrowserPrefersDark_WhenGetPreferredThemeAsync_ThenDarkAndModuleImportedOnce()
    {
        // Arrange
        mockModule.Setup(x => x.InvokeAsync<string>("getPreferredTheme", It.IsAny<object?[]>())).ReturnsAsync("dark");
        var sut = new BrowserInteropService(mockJs.Object);

        // Act
        var first = await sut.GetPreferredThemeAsync();
        await sut.GetPreferredThemeAsync();

        // Assert
        Assert.Equal("dark", first);
        mockJs.Verify(x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object?[]>()), Times.Once);
    }

    [Fact]
    public async Task GivenLanguage_WhenSetDocumentLanguageAsync_ThenModuleSetsIt()
    {
        // Arrange
        var sut = new BrowserInteropService(mockJs.Object);

        // Act
        await sut.SetDocumentLanguageAsync("fr-FR");

        // Assert
        mockModule.Verify(x => x.InvokeAsync<IJSVoidResult>("setLanguage", It.Is<object?[]>(a => (string?)a[0] == "fr-FR")), Times.Once);
    }

    [Theory]
    [InlineData(nameof(BrowserInteropService.FocusFirstInputAsync), "focusFirstInput", true)]
    [InlineData(nameof(BrowserInteropService.FocusFirstButtonAsync), "focusFirstButton", false)]
    [InlineData(nameof(BrowserInteropService.FocusAutofocusAsync), "focusAutofocus", true)]
    [InlineData(nameof(BrowserInteropService.FocusAsync), "focusElement", false)]
    public async Task GivenElement_WhenFocusing_ThenModuleFocusesItAndResultReturned(string method, string function, bool found)
    {
        // Arrange
        var element = new ElementReference("dialog");
        mockModule.Setup(x => x.InvokeAsync<bool>(function, It.Is<object?[]>(a => ((ElementReference)a[0]!).Id == "dialog"))).ReturnsAsync(found);
        var sut = new BrowserInteropService(mockJs.Object);
        Func<ElementReference, Task<bool>> focus = method switch
        {
            nameof(BrowserInteropService.FocusFirstInputAsync) => sut.FocusFirstInputAsync,
            nameof(BrowserInteropService.FocusFirstButtonAsync) => sut.FocusFirstButtonAsync,
            nameof(BrowserInteropService.FocusAutofocusAsync) => sut.FocusAutofocusAsync,
            _ => sut.FocusAsync,
        };

        // Act
        var result = await focus(element);

        // Assert
        Assert.Equal(found, result);
        mockModule.Verify(x => x.InvokeAsync<bool>(function, It.IsAny<object?[]>()), Times.Once);
    }
}
