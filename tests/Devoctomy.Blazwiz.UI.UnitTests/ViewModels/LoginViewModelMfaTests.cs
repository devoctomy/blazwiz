using Devoctomy.Blazwiz.Auth.Exceptions;
using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.Auth.Sessions.Interfaces;
using Devoctomy.Blazwiz.Browser.Models;
using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Core.Offline;
using Devoctomy.Blazwiz.Cryptography.Interfaces;
using Devoctomy.Blazwiz.Testing;
using Devoctomy.Blazwiz.UI.Models;
using Devoctomy.Blazwiz.UI.Services;
using Devoctomy.Blazwiz.UI.Services.Interfaces;
using Devoctomy.Blazwiz.UI.Shell;
using Devoctomy.Blazwiz.UI.Shell.Interfaces;
using Devoctomy.Blazwiz.UI.ViewModels;
using Moq;

namespace Devoctomy.Blazwiz.UI.UnitTests.ViewModels;

// The code from the authenticator app at sign in, setting one up, and the remembered device kept with the passkey.
public class LoginViewModelMfaTests
{
    private const string Email = "user@test.com";

    private readonly FrameworkText testText = new();
    private readonly Mock<IAuthenticationService> mockAuth = new();
    private readonly Mock<INavigationService> mockNavigation = new();
    private readonly Mock<IThemeService> mockTheme = new();
    private readonly Mock<ILocalStorageService> mockLocalStorage = new();
    private readonly Mock<IWebAuthnService> mockWebAuthn = new();
    private readonly Mock<IPasswordCryptoService> mockPasswordCrypto = new();
    private readonly Mock<IDerivedKeyStore> mockKeyStore = new();
    private readonly Mock<ISessionLifecycle> mockLifecycle = new();
    private readonly Mock<IDeviceNameService> mockDeviceName = new();
    private readonly Mock<IQrCodeService> mockQrCode = new();
    private readonly Mock<IClipboardService> mockClipboard = new();
    private readonly OfflineModeService offlineMode = new();
    private readonly AppSettings appSettings = new() { ApplicationTitle = "Test App", EnablePortalLoginAuthn = true };
    private SavedUsersData? saved;

