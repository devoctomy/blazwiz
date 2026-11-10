using Devoctomy.Blazwiz.Browser.Models;
using Devoctomy.Blazwiz.Browser.Services;
using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Microsoft.JSInterop;
using Moq;

namespace Devoctomy.Blazwiz.Browser.UnitTests.Services;

public class DeviceNameServiceTests
{
    private readonly Mock<IJSRuntime> mockJs = new();
    private readonly Mock<IJSObjectReference> mockModule = new();
    private readonly Mock<ILocalStorageService> mockStorage = new();
    private readonly Dictionary<string, string> stored = [];

    public DeviceNameServiceTests()
    {
        mockJs.Setup(x => x.InvokeAsync<IJSObjectReference>("import", It.Is<object?[]>(a => (string?)a[0] == DeviceNameService.ModulePath)))
            .ReturnsAsync(mockModule.Object);
        mockStorage.Setup(x => x.GetItemAsync(It.IsAny<string>()))
            .ReturnsAsync((string key) => stored.TryGetValue(key, out var value) ? value : null);
        mockStorage.Setup(x => x.SetItemAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback((string key, string value) => stored[key] = value)
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("jsRuntime", Assert.Throws<ArgumentNullException>(() => new DeviceNameService(null!, mockStorage.Object)).ParamName);
        Assert.Equal("localStorageService", Assert.Throws<ArgumentNullException>(() => new DeviceNameService(mockJs.Object, null!)).ParamName);
    }

    [Fact]
    public async Task GivenNoStoredName_WhenGetNameAsync_ThenDefaultIsMadeFromPlatformBrowserAndSixDigits_AndStored()
    {
        // Arrange
        Describe("Android", "Edge");
        var sut = new DeviceNameService(mockJs.Object, mockStorage.Object);

        // Act
        var result = await sut.GetNameAsync();

        // Assert
        Assert.Matches("^Android-Edge-[0-9]{6}$", result);
        Assert.Equal(result, stored[DeviceNameService.StorageKey]);
    }

    [Fact]
    public async Task GivenUnrecognisedPlatformAndBrowser_WhenGetNameAsync_ThenUnknownIsUsed()
    {
        // Arrange
        Describe(null, " ");
        var sut = new DeviceNameService(mockJs.Object, mockStorage.Object);

        // Act
        var result = await sut.GetNameAsync();

        // Assert
        Assert.Matches("^Unknown-Unknown-[0-9]{6}$", result);
    }

    [Fact]
    public async Task GivenTheModuleFails_WhenGetNameAsync_ThenUnknownIsUsed()
    {
        // Arrange
        mockModule.Setup(x => x.InvokeAsync<DeviceDescription?>("describe", It.IsAny<object?[]>()))
            .ThrowsAsync(new JSException("no"));
        var sut = new DeviceNameService(mockJs.Object, mockStorage.Object);

        // Act
        var result = await sut.GetNameAsync();

        // Assert
        Assert.Matches("^Unknown-Unknown-[0-9]{6}$", result);
    }

    [Fact]
    public async Task GivenLongOrOddParts_WhenGetNameAsync_ThenPartsAreLettersAndDigitsOnly_AndTheNameFits()
    {
        // Arrange
        Describe("Some Very Long Platform Name!", "<script>");
        var sut = new DeviceNameService(mockJs.Object, mockStorage.Object);

        // Act
        var result = await sut.GetNameAsync();

        // Assert
        Assert.Matches("^SomeVeryLong-script-[0-9]{6}$", result);
        Assert.True(result.Length <= DeviceNameService.MaxLength);
    }

    [Fact]
    public async Task GivenStoredName_WhenGetNameAsync_ThenItIsReturned_AndNothingIsMade()
    {
        // Arrange
        stored[DeviceNameService.StorageKey] = "Work laptop";
        var sut = new DeviceNameService(mockJs.Object, mockStorage.Object);

        // Act
        var result = await sut.GetNameAsync();

        // Assert
        Assert.Equal("Work laptop", result);
        mockJs.Verify(x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object?[]>()), Times.Never);
    }

    [Fact]
    public async Task GivenNameRead_WhenGetNameAsyncAgain_ThenStorageIsNotReadAgain()
    {
        // Arrange
        stored[DeviceNameService.StorageKey] = "Work laptop";
        var sut = new DeviceNameService(mockJs.Object, mockStorage.Object);
        await sut.GetNameAsync();

        // Act
        var result = await sut.GetNameAsync();

        // Assert
        Assert.Equal("Work laptop", result);
        mockStorage.Verify(x => x.GetItemAsync(DeviceNameService.StorageKey), Times.Once);
    }

    [Fact]
    public async Task GivenValidName_WhenSetNameAsync_ThenItIsTrimmedAndStored_AndReturnedNext()
    {
        // Arrange
        var sut = new DeviceNameService(mockJs.Object, mockStorage.Object);

        // Act
        await sut.SetNameAsync("  Phone  ");
        var result = await sut.GetNameAsync();

        // Assert
        Assert.Equal("Phone", stored[DeviceNameService.StorageKey]);
        Assert.Equal("Phone", result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a\nb")]
    [InlineData("123456789012345678901234567890123")]
    public async Task GivenInvalidName_WhenSetNameAsync_ThenThrowsArgumentException_AndNothingIsStored(string name)
    {
        // Arrange
        var sut = new DeviceNameService(mockJs.Object, mockStorage.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => sut.SetNameAsync(name));
        Assert.Empty(stored);
    }

    [Fact]
    public async Task GivenNullName_WhenSetNameAsync_ThenThrowsArgumentNullException()
    {
        // Arrange
        var sut = new DeviceNameService(mockJs.Object, mockStorage.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.SetNameAsync(null!));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("  ", null)]
    [InlineData("\u0007", null)]
    [InlineData(" Phone ", "Phone")]
    [InlineData("Ph\u0000one", "Phone")]
    [InlineData("123456789012345678901234567890123456", "12345678901234567890123456789012")]
    public void GivenName_WhenSanitize_ThenTrimmedWithoutControlCharacters_AndCut(string? name, string? expected)
    {
        // Arrange
        var sut = new DeviceNameService(mockJs.Object, mockStorage.Object);

        // Act
        var result = sut.Sanitize(name);

        // Assert
        Assert.Equal(expected, result);
    }

    private void Describe(string? platform, string? browser)
    {
        mockModule.Setup(x => x.InvokeAsync<DeviceDescription?>("describe", It.IsAny<object?[]>()))
            .ReturnsAsync(new DeviceDescription { Platform = platform, Browser = browser });
    }
}
