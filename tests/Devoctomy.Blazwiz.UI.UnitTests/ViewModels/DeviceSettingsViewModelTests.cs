using Devoctomy.Blazwiz.Auth.Sessions.Interfaces;
using Devoctomy.Blazwiz.Browser.Services;
using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Testing;
using Devoctomy.Blazwiz.UI.ViewModels;
using Moq;

namespace Devoctomy.Blazwiz.UI.UnitTests.ViewModels;

public class DeviceSettingsViewModelTests
{
    private readonly FrameworkText testText = new();
    private readonly Mock<IDeviceNameService> mockDeviceName = new();
    private readonly Mock<ISignOutEverywhereNotifier> mockNotifier = new();

    public DeviceSettingsViewModelTests()
    {
        mockDeviceName.Setup(x => x.GetNameAsync()).ReturnsAsync("Android-Edge-624834");
        mockNotifier.Setup(x => x.HasHandlers).Returns(true);
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("deviceNameService", Assert.Throws<ArgumentNullException>(() => new DeviceSettingsViewModel(null!, mockNotifier.Object, testText.Ui)).ParamName);
        Assert.Equal("signOutEverywhereNotifier", Assert.Throws<ArgumentNullException>(() => new DeviceSettingsViewModel(mockDeviceName.Object, null!, testText.Ui)).ParamName);
        Assert.Equal("text", Assert.Throws<ArgumentNullException>(() => new DeviceSettingsViewModel(mockDeviceName.Object, mockNotifier.Object, null!)).ParamName);
    }

    [Fact]
    public async Task GivenHandlers_WhenInitialized_ThenVisibleWithTheDevicesName()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.OnInitializedAsync();

        // Assert
        Assert.True(sut.IsVisible);
        Assert.Equal("Android-Edge-624834", sut.Name);
        Assert.Equal(DeviceNameService.MaxLength, sut.MaxLength);
    }

    [Fact]
    public async Task GivenNoHandlers_WhenInitialized_ThenHidden_AndTheNameIsNotMade()
    {
        // Arrange
        mockNotifier.Setup(x => x.HasHandlers).Returns(false);
        var sut = CreateSut();

        // Act
        await sut.OnInitializedAsync();

        // Assert
        Assert.False(sut.IsVisible);
        mockDeviceName.Verify(x => x.GetNameAsync(), Times.Never);
    }

    [Fact]
    public async Task GivenValidName_WhenSaved_ThenStoredTrimmed_AndConfirmed()
    {
        // Arrange
        var sut = CreateSut();
        sut.Name = "  Work laptop ";

        // Act
        await sut.SaveCommand.ExecuteAsync(null);

        // Assert
        mockDeviceName.Verify(x => x.SetNameAsync("Work laptop"), Times.Once);
        Assert.Equal("Work laptop", sut.Name);
        Assert.Equal("Saved.", sut.SavedMessage);
        Assert.Null(sut.ErrorMessage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("123456789012345678901234567890123")]
    [InlineData("Work\tlaptop")]
    [InlineData("Work\u0007laptop")]
    [InlineData(null)]
    public async Task GivenInvalidName_WhenSaved_ThenErrorShown_AndNothingStored(string? name)
    {
        // Arrange
        var sut = CreateSut();
        sut.Name = name!;

        // Act
        await sut.SaveCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("Enter a name of 1 to 32 characters.", sut.ErrorMessage);
        Assert.Null(sut.SavedMessage);
        mockDeviceName.Verify(x => x.SetNameAsync(It.IsAny<string>()), Times.Never);
    }

    private DeviceSettingsViewModel CreateSut() => new(mockDeviceName.Object, mockNotifier.Object, testText.Ui);
}
