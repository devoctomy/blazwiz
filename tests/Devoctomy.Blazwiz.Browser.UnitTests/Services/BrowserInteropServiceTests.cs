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
    public async Task GivenElement_WhenFocusFirstInputAsync_ThenModuleFocusesIt()
    {
        // Arrange
        var element = new ElementReference("dialog");
        var sut = new BrowserInteropService(mockJs.Object);

        // Act
        await sut.FocusFirstInputAsync(element);

        // Assert
        mockModule.Verify(x => x.InvokeAsync<IJSVoidResult>("focusFirstInput", It.Is<object?[]>(a => ((ElementReference)a[0]!).Id == "dialog")), Times.Once);
    }
}
