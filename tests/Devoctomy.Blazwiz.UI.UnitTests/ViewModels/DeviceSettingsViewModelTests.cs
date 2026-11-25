using Devoctomy.Blazwiz.Auth.Exceptions;
using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.Auth.Sessions.Interfaces;
using Devoctomy.Blazwiz.Browser.Services;
using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Core.Exceptions;
using Devoctomy.Blazwiz.Testing;
using Devoctomy.Blazwiz.UI.Services.Interfaces;
using Devoctomy.Blazwiz.UI.ViewModels;
using Moq;

namespace Devoctomy.Blazwiz.UI.UnitTests.ViewModels;

public class DeviceSettingsViewModelTests
{
    private readonly FrameworkText testText = new();
    private readonly Mock<IDeviceNameService> mockDeviceName = new();
    private readonly Mock<ISignOutEverywhereNotifier> mockNotifier = new();
    private readonly Mock<IRememberedDeviceService> mockDevices = new();
    private readonly Mock<ISavedDeviceCredentialService> mockSaved = new();
    private readonly RememberedDevice thisDevice = new() { Key = "k1", Name = "Android-Edge-624834", IsThisDevice = true };
    private readonly RememberedDevice otherDevice = new() { Key = "k2", Name = "Work laptop" };

    public DeviceSettingsViewModelTests()
    {
        mockDeviceName.Setup(x => x.GetNameAsync()).ReturnsAsync("Android-Edge-624834");
        mockNotifier.Setup(x => x.HasHandlers).Returns(true);
        mockDevices.Setup(x => x.ListAsync()).ReturnsAsync([otherDevice]);
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("deviceNameService", Assert.Throws<ArgumentNullException>(() => new DeviceSettingsViewModel(null!, mockNotifier.Object, mockDevices.Object, mockSaved.Object, testText.Errors, testText.Ui)).ParamName);
        Assert.Equal("signOutEverywhereNotifier", Assert.Throws<ArgumentNullException>(() => new DeviceSettingsViewModel(mockDeviceName.Object, null!, mockDevices.Object, mockSaved.Object, testText.Errors, testText.Ui)).ParamName);
        Assert.Equal("rememberedDevices", Assert.Throws<ArgumentNullException>(() => new DeviceSettingsViewModel(mockDeviceName.Object, mockNotifier.Object, null!, mockSaved.Object, testText.Errors, testText.Ui)).ParamName);
        Assert.Equal("savedDeviceCredential", Assert.Throws<ArgumentNullException>(() => new DeviceSettingsViewModel(mockDeviceName.Object, mockNotifier.Object, mockDevices.Object, null!, testText.Errors, testText.Ui)).ParamName);
        Assert.Equal("errorText", Assert.Throws<ArgumentNullException>(() => new DeviceSettingsViewModel(mockDeviceName.Object, mockNotifier.Object, mockDevices.Object, mockSaved.Object, null!, testText.Ui)).ParamName);
        Assert.Equal("text", Assert.Throws<ArgumentNullException>(() => new DeviceSettingsViewModel(mockDeviceName.Object, mockNotifier.Object, mockDevices.Object, mockSaved.Object, testText.Errors, null!)).ParamName);
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
        Assert.Null(sut.ForgetWarning);
        Assert.False(sut.IsBusy);
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
        var sut = await CreateInitializedSutAsync();
        sut.Name = "  Work laptop ";

        // Act
        await sut.SaveCommand.ExecuteAsync(null);

        // Assert
        mockDeviceName.Verify(x => x.SetNameAsync("Work laptop"), Times.Once);
        Assert.Equal("Work laptop", sut.Name);
        Assert.Equal("Saved.", sut.SavedMessage);
        Assert.Null(sut.ErrorMessage);
        Assert.Null(sut.ForgetWarning);
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
        mockDevices.Verify(x => x.ListAsync(), Times.Never);
    }

