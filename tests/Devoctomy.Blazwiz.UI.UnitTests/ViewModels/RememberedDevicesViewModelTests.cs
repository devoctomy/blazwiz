using Devoctomy.Blazwiz.Auth.Exceptions;
using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Core.Exceptions;
using Devoctomy.Blazwiz.Testing;
using Devoctomy.Blazwiz.UI.ViewModels;
using Moq;

namespace Devoctomy.Blazwiz.UI.UnitTests.ViewModels;

public class RememberedDevicesViewModelTests
{
    private readonly FrameworkText testText = new();
    private readonly Mock<IRememberedDeviceService> mockDevices = new();
    private readonly Mock<IAuthenticationService> mockAuth = new();
    private readonly Mock<ILocalStorageService> mockLocalStorage = new();
    private readonly RememberedDevice thisDevice = new() { Key = "k1", Name = "Work laptop", IsThisDevice = true };
    private readonly RememberedDevice otherDevice = new() { Key = "k2" };
    private SavedUsersData? saved;

    public RememberedDevicesViewModelTests()
    {
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(new UserSession { Username = "bob@example.com", Email = "bob@example.com" });
        mockDevices.Setup(x => x.ListAsync()).ReturnsAsync([thisDevice, otherDevice]);
        mockLocalStorage
            .Setup(x => x.SetObjectAsync(LoginViewModel.SavedUsersStorageKey, It.IsAny<SavedUsersData>()))
            .Callback<string, SavedUsersData>((_, data) => saved = data)
            .Returns(Task.CompletedTask);
    }

