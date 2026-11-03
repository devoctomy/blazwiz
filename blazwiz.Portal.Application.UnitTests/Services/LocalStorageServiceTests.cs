using blazwiz.Core.Configuration;
using blazwiz.Core.Configuration.Interfaces;
using System.Text.Json;
using blazwiz.Portal.Application.Services;
using blazwiz.Portal.Models.Settings;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Moq;

namespace blazwiz.Portal.Application.UnitTests.Services;

public class LocalStorageServiceTests
{
    private readonly Mock<IAppStorageKeys> mockKeys = CreateKeys();

    private readonly Mock<IJSRuntime> mockJsRuntime = new();

    [Fact]
    public void GivenNullJsRuntime_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => new LocalStorageService(null!, mockKeys.Object));
        Assert.Equal("jsRuntime", exception.ParamName);
    }

    [Fact]
    public async Task GivenStoredValue_WhenGetItemAsync_ThenReturnsValue()
    {
        // Arrange
        SetupGetItem("key", "value");
        var sut = new LocalStorageService(mockJsRuntime.Object, mockKeys.Object);

        // Act
        var result = await sut.GetItemAsync("key");

        // Assert
        Assert.Equal("value", result);
    }

    [Fact]
    public async Task GivenKeyAndValue_WhenSetItemAsync_ThenCallsLocalStorageSetItem()
    {
        // Arrange
        var sut = new LocalStorageService(mockJsRuntime.Object, mockKeys.Object);

        // Act
        await sut.SetItemAsync("key", "value");

        // Assert
        mockJsRuntime.Verify(
            x => x.InvokeAsync<IJSVoidResult>(
                "localStorage.setItem",
                It.Is<object?[]>(a => (string?)a[0] == "key" && (string?)a[1] == "value")),
            Times.Once);
    }

    [Fact]
    public async Task GivenKey_WhenRemoveItemAsync_ThenCallsLocalStorageRemoveItem()
    {
        // Arrange
        var sut = new LocalStorageService(mockJsRuntime.Object, mockKeys.Object);

        // Act
        await sut.RemoveItemAsync("key");

        // Assert
        mockJsRuntime.Verify(
            x => x.InvokeAsync<IJSVoidResult>(
                "localStorage.removeItem",
                It.Is<object?[]>(a => (string?)a[0] == "key")),
            Times.Once);
    }

    [Fact]
    public async Task GivenStoredJson_WhenGetObjectAsync_ThenReturnsDeserialisedObject()
    {
        // Arrange
        SetupGetItem("settings", JsonSerializer.Serialize(new UserSettings { Theme = "dark", SidebarCollapsed = true }));
        var sut = new LocalStorageService(mockJsRuntime.Object, mockKeys.Object);

        // Act
        var result = await sut.GetObjectAsync<UserSettings>("settings");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("dark", result.Theme);
        Assert.True(result.SidebarCollapsed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    public async Task GivenMissingOrInvalidJson_WhenGetObjectAsync_ThenReturnsDefault(string? stored)
    {
        // Arrange
        SetupGetItem("settings", stored);
        var sut = new LocalStorageService(mockJsRuntime.Object, mockKeys.Object);

        // Act
        var result = await sut.GetObjectAsync<UserSettings>("settings");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GivenObject_WhenSetObjectAsync_ThenStoresJson()
    {
        // Arrange
        var sut = new LocalStorageService(mockJsRuntime.Object, mockKeys.Object);
        var settings = new UserSettings { Theme = "light" };

        // Act
        await sut.SetObjectAsync("settings", settings);

        // Assert
        var expectedJson = JsonSerializer.Serialize(settings);
        mockJsRuntime.Verify(
            x => x.InvokeAsync<IJSVoidResult>(
                "localStorage.setItem",
                It.Is<object?[]>(a => (string?)a[0] == "settings" && (string?)a[1] == expectedJson)),
            Times.Once);
    }

    private void SetupGetItem(string key, string? value)
    {
        mockJsRuntime
            .Setup(x => x.InvokeAsync<string?>("localStorage.getItem", It.Is<object?[]>(a => (string?)a[0] == key)))
            .ReturnsAsync(value);
    }


    [Fact]
    public async Task GivenAppName_WhenSettingGettingAndRemoving_ThenKeysAreNamedAfterTheApp()
    {
        // Arrange
        var sut = new LocalStorageService(mockJsRuntime.Object, new AppStorageKeys(new BlazwizAppOptions { Name = "app" }));

        // Act
        await sut.SetItemAsync("session", "value");
        await sut.GetItemAsync("session");
        await sut.RemoveItemAsync("session");

        // Assert
        mockJsRuntime.Verify(x => x.InvokeAsync<IJSVoidResult>("localStorage.setItem", It.Is<object?[]>(a => (string?)a[0] == "app_session")), Times.Once);
        mockJsRuntime.Verify(x => x.InvokeAsync<string?>("localStorage.getItem", It.Is<object?[]>(a => (string?)a[0] == "app_session")), Times.Once);
        mockJsRuntime.Verify(x => x.InvokeAsync<IJSVoidResult>("localStorage.removeItem", It.Is<object?[]>(a => (string?)a[0] == "app_session")), Times.Once);
    }

    [Fact]
    public void GivenNullStorageKeys_WhenConstructing_ThenThrowsArgumentNullException()
    {
        Assert.Equal("storageKeys", Assert.Throws<ArgumentNullException>(() => new LocalStorageService(mockJsRuntime.Object, null!)).ParamName);
    }

    // Keeps names as they are, so the tests can check them; the prefix itself is checked separately.
    private static Mock<IAppStorageKeys> CreateKeys()
    {
        var keys = new Mock<IAppStorageKeys>();
        keys.Setup(x => x.For(It.IsAny<string>())).Returns<string>(name => name);
        keys.SetupGet(x => x.Prefix).Returns(string.Empty);
        return keys;
    }
}