    [Fact]
    public async Task GivenARememberedDevice_WhenRenamed_ThenItWarnsFirst_AndNothingIsSavedOrForgotten()
    {
        // Arrange
        mockDevices.Setup(x => x.ListAsync()).ReturnsAsync([thisDevice, otherDevice]);
        var sut = await CreateInitializedSutAsync();
        sut.Name = " Phone ";

        // Act
        await sut.SaveCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(testText.Ui["Settings.Device.Forget.Text", "Phone"], sut.ForgetWarning);
        Assert.Contains("\"Phone\"", sut.ForgetWarning);
        Assert.Equal("Phone", sut.Name);
        Assert.Null(sut.SavedMessage);
        mockDeviceName.Verify(x => x.SetNameAsync(It.IsAny<string>()), Times.Never);
        mockDevices.Verify(x => x.ForgetAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GivenTheWarning_WhenConfirmed_ThenTheDeviceIsForgotten_ItsCredentialDropped_AndTheNameSaved()
    {
        // Arrange
        var calls = new List<string>();
        mockDevices.Setup(x => x.ListAsync()).ReturnsAsync([thisDevice, otherDevice]);
        mockDevices.Setup(x => x.ForgetAsync("k1")).Callback(() => calls.Add("forget")).Returns(Task.CompletedTask);
        mockSaved.Setup(x => x.DropAsync()).Callback(() => calls.Add("drop")).Returns(Task.CompletedTask);
        mockDeviceName.Setup(x => x.SetNameAsync("Phone")).Callback(() => calls.Add("name")).Returns(Task.CompletedTask);
        var sut = await CreateInitializedSutAsync();
        sut.Name = "Phone";
        await sut.SaveCommand.ExecuteAsync(null);

        // Act
        await sut.ConfirmForgetCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(["forget", "drop", "name"], calls);
        Assert.Null(sut.ForgetWarning);
        Assert.Equal("Saved.", sut.SavedMessage);
        Assert.False(sut.IsBusy);
    }

    [Fact]
    public async Task GivenTheWarning_WhenForgettingFails_ThenTheErrorIsShown_AndTheNameIsNotSaved()
    {
        // Arrange
        mockDevices.Setup(x => x.ListAsync()).ReturnsAsync([thisDevice]);
        mockDevices.Setup(x => x.ForgetAsync("k1")).ThrowsAsync(new BlazwizException(AuthErrorCodes.DevicesFailed));
        var sut = await CreateInitializedSutAsync();
        sut.Name = "Phone";
        await sut.SaveCommand.ExecuteAsync(null);

        // Act
        await sut.ConfirmForgetCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(testText.Errors.Describe(AuthErrorCodes.DevicesFailed, []), sut.ErrorMessage);
        Assert.Null(sut.ForgetWarning);
        Assert.False(sut.IsBusy);
        mockSaved.Verify(x => x.DropAsync(), Times.Never);
        mockDeviceName.Verify(x => x.SetNameAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GivenTheWarning_WhenCancelled_ThenTheOldNameIsBack_AndNothingIsSavedOrForgotten()
    {
        // Arrange
        mockDevices.Setup(x => x.ListAsync()).ReturnsAsync([thisDevice]);
        var sut = await CreateInitializedSutAsync();
        sut.Name = "Phone";
        await sut.SaveCommand.ExecuteAsync(null);

        // Act
        sut.CancelForgetCommand.Execute(null);

        // Assert
        Assert.Null(sut.ForgetWarning);
        Assert.Equal("Android-Edge-624834", sut.Name);
        mockDevices.Verify(x => x.ForgetAsync(It.IsAny<string>()), Times.Never);
        mockDeviceName.Verify(x => x.SetNameAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GivenNoWarning_WhenConfirmed_ThenNothingHappens()
    {
        // Arrange
        var sut = await CreateInitializedSutAsync();

        // Act
        await sut.ConfirmForgetCommand.ExecuteAsync(null);

        // Assert
        mockDevices.Verify(x => x.ForgetAsync(It.IsAny<string>()), Times.Never);
        mockDeviceName.Verify(x => x.SetNameAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GivenAConfirmationUnderWay_WhenConfirmedAgain_ThenItIsForgottenOnce()
    {
        // Arrange
        var pending = new TaskCompletionSource();
        mockDevices.Setup(x => x.ListAsync()).ReturnsAsync([thisDevice]);
        mockDevices.Setup(x => x.ForgetAsync("k1")).Returns(pending.Task);
        var sut = await CreateInitializedSutAsync();
        sut.Name = "Phone";
        await sut.SaveCommand.ExecuteAsync(null);
        var first = sut.ConfirmForgetAsync();

        // Act
        await sut.ConfirmForgetAsync();
        pending.SetResult();
        await first;

        // Assert
        mockDevices.Verify(x => x.ForgetAsync("k1"), Times.Once);
    }

    [Fact]
    public async Task GivenTheSameName_WhenSaved_ThenSavedWithoutAsking()
    {
        // Arrange
        mockDevices.Setup(x => x.ListAsync()).ReturnsAsync([thisDevice]);
        var sut = await CreateInitializedSutAsync();

        // Act
        await sut.SaveCommand.ExecuteAsync(null);

        // Assert
        Assert.Null(sut.ForgetWarning);
        Assert.Equal("Saved.", sut.SavedMessage);
        mockDevices.Verify(x => x.ListAsync(), Times.Never);
    }

    [Fact]
    public async Task GivenTheDevicesCannotBeListed_WhenRenamed_ThenTheNameIsSavedWithoutAsking()
    {
        // Arrange: e.g. offline.
        mockDevices.Setup(x => x.ListAsync()).ThrowsAsync(new BlazwizException(AuthErrorCodes.AccountChangeOffline));
        var sut = await CreateInitializedSutAsync();
        sut.Name = "Phone";

        // Act
        await sut.SaveCommand.ExecuteAsync(null);

        // Assert
        Assert.Null(sut.ForgetWarning);
        mockDeviceName.Verify(x => x.SetNameAsync("Phone"), Times.Once);
        Assert.Equal("Saved.", sut.SavedMessage);
    }

    [Fact]
    public async Task GivenARenameSaved_WhenRenamedBackAgain_ThenTheSavedNameIsTheOneCompared()
    {
        // Arrange: not remembered when first renamed, remembered by the second time.
        var sut = await CreateInitializedSutAsync();
        sut.Name = "Phone";
        await sut.SaveCommand.ExecuteAsync(null);
        mockDevices.Setup(x => x.ListAsync()).ReturnsAsync([thisDevice]);

        // Act
        await sut.SaveCommand.ExecuteAsync(null);

        // Assert
        Assert.Null(sut.ForgetWarning);
        mockDeviceName.Verify(x => x.SetNameAsync("Phone"), Times.Exactly(2));
    }

    private async Task<DeviceSettingsViewModel> CreateInitializedSutAsync()
    {
        var sut = CreateSut();
        await sut.OnInitializedAsync();
        return sut;
    }

    private DeviceSettingsViewModel CreateSut() => new(mockDeviceName.Object, mockNotifier.Object, mockDevices.Object, mockSaved.Object, testText.Errors, testText.Ui);
}
