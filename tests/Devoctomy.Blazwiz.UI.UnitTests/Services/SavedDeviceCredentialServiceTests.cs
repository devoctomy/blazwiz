using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.UI.Services;
using Devoctomy.Blazwiz.UI.ViewModels;
using Moq;

namespace Devoctomy.Blazwiz.UI.UnitTests.Services;

public class SavedDeviceCredentialServiceTests
{
    private readonly Mock<IAuthenticationService> mockAuth = new();
    private readonly Mock<ILocalStorageService> mockLocalStorage = new();
    private SavedUsersData? saved;

    public SavedDeviceCredentialServiceTests()
    {
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(new UserSession { Username = "bob@example.com", Email = "bob@example.com" });
        mockLocalStorage
            .Setup(x => x.SetObjectAsync(LoginViewModel.SavedUsersStorageKey, It.IsAny<SavedUsersData>()))
            .Callback<string, SavedUsersData>((_, data) => saved = data)
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("authService", Assert.Throws<ArgumentNullException>(() => new SavedDeviceCredentialService(null!, mockLocalStorage.Object)).ParamName);
        Assert.Equal("localStorageService", Assert.Throws<ArgumentNullException>(() => new SavedDeviceCredentialService(mockAuth.Object, null!)).ParamName);
    }

    [Fact]
    public async Task GivenTheSignedInUsersStoredDevice_WhenDropped_ThenOnlyItIsGone()
    {
        // Arrange
        var user = new SavedUser
        {
            Email = "Bob@Example.com",
            WebAuthnCredential = new WebAuthnStoredCredential { CredentialID = "cred1", EncryptedPassword = "enc", EncryptedDevice = "enc-device" },
        };
        var alice = new SavedUser { Email = "alice@example.com", WebAuthnCredential = new WebAuthnStoredCredential { EncryptedDevice = "alice-device" } };
        mockLocalStorage
            .Setup(x => x.GetObjectAsync<SavedUsersData>(LoginViewModel.SavedUsersStorageKey))
            .ReturnsAsync(new SavedUsersData { Users = [alice, user] });

        // Act
        await CreateSut().DropAsync();

        // Assert
        var stored = saved?.Users.Single(u => u.Email == "Bob@Example.com").WebAuthnCredential;
        Assert.Null(stored?.EncryptedDevice);
        Assert.Equal("enc", stored?.EncryptedPassword);
        Assert.Equal("alice-device", saved?.Users.Single(u => u.Email == "alice@example.com").WebAuthnCredential?.EncryptedDevice);
    }

    [Fact]
    public async Task GivenNothingStoredForTheUser_WhenDropped_ThenNothingIsSaved()
    {
        // Arrange
        mockLocalStorage
            .Setup(x => x.GetObjectAsync<SavedUsersData>(LoginViewModel.SavedUsersStorageKey))
            .ReturnsAsync(new SavedUsersData { Users = [new SavedUser { Email = "bob@example.com" }] });

        // Act
        await CreateSut().DropAsync();

        // Assert
        Assert.Null(saved);
    }

    [Fact]
    public async Task GivenNobodySignedIn_WhenDropped_ThenNothingIsReadOrSaved()
    {
        // Arrange
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync((UserSession?)null);

        // Act
        await CreateSut().DropAsync();

        // Assert
        mockLocalStorage.VerifyNoOtherCalls();
    }

    private SavedDeviceCredentialService CreateSut() => new(mockAuth.Object, mockLocalStorage.Object);
}