    public static TheoryData<UserSession?> NotOnline => new()
    {
        null,
        new UserSession { Username = "bob@example.com", IsOffline = true },
    };

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("deviceService", Assert.Throws<ArgumentNullException>(() => new RememberedDevicesViewModel(null!, mockAuth.Object, mockLocalStorage.Object, testText.Errors)).ParamName);
        Assert.Equal("authService", Assert.Throws<ArgumentNullException>(() => new RememberedDevicesViewModel(mockDevices.Object, null!, mockLocalStorage.Object, testText.Errors)).ParamName);
        Assert.Equal("localStorageService", Assert.Throws<ArgumentNullException>(() => new RememberedDevicesViewModel(mockDevices.Object, mockAuth.Object, null!, testText.Errors)).ParamName);
        Assert.Equal("errorText", Assert.Throws<ArgumentNullException>(() => new RememberedDevicesViewModel(mockDevices.Object, mockAuth.Object, mockLocalStorage.Object, null!)).ParamName);
    }

    [Fact]
    public async Task GivenOnlineSession_WhenOnInitializedAsync_ThenVisible_WithTheDevices()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.OnInitializedAsync();

        // Assert
        Assert.True(sut.IsVisible);
        Assert.Equal([thisDevice, otherDevice], sut.Devices);
    }

    [Theory]
    [MemberData(nameof(NotOnline))]
    public async Task GivenNoOnlineSession_WhenOnInitializedAsync_ThenHidden(UserSession? session)
    {
        // Arrange
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(session);
        var sut = CreateSut();

        // Act
        await sut.OnInitializedAsync();

        // Assert
        Assert.False(sut.IsVisible);
        mockDevices.Verify(x => x.ListAsync(), Times.Never);
    }

    [Fact]
    public async Task GivenListUnreadable_WhenOnInitializedAsync_ThenHidden()
    {
        // Arrange
        mockDevices.Setup(x => x.ListAsync()).ThrowsAsync(new BlazwizException(AuthErrorCodes.Unreachable));
        var sut = CreateSut();

        // Act
        await sut.OnInitializedAsync();

        // Assert
        Assert.False(sut.IsVisible);
    }

    [Fact]
    public async Task GivenAnotherDevice_WhenForgetAsync_ThenGoneFromTheList_AndNothingStoredChanged()
    {
        // Arrange
        var sut = await CreateInitializedSutAsync();

        // Act
        await sut.ForgetAsync(otherDevice);

        // Assert
        mockDevices.Verify(x => x.ForgetAsync("k2"), Times.Once);
        Assert.Equal([thisDevice], sut.Devices);
        Assert.Null(saved);
        Assert.False(sut.IsBusy);
    }

    [Fact]
    public async Task GivenThisDevice_WhenForgetAsync_ThenItsStoredCredentialIsDropped()
    {
        // Arrange
        var user = new SavedUser
        {
            Email = "Bob@Example.com",
            WebAuthnCredential = new WebAuthnStoredCredential { CredentialID = "cred1", EncryptedPassword = "enc", EncryptedDevice = "enc-device" },
        };
        mockLocalStorage
            .Setup(x => x.GetObjectAsync<SavedUsersData>(LoginViewModel.SavedUsersStorageKey))
            .ReturnsAsync(new SavedUsersData { Users = [new SavedUser { Email = "alice@example.com" }, user] });
        var sut = await CreateInitializedSutAsync();

        // Act
        await sut.ForgetAsync(thisDevice);

        // Assert
        Assert.Equal([otherDevice], sut.Devices);
        var stored = saved?.Users.Single(u => u.Email == "Bob@Example.com").WebAuthnCredential;
        Assert.Null(stored?.EncryptedDevice);
        Assert.Equal("enc", stored?.EncryptedPassword);
    }

    [Fact]
    public async Task GivenThisDeviceWithNothingStored_WhenForgetAsync_ThenNothingSaved()
    {
        // Arrange
        mockLocalStorage
            .Setup(x => x.GetObjectAsync<SavedUsersData>(LoginViewModel.SavedUsersStorageKey))
            .ReturnsAsync(new SavedUsersData { Users = [new SavedUser { Email = "bob@example.com" }] });
        var sut = await CreateInitializedSutAsync();

        // Act
        await sut.ForgetAsync(thisDevice);

        // Assert
        Assert.Null(saved);
        Assert.Equal([otherDevice], sut.Devices);
    }

    [Fact]
    public async Task GivenRefused_WhenForgetAsync_ThenTheError_AndStillListed()
    {
        // Arrange
        mockDevices.Setup(x => x.ForgetAsync("k2")).ThrowsAsync(new BlazwizException(AuthErrorCodes.DevicesFailed));
        var sut = await CreateInitializedSutAsync();

        // Act
        await sut.ForgetAsync(otherDevice);

        // Assert
        Assert.Equal(testText.Errors.Describe(AuthErrorCodes.DevicesFailed, []), sut.ErrorMessage);
        Assert.Equal([thisDevice, otherDevice], sut.Devices);
    }

    [Fact]
    public async Task GivenBusy_WhenForgetAsyncAgain_ThenIgnored()
    {
        // Arrange
        var pending = new TaskCompletionSource();
        mockDevices.Setup(x => x.ForgetAsync("k2")).Returns(pending.Task);
        var sut = await CreateInitializedSutAsync();
        var first = sut.ForgetAsync(otherDevice);

        // Act
        await sut.ForgetAsync(thisDevice);
        pending.SetResult();
        await first;

        // Assert
        mockDevices.Verify(x => x.ForgetAsync("k1"), Times.Never);
    }

    [Fact]
    public async Task GivenNotInitialized_WhenForgetAsyncThisDevice_ThenNoStoredUserMatches()
    {
        // Arrange
        mockLocalStorage
            .Setup(x => x.GetObjectAsync<SavedUsersData>(LoginViewModel.SavedUsersStorageKey))
            .ReturnsAsync(new SavedUsersData { Users = [new SavedUser { Email = "bob@example.com", WebAuthnCredential = new WebAuthnStoredCredential { EncryptedDevice = "enc-device" } }] });
        var sut = CreateSut();

        // Act
        await sut.ForgetAsync(thisDevice);

        // Assert
        Assert.Null(saved);
    }

    [Fact]
    public async Task GivenNullDevice_WhenForgetAsync_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => CreateSut().ForgetAsync(null!));
    }

    private async Task<RememberedDevicesViewModel> CreateInitializedSutAsync()
    {
        var sut = CreateSut();
        await sut.OnInitializedAsync();
        return sut;
    }

    private RememberedDevicesViewModel CreateSut() => new(mockDevices.Object, mockAuth.Object, mockLocalStorage.Object, testText.Errors);
}
