using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.Browser.Models;
using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Cryptography;
using Devoctomy.Blazwiz.UI.Services;
using Devoctomy.Blazwiz.UI.ViewModels;
using Moq;

namespace Devoctomy.Blazwiz.UI.UnitTests.Services;

public class PasskeyKeysServiceTests
{
    private const string KeyName = "e2e-kek";

    private readonly Mock<IAuthenticationService> mockAuth = new();
    private readonly Mock<ILocalStorageService> mockStorage = new();
    private readonly Mock<IWebAuthnService> mockWebAuthn = new();
    private readonly InMemoryDerivedKeyStore keys = new();
    private SavedUsersData? saved;

    public PasskeyKeysServiceTests()
    {
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(new UserSession { Username = "bob@example.com", Email = "bob@example.com" });
        mockStorage.Setup(x => x.GetObjectAsync<SavedUsersData>(LoginViewModel.SavedUsersStorageKey)).ReturnsAsync(() => saved);
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("authService", Assert.Throws<ArgumentNullException>(() => new PasskeyKeysService(null!, mockStorage.Object, mockWebAuthn.Object, keys)).ParamName);
        Assert.Equal("localStorageService", Assert.Throws<ArgumentNullException>(() => new PasskeyKeysService(mockAuth.Object, null!, mockWebAuthn.Object, keys)).ParamName);
        Assert.Equal("webAuthnService", Assert.Throws<ArgumentNullException>(() => new PasskeyKeysService(mockAuth.Object, mockStorage.Object, null!, keys)).ParamName);
        Assert.Equal("keyStore", Assert.Throws<ArgumentNullException>(() => new PasskeyKeysService(mockAuth.Object, mockStorage.Object, mockWebAuthn.Object, null!)).ParamName);
    }

    [Fact]
    public async Task GivenTheSignedInUserRememberedWithAPasskey_WhenAsked_ThenAvailable()
    {
        // Arrange
        RememberBob(withPasskey: true);

        // Act & Assert
        Assert.True(await CreateSut().IsAvailableAsync());
    }

    [Fact]
    public async Task GivenTheSignedInUserRememberedWithoutAPasskey_OrNotRemembered_WhenAsked_ThenNotAvailable()
    {
        // Arrange
        var sut = CreateSut();

        // Act & Assert
        Assert.False(await sut.IsAvailableAsync());
        RememberBob(withPasskey: false);
        Assert.False(await sut.IsAvailableAsync());
        saved = new SavedUsersData { Users = [new SavedUser { Email = "alice@example.com", WebAuthnCredential = new WebAuthnStoredCredential { CredentialID = "alice-cred" } }] };
        Assert.False(await sut.IsAvailableAsync());
    }

    [Fact]
    public async Task GivenNobodySignedIn_WhenAsked_ThenNotAvailable()
    {
        // Arrange
        RememberBob(withPasskey: true);
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync((UserSession?)null);

        // Act & Assert
        Assert.False(await CreateSut().IsAvailableAsync());
    }

    [Fact]
    public async Task GivenTheKeyInMemory_WhenEnsured_ThenThePasskeyIsNotAskedFor()
    {
        // Arrange
        RememberBob(withPasskey: true);
        keys.SetKey(KeyName, new byte[32]);

        // Act
        var ensured = await CreateSut().EnsureKeyAsync(KeyName);

        // Assert
        Assert.True(ensured);
        mockWebAuthn.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GivenTheKeyNotInMemory_WhenEnsured_ThenThePasskeyIsAskedFor_AndTheKeyIsThere()
    {
        // Arrange: authenticating derives the keys from the PRF.
        RememberBob(withPasskey: true);
        mockWebAuthn.Setup(x => x.AuthenticateAsync("bob-cred"))
            .Callback(() => keys.SetKey(KeyName, new byte[32]))
            .ReturnsAsync(new WebAuthnAuthenticateResult { Success = true, KeysDerived = true });

        // Act
        var ensured = await CreateSut().EnsureKeyAsync(KeyName);

        // Assert
        Assert.True(ensured);
        mockWebAuthn.Verify(x => x.AuthenticateAsync("bob-cred"), Times.Once);
    }

    [Fact]
    public async Task GivenThePasskeyCancelledOrFailing_WhenEnsured_ThenFalse()
    {
        // Arrange
        RememberBob(withPasskey: true);
        var sut = CreateSut();

        // Act & Assert
        mockWebAuthn.Setup(x => x.AuthenticateAsync("bob-cred")).ReturnsAsync(new WebAuthnAuthenticateResult { Success = false, Error = "NotAllowedError" });
        Assert.False(await sut.EnsureKeyAsync(KeyName));
        mockWebAuthn.Setup(x => x.AuthenticateAsync("bob-cred")).ThrowsAsync(new InvalidOperationException("no WebAuthn"));
        Assert.False(await sut.EnsureKeyAsync(KeyName));
    }

    [Fact]
    public async Task GivenNoPasskey_WhenEnsured_ThenFalse_AndNothingIsAskedFor()
    {
        // Act
        var ensured = await CreateSut().EnsureKeyAsync(KeyName);

        // Assert
        Assert.False(ensured);
        mockWebAuthn.VerifyNoOtherCalls();
    }

    private void RememberBob(bool withPasskey) => saved = new SavedUsersData
    {
        Users = [new SavedUser { Email = "BOB@example.com", WebAuthnCredential = withPasskey ? new WebAuthnStoredCredential { CredentialID = "bob-cred" } : null }],
    };

    private PasskeyKeysService CreateSut() => new(mockAuth.Object, mockStorage.Object, mockWebAuthn.Object, keys);
}