    public LoginViewModelMfaTests()
    {
        mockDeviceName.Setup(x => x.GetNameAsync()).ReturnsAsync("Work laptop");
        mockQrCode.Setup(x => x.CreateImageUrl(It.IsAny<string>())).Returns("data:image/svg+xml;base64,QR");
        mockPasswordCrypto.Setup(x => x.DecryptAsync("enc", Email)).ReturnsAsync("secret");
        mockPasswordCrypto.Setup(x => x.EncryptAsync(It.IsAny<string>(), Email)).ReturnsAsync((string plain, string _) => "enc:" + plain);
        mockWebAuthn.Setup(x => x.AuthenticateAsync("cred1")).ReturnsAsync(new WebAuthnAuthenticateResult { Success = true, KeysDerived = true });
        mockLocalStorage
            .Setup(x => x.SetObjectAsync(LoginViewModel.SavedUsersStorageKey, It.IsAny<SavedUsersData>()))
            .Callback<string, SavedUsersData>((_, data) => saved = data)
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task GivenCodeAskedFor_WhenLoginAsync_ThenTheCodeStep()
    {
        // Arrange
        mockAuth.Setup(x => x.LoginAsync("bob", "pw")).ReturnsAsync(new LoginResult { MfaCodeRequired = true });
        var sut = CreateTypedSut();
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // Act
        await sut.LoginAsync();

        // Assert
        Assert.True(sut.IsMfaCodeRequired);
        Assert.False(sut.IsMfaSetupRequired);
        Assert.True(sut.IsMfaStep);
        Assert.True(sut.IsChallenged);
        Assert.Contains(nameof(LoginViewModel.IsChallenged), changed);
        Assert.Null(sut.ErrorMessage);
        mockNavigation.Verify(x => x.NavigateTo(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GivenCodeAskedFor_WhenCompleteMfaAsync_ThenSignedIn()
    {
        // Arrange
        mockAuth.Setup(x => x.LoginAsync("bob", "pw")).ReturnsAsync(new LoginResult { MfaCodeRequired = true });
        mockAuth.Setup(x => x.CompleteMfaAsync("123456")).ReturnsAsync(new LoginResult { Success = true });
        var sut = CreateTypedSut();
        await sut.LoginAsync();
        sut.MfaCode = "123456";

        // Act
        await sut.CompleteMfaAsync();

        // Assert
        Assert.False(sut.IsMfaStep);
        Assert.Equal(string.Empty, sut.MfaCode);
        mockLifecycle.Verify(x => x.SignedInAsync(), Times.Once);
        mockNavigation.Verify(x => x.NavigateTo("/"), Times.Once);
    }

    [Fact]
    public async Task GivenWrongCode_WhenCompleteMfaAsync_ThenTheErrorAndTheStepStays()
    {
        // Arrange
        mockAuth.Setup(x => x.LoginAsync("bob", "pw")).ReturnsAsync(new LoginResult { MfaCodeRequired = true });
        mockAuth.Setup(x => x.CompleteMfaAsync("000000")).ReturnsAsync(new LoginResult { ErrorCode = AuthErrorCodes.CodeMismatch });
        var sut = CreateTypedSut();
        await sut.LoginAsync();
        sut.MfaCode = "000000";

        // Act
        await sut.CompleteMfaAsync();

        // Assert
        Assert.True(sut.IsMfaCodeRequired);
        Assert.Equal(testText.Errors.Describe(AuthErrorCodes.CodeMismatch, []), sut.ErrorMessage);
        Assert.Equal(string.Empty, sut.MfaCode);
    }

    [Fact]
    public async Task GivenCompletingThrows_WhenCompleteMfaAsync_ThenAGeneralError()
    {
        // Arrange
        mockAuth.Setup(x => x.LoginAsync("bob", "pw")).ReturnsAsync(new LoginResult { MfaCodeRequired = true });
        mockAuth.Setup(x => x.CompleteMfaAsync(It.IsAny<string>())).ThrowsAsync(new InvalidOperationException("boom"));
        var sut = CreateTypedSut();
        await sut.LoginAsync();

        // Act
        await sut.CompleteMfaAsync();

        // Assert
        Assert.Equal(testText.Ui["Login.Error.Failed"].Value, sut.ErrorMessage);
        Assert.False(sut.IsBusy);
    }

    [Fact]
    public async Task GivenNoCodeStep_WhenCompleteMfaAsync_ThenNothingHappens()
    {
        // Arrange
        var sut = CreateTypedSut();

        // Act
        await sut.CompleteMfaAsync();

        // Assert
        mockAuth.Verify(x => x.CompleteMfaAsync(It.IsAny<string>()), Times.Never);
        mockAuth.Verify(x => x.CompleteMfaSetupAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GivenSetUpRequired_WhenLoginAsync_ThenTheSetUpStepWithTheSecret()
    {
        // Arrange
        mockAuth.Setup(x => x.LoginAsync("bob", "pw")).ReturnsAsync(new LoginResult { MfaSetupSecret = "JBSWY3DPEHPK3PXP" });
        var sut = CreateTypedSut();
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // Act
        await sut.LoginAsync();

        // Assert
        Assert.True(sut.IsMfaSetupRequired);
        Assert.True(sut.IsMfaStep);
        Assert.True(sut.MfaSetup.IsShown);
        Assert.Equal("JBSWY3DPEHPK3PXP", sut.MfaSetup.Secret);
        Assert.Contains(nameof(LoginViewModel.MfaSetup), changed);
    }

    [Fact]
    public async Task GivenSetUpStep_WhenCompleteMfaAsync_ThenTheFirstCodeFinishesSigningIn()
    {
        // Arrange
        mockAuth.Setup(x => x.LoginAsync("bob", "pw")).ReturnsAsync(new LoginResult { MfaSetupSecret = "JBSWY3DPEHPK3PXP" });
        mockAuth.Setup(x => x.CompleteMfaSetupAsync("123456")).ReturnsAsync(new LoginResult { Success = true });
        var sut = CreateTypedSut();
        await sut.LoginAsync();
        sut.MfaCode = "123456";

        // Act
        await sut.CompleteMfaAsync();

        // Assert
        Assert.False(sut.IsMfaSetupRequired);
        Assert.False(sut.MfaSetup.IsShown);
        mockNavigation.Verify(x => x.NavigateTo("/"), Times.Once);
    }

    [Fact]
    public async Task GivenNewPasswordThenCode_WhenCompleteNewPasswordAsync_ThenTheCodeStep_AndTheNewPasswordIsKept()
    {
        // Arrange
        mockAuth.Setup(x => x.LoginAsync("bob", "pw")).ReturnsAsync(new LoginResult { NewPasswordRequired = true });
        mockAuth.Setup(x => x.CompleteNewPasswordAsync("NewPassword123")).ReturnsAsync(new LoginResult { MfaCodeRequired = true });
        var sut = CreateTypedSut();
        await sut.LoginAsync();
        sut.NewPassword = "NewPassword123";
        sut.ConfirmNewPassword = "NewPassword123";

        // Act
        await sut.CompleteNewPasswordAsync();

        // Assert
        Assert.False(sut.IsNewPasswordRequired);
        Assert.True(sut.IsMfaCodeRequired);
        Assert.Equal("NewPassword123", sut.Password);
    }

    [Fact]
    public async Task GivenCodeStepAfterTypingThePassword_WhenCancelMfa_ThenBackToTheForm()
    {
        // Arrange
        mockAuth.Setup(x => x.LoginAsync("bob", "pw")).ReturnsAsync(new LoginResult { MfaSetupSecret = "JBSWY3DPEHPK3PXP" });
        var sut = CreateTypedSut();
        await sut.LoginAsync();

        // Act
        sut.CancelMfa();

        // Assert
        Assert.False(sut.IsMfaStep);
        Assert.False(sut.MfaSetup.IsShown);
        Assert.Equal("pw", sut.Password);
        mockKeyStore.Verify(x => x.Clear(), Times.Never);
    }

    [Fact]
    public async Task GivenPasskeySignInAskedForACode_WhenCompleteMfaAsync_ThenThisDeviceIsRemembered_AndItsCredentialKept()
    {
        // Arrange
        var user = PasskeyUser();
        var sut = await CreatePasskeySutAsync(user);
        mockAuth.Setup(x => x.LoginAsync(Email, "secret", null)).ReturnsAsync(new LoginResult { MfaCodeRequired = true });
        mockAuth.Setup(x => x.CompleteMfaAsync("123456")).ReturnsAsync(new LoginResult { Success = true });
        mockAuth.Setup(x => x.RememberDeviceAsync("Work laptop")).ReturnsAsync("device-credential");
        await sut.SelectUserCommand.ExecuteAsync(user);
        Assert.True(sut.IsMfaCodeRequired);
        sut.MfaCode = "123456";

        // Act
        await sut.CompleteMfaAsync();

        // Assert
        Assert.Equal("enc:device-credential", user.WebAuthnCredential!.EncryptedDevice);
        Assert.Equal("enc:device-credential", saved?.Users.Single().WebAuthnCredential?.EncryptedDevice);
        mockNavigation.Verify(x => x.NavigateTo("/"), Times.Once);
        mockKeyStore.Verify(x => x.Clear(), Times.Once); // Only when the page opened, before the passkey gave them.
    }

    [Fact]
    public async Task GivenRememberedDevice_WhenSelectUser_ThenSignsInWithIt_WithoutTheCode()
    {
        // Arrange
        var user = PasskeyUser();
        user.WebAuthnCredential!.EncryptedDevice = "enc-device";
        mockPasswordCrypto.Setup(x => x.DecryptAsync("enc-device", Email)).ReturnsAsync("device-credential");
        var sut = await CreatePasskeySutAsync(user);
        mockAuth.Setup(x => x.LoginAsync(Email, "secret", "device-credential")).ReturnsAsync(new LoginResult { Success = true });

        // Act
        await sut.SelectUserCommand.ExecuteAsync(user);

        // Assert
        Assert.False(sut.IsMfaStep);
        Assert.Equal("enc-device", user.WebAuthnCredential.EncryptedDevice);
        mockNavigation.Verify(x => x.NavigateTo("/"), Times.Once);
    }

    [Fact]
    public async Task GivenRememberedDeviceForgottenElsewhere_WhenSelectUser_ThenItsCredentialIsDropped_AndTheCodeAskedFor()
    {
        // Arrange
        var user = PasskeyUser();
        user.WebAuthnCredential!.EncryptedDevice = "enc-device";
        mockPasswordCrypto.Setup(x => x.DecryptAsync("enc-device", Email)).ReturnsAsync("device-credential");
        var sut = await CreatePasskeySutAsync(user);
        mockAuth.Setup(x => x.LoginAsync(Email, "secret", "device-credential")).ReturnsAsync(new LoginResult { MfaCodeRequired = true, RememberedDeviceRejected = true });

        // Act
        await sut.SelectUserCommand.ExecuteAsync(user);

        // Assert
        Assert.True(sut.IsMfaCodeRequired);
        Assert.Null(user.WebAuthnCredential.EncryptedDevice);
        Assert.Null(saved?.Users.Single().WebAuthnCredential?.EncryptedDevice);
    }

    [Fact]
    public async Task GivenSavingFails_WhenARejectedDeviceIsDropped_ThenTheSignInCarriesOn()
    {
        // Arrange
        var user = PasskeyUser();
        user.WebAuthnCredential!.EncryptedDevice = "enc-device";
        mockPasswordCrypto.Setup(x => x.DecryptAsync("enc-device", Email)).ReturnsAsync("device-credential");
        var sut = await CreatePasskeySutAsync(user);
        mockLocalStorage
            .Setup(x => x.SetObjectAsync(LoginViewModel.SavedUsersStorageKey, It.IsAny<SavedUsersData>()))
            .ThrowsAsync(new InvalidOperationException("quota"));
        mockAuth.Setup(x => x.LoginAsync(Email, "secret", "device-credential")).ReturnsAsync(new LoginResult { MfaCodeRequired = true, RememberedDeviceRejected = true });

        // Act
        await sut.SelectUserCommand.ExecuteAsync(user);

        // Assert
        Assert.True(sut.IsMfaCodeRequired);
        Assert.Null(sut.ErrorMessage);
    }

    [Fact]
    public async Task GivenCodeBeingChecked_WhenCompleteMfaAsyncAgain_ThenIgnored()
    {
        // Arrange
        mockAuth.Setup(x => x.LoginAsync("bob", "pw")).ReturnsAsync(new LoginResult { MfaCodeRequired = true });
        var pending = new TaskCompletionSource<LoginResult>();
        mockAuth.Setup(x => x.CompleteMfaAsync(It.IsAny<string>())).Returns(pending.Task);
        var sut = CreateTypedSut();
        await sut.LoginAsync();
        var first = sut.CompleteMfaAsync();

        // Act
        await sut.CompleteMfaAsync();
        pending.SetResult(new LoginResult { Success = true });
        await first;

        // Assert
        mockAuth.Verify(x => x.CompleteMfaAsync(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task GivenNewPasswordAskedFor_WhenLoginAsync_ThenChallenged_ButNoCodeStep()
    {
        // Arrange
        mockAuth.Setup(x => x.LoginAsync("bob", "pw")).ReturnsAsync(new LoginResult { NewPasswordRequired = true });
        var sut = CreateTypedSut();

        // Act
        await sut.LoginAsync();

        // Assert
        Assert.True(sut.IsChallenged);
        Assert.False(sut.IsMfaStep);
    }

    [Fact]
    public async Task GivenUnreadableDeviceCredential_WhenSelectUser_ThenSignsInWithoutIt()
    {
        // Arrange
        var user = PasskeyUser();
        user.WebAuthnCredential!.EncryptedDevice = "garbled";
        mockPasswordCrypto.Setup(x => x.DecryptAsync("garbled", Email)).ThrowsAsync(new InvalidOperationException("bad tag"));
        var sut = await CreatePasskeySutAsync(user);
        mockAuth.Setup(x => x.LoginAsync(Email, "secret", null)).ReturnsAsync(new LoginResult { Success = true });

        // Act
        await sut.SelectUserCommand.ExecuteAsync(user);

        // Assert
        mockNavigation.Verify(x => x.NavigateTo("/"), Times.Once);
    }

    [Fact]
    public async Task GivenPasskeySignInWithNothingToRemember_WhenSelectUser_ThenNothingStored()
    {
        // Arrange
        var user = PasskeyUser();
        var sut = await CreatePasskeySutAsync(user);
        mockAuth.Setup(x => x.LoginAsync(Email, "secret", null)).ReturnsAsync(new LoginResult { Success = true });
        mockAuth.Setup(x => x.RememberDeviceAsync(It.IsAny<string>())).ReturnsAsync((string?)null);

        // Act
        await sut.SelectUserCommand.ExecuteAsync(user);

        // Assert
        Assert.Null(user.WebAuthnCredential!.EncryptedDevice);
        Assert.Null(saved);
    }

    [Fact]
    public async Task GivenRememberingTheDeviceThrows_WhenSelectUser_ThenSignedInAllTheSame()
    {
        // Arrange
        var user = PasskeyUser();
        var sut = await CreatePasskeySutAsync(user);
        mockAuth.Setup(x => x.LoginAsync(Email, "secret", null)).ReturnsAsync(new LoginResult { Success = true });
        mockAuth.Setup(x => x.RememberDeviceAsync(It.IsAny<string>())).ThrowsAsync(new InvalidOperationException("boom"));

        // Act
        await sut.SelectUserCommand.ExecuteAsync(user);

        // Assert
        Assert.Null(user.WebAuthnCredential!.EncryptedDevice);
        mockNavigation.Verify(x => x.NavigateTo("/"), Times.Once);
    }

    [Fact]
    public async Task GivenPasskeySignInAskedForACode_WhenCancelMfa_ThenTheKeysAreForgotten_AndBackToTheUsers()
    {
        // Arrange
        var user = PasskeyUser();
        var sut = await CreatePasskeySutAsync(user);
        mockAuth.Setup(x => x.LoginAsync(Email, "secret", null)).ReturnsAsync(new LoginResult { MfaCodeRequired = true });
        await sut.SelectUserCommand.ExecuteAsync(user);

        // Act
        sut.CancelMfa();

        // Assert
        Assert.False(sut.IsMfaStep);
        Assert.True(sut.ShowUsersList);
        Assert.Equal(string.Empty, sut.Password);
        mockKeyStore.Verify(x => x.Clear(), Times.Exactly(2)); // When the page opened, and now.
    }

    [Fact]
    public async Task GivenPasskeySetUpAfterSigningIn_WhenHandleWebAuthnSetupAsync_ThenTheDeviceIsRememberedWithThePassword()
    {
        // Arrange
        mockAuth.Setup(x => x.LoginAsync(Email, "pw")).ReturnsAsync(new LoginResult { Success = true });
        mockAuth.Setup(x => x.RememberDeviceAsync("Work laptop")).ReturnsAsync("device-credential");
        mockWebAuthn.Setup(x => x.RegisterAsync(Email)).ReturnsAsync(new WebAuthnRegisterResult { Success = true, PrfEnabled = true, KeysDerived = true, CredentialID = "cred1" });
        var sut = CreateSut();
        sut.Username = Email;
        sut.Password = "pw";
        sut.RememberMe = true;
        await sut.LoginAsync();
        Assert.True(sut.ShowWebAuthnPrompt);

        // Act
        await sut.HandleWebAuthnSetupAsync();

        // Assert
        var credential = saved?.Users.Single().WebAuthnCredential;
        Assert.Equal("enc:pw", credential?.EncryptedPassword);
        Assert.Equal("enc:device-credential", credential?.EncryptedDevice);
    }

    [Fact]
    public void GivenSut_WhenDisposed_ThenNoLongerListensToTheSetUp()
    {
        // Arrange
        var setup = new AuthenticatorSetupViewModel(mockQrCode.Object, mockClipboard.Object, appSettings);
        var sut = CreateSut(setup);
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // Act
        sut.Dispose();
        setup.Show("JBSWY3DPEHPK3PXP", Email);

        // Assert
        Assert.DoesNotContain(nameof(LoginViewModel.MfaSetup), changed);
    }

    private static SavedUser PasskeyUser() => new()
    {
        Email = Email,
        WebAuthnCredential = new WebAuthnStoredCredential { CredentialID = "cred1", EncryptedPassword = "enc" },
    };

    private async Task<LoginViewModel> CreatePasskeySutAsync(SavedUser user)
    {
        mockLocalStorage
            .Setup(x => x.GetObjectAsync<SavedUsersData>(LoginViewModel.SavedUsersStorageKey))
            .ReturnsAsync(new SavedUsersData { Users = [user] });
        var sut = CreateSut();
        await sut.OnInitializedAsync();
        return sut;
    }

    private LoginViewModel CreateTypedSut()
    {
        var sut = CreateSut();
        sut.Username = "bob";
        sut.Password = "pw";
        return sut;
    }

    private LoginViewModel CreateSut(AuthenticatorSetupViewModel? setup = null) => new(
        mockAuth.Object,
        mockNavigation.Object,
        mockTheme.Object,
        mockLocalStorage.Object,
        mockWebAuthn.Object,
        mockPasswordCrypto.Object,
        mockKeyStore.Object,
        offlineMode,
        new List<ISignInOption>(),
        new List<SignInOptionRegistration>(),
        mockLifecycle.Object,
        appSettings,
        testText.Ui,
        testText.Errors,
        new SessionEndedNoticeService(),
        mockDeviceName.Object,
        setup ?? new AuthenticatorSetupViewModel(mockQrCode.Object, mockClipboard.Object, appSettings));
}
