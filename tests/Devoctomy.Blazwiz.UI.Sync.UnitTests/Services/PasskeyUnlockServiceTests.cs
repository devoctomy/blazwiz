using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.Browser.Models;
using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.UI.Sync.Services;
using Devoctomy.Blazwiz.UI.ViewModels;
using Moq;

namespace Devoctomy.Blazwiz.UI.Sync.UnitTests.Services;

public class PasskeyUnlockServiceTests
{
    private readonly Mock<IAuthenticationService> mockAuth = new();
    private readonly Mock<ILocalStorageService> mockLocalStorage = new();
    private readonly Mock<IWebAuthnService> mockWebAuthn = new();

    public PasskeyUnlockServiceTests()
    {
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(new UserSession { Username = "bob@example.com" });
        mockLocalStorage.Setup(x => x.GetObjectAsync<SavedUsersData>(LoginViewModel.SavedUsersStorageKey)).ReturnsAsync(new SavedUsersData
        {
            Users =
            [
                new SavedUser { Email = "alice@example.com", WebAuthnCredential = new WebAuthnStoredCredential { CredentialID = "alice-cred" } },
                new SavedUser { Email = "Bob@Example.com", WebAuthnCredential = new WebAuthnStoredCredential { CredentialID = "bob-cred" } },
            ],
        });
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("authService", Assert.Throws<ArgumentNullException>(() => new PasskeyUnlockService(null!, mockLocalStorage.Object, mockWebAuthn.Object)).ParamName);
        Assert.Equal("localStorageService", Assert.Throws<ArgumentNullException>(() => new PasskeyUnlockService(mockAuth.Object, null!, mockWebAuthn.Object)).ParamName);
        Assert.Equal("webAuthnService", Assert.Throws<ArgumentNullException>(() => new PasskeyUnlockService(mockAuth.Object, mockLocalStorage.Object, null!)).ParamName);
    }

    [Fact]
    public async Task GivenSavedPasskeyForSignedInUser_WhenUnlockAsync_ThenAuthenticatesWithItAndReportsKeysDerived()
    {
        // Arrange
        mockWebAuthn.Setup(x => x.AuthenticateAsync("bob-cred")).ReturnsAsync(new WebAuthnAuthenticateResult { Success = true, KeysDerived = true });
        var sut = CreateSut();

        // Act & Assert
        Assert.True(await sut.CanUnlockAsync());
        Assert.True(await sut.UnlockAsync());
    }

    [Fact]
    public async Task GivenAuthenticatorWithoutPrf_WhenUnlockAsync_ThenFalse()
    {
        // Arrange
        mockWebAuthn.Setup(x => x.AuthenticateAsync("bob-cred")).ReturnsAsync(new WebAuthnAuthenticateResult { Success = true, KeysDerived = false });

        // Act & Assert
        Assert.False(await CreateSut().UnlockAsync());
    }

    [Fact]
    public async Task GivenNoSavedPasskey_WhenUnlockAsync_ThenFalseWithoutAsking()
    {
        // Arrange
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(new UserSession { Username = "carol@example.com" });
        var sut = CreateSut();

        // Act & Assert
        Assert.False(await sut.CanUnlockAsync());
        Assert.False(await sut.UnlockAsync());
        mockWebAuthn.Verify(x => x.AuthenticateAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GivenSessionEmailMatches_WhenCanUnlockAsync_ThenTrue()
    {
        // Arrange
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(new UserSession { Username = "sub-123", Email = "alice@example.com" });

        // Act & Assert
        Assert.True(await CreateSut().CanUnlockAsync());
    }

    [Fact]
    public async Task GivenSignedOutOrUnreadableStorage_WhenCanUnlockAsync_ThenFalse()
    {
        // Arrange
        mockLocalStorage.Setup(x => x.GetObjectAsync<SavedUsersData>(LoginViewModel.SavedUsersStorageKey)).ThrowsAsync(new InvalidOperationException());

        // Act & Assert
        Assert.False(await CreateSut().CanUnlockAsync());
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync((UserSession?)null);
        Assert.False(await CreateSut().CanUnlockAsync());
    }

    private PasskeyUnlockService CreateSut() => new(mockAuth.Object, mockLocalStorage.Object, mockWebAuthn.Object);
}
