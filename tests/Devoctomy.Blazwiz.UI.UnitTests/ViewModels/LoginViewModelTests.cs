using Devoctomy.Blazwiz.Testing;
using Devoctomy.Blazwiz.Auth.Exceptions;
using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.Auth.Sessions.Interfaces;
using Devoctomy.Blazwiz.Browser.Models;
using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Core.Offline;
using Devoctomy.Blazwiz.Core.Offline.Models;
using Devoctomy.Blazwiz.Cryptography.Interfaces;
using Devoctomy.Blazwiz.UI.Models;
using Devoctomy.Blazwiz.UI.Services.Interfaces;
using Devoctomy.Blazwiz.UI.Shell;
using Devoctomy.Blazwiz.UI.Shell.Interfaces;
using Devoctomy.Blazwiz.UI.ViewModels;
using Moq;

namespace Devoctomy.Blazwiz.UI.UnitTests.ViewModels;

public class LoginViewModelTests
{
    private readonly FrameworkText testText = new();

    private readonly Mock<IAuthenticationService> mockAuth = new();
    private readonly Mock<INavigationService> mockNavigation = new();
    private readonly Mock<IThemeService> mockTheme = new();
    private readonly Mock<ILocalStorageService> mockLocalStorage = new();
    private readonly Mock<IWebAuthnService> mockWebAuthn = new();
    private readonly Mock<IPasswordCryptoService> mockPasswordCrypto = new();
    private readonly Mock<IDerivedKeyStore> mockKeyStore = new();
    private readonly OfflineModeService offlineMode = new();
    private readonly List<ISignInOption> options = [];
    private readonly List<SignInOptionRegistration> optionRegistrations = [];
    private readonly Mock<ISessionLifecycle> mockLifecycle = new();
    private readonly AppSettings appSettings = new() { ApplicationTitle = "Test App", EnablePortalLoginAuthn = true };

    public LoginViewModelTests()
    {
        mockAuth.Setup(x => x.SignInOfflineAsync(It.IsAny<string>(), It.IsAny<OfflineEntryReason>())).ReturnsAsync(new LoginResult { Success = true });
    }

    [Fact]
    public void GivenNullAuthService_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Arrange
        var exception = Assert.Throws<ArgumentNullException>(() => new LoginViewModel(null!, mockNavigation.Object, mockTheme.Object, mockLocalStorage.Object, mockWebAuthn.Object, mockPasswordCrypto.Object, mockKeyStore.Object, offlineMode,
            options,
            optionRegistrations,
            mockLifecycle.Object,
            appSettings,

        // Act
        testText.Ui, testText.Errors));

        // Assert
        Assert.Equal("authService", exception.ParamName);
    }

    [Fact]
    public void GivenNullNavigationService_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Arrange
        var exception = Assert.Throws<ArgumentNullException>(() => new LoginViewModel(mockAuth.Object, null!, mockTheme.Object, mockLocalStorage.Object, mockWebAuthn.Object, mockPasswordCrypto.Object, mockKeyStore.Object, offlineMode,
            options,
            optionRegistrations,
            mockLifecycle.Object,
            appSettings,

        // Act
        testText.Ui, testText.Errors));

        // Assert
        Assert.Equal("navigationService", exception.ParamName);
    }

    [Fact]
    public void GivenNullThemeService_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Arrange
        var exception = Assert.Throws<ArgumentNullException>(() => new LoginViewModel(mockAuth.Object, mockNavigation.Object, null!, mockLocalStorage.Object, mockWebAuthn.Object, mockPasswordCrypto.Object, mockKeyStore.Object, offlineMode,
            options,
            optionRegistrations,
            mockLifecycle.Object,
            appSettings,

        // Act
        testText.Ui, testText.Errors));

        // Assert
        Assert.Equal("themeService", exception.ParamName);
    }

    [Fact]
    public void GivenNullLocalStorageService_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Arrange
        var exception = Assert.Throws<ArgumentNullException>(() => new LoginViewModel(mockAuth.Object, mockNavigation.Object, mockTheme.Object, null!, mockWebAuthn.Object, mockPasswordCrypto.Object, mockKeyStore.Object, offlineMode,
            options,
            optionRegistrations,
            mockLifecycle.Object,
            appSettings,

        // Act
        testText.Ui, testText.Errors));

        // Assert
        Assert.Equal("localStorageService", exception.ParamName);
    }

    [Fact]
    public void GivenNullWebAuthnService_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Arrange
        var exception = Assert.Throws<ArgumentNullException>(() => new LoginViewModel(mockAuth.Object, mockNavigation.Object, mockTheme.Object, mockLocalStorage.Object, null!, mockPasswordCrypto.Object, mockKeyStore.Object, offlineMode,
            options,
            optionRegistrations,
            mockLifecycle.Object,
            appSettings,

        // Act
        testText.Ui, testText.Errors));

        // Assert
        Assert.Equal("webAuthnService", exception.ParamName);
    }

    [Fact]
    public void GivenNullPasswordCryptoService_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Arrange
        var exception = Assert.Throws<ArgumentNullException>(() => new LoginViewModel(mockAuth.Object, mockNavigation.Object, mockTheme.Object, mockLocalStorage.Object, mockWebAuthn.Object, null!, mockKeyStore.Object, offlineMode,
            options,
            optionRegistrations,
            mockLifecycle.Object,
            appSettings,

        // Act
        testText.Ui, testText.Errors));

        // Assert
        Assert.Equal("passwordCryptoService", exception.ParamName);
    }

    [Fact]
    public void GivenNullKeyStore_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Arrange
        var exception = Assert.Throws<ArgumentNullException>(() => new LoginViewModel(mockAuth.Object, mockNavigation.Object, mockTheme.Object, mockLocalStorage.Object, mockWebAuthn.Object, mockPasswordCrypto.Object, null!, offlineMode,
            options,
            optionRegistrations,
            mockLifecycle.Object,
            appSettings,

        // Act
        testText.Ui, testText.Errors));

        // Assert
        Assert.Equal("keyStore", exception.ParamName);
    }

    [Fact]
    public void GivenNullAppSettings_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Arrange
        var exception = Assert.Throws<ArgumentNullException>(() => new LoginViewModel(mockAuth.Object, mockNavigation.Object, mockTheme.Object, mockLocalStorage.Object, mockWebAuthn.Object, mockPasswordCrypto.Object, mockKeyStore.Object, offlineMode,
            options,
            optionRegistrations,
            mockLifecycle.Object,
            null!,

        // Act
        testText.Ui, testText.Errors));

        // Assert
        Assert.Equal("appSettings", exception.ParamName);
    }

    [Fact]
    public void GivenAppSettings_WhenConstructed_ThenExposesSettings()
    {
        // Act
        var sut = CreateSut();

        // Assert
        Assert.Same(appSettings, sut.AppSettings);
        Assert.Equal(string.Empty, sut.Username);
        Assert.Equal(string.Empty, sut.Password);
        Assert.False(sut.IsBusy);
        Assert.Null(sut.ErrorMessage);
    }

    [Theory]
    [InlineData(true, "bi-sun")]
    [InlineData(false, "bi-moon-stars")]
    public void GivenTheme_WhenGettingThemeIcon_ThenReturnsIconForOtherTheme(bool isDark, string expected)
    {
        // Arrange
        mockTheme.SetupGet(x => x.IsDark).Returns(isDark);
        var sut = CreateSut();

        // Act & Assert
        Assert.Equal(expected, sut.ThemeIcon);
    }

    [Fact]
    public async Task GivenNoSession_WhenInitialized_ThenInitializesThemeAndStaysOnLogin()
    {
        // Arrange
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync((UserSession?)null);
        var sut = CreateSut();

        // Act
        await sut.OnInitializedAsync();

        // Assert
        mockTheme.Verify(x => x.InitializeAsync(), Times.Once);
        mockNavigation.Verify(x => x.NavigateTo(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GivenNoSession_WhenInitialized_ThenDerivedKeysAreCleared()
    {
        // Arrange
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync((UserSession?)null);
        var sut = CreateSut();

        // Act
        await sut.OnInitializedAsync();

        // Assert
        mockKeyStore.Verify(x => x.Clear(), Times.Once);
    }

    [Fact]
    public async Task GivenExistingSession_WhenInitialized_ThenNavigatesHome()
    {
        // Arrange
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(new UserSession());
        var sut = CreateSut();

        // Act
        await sut.OnInitializedAsync();

        // Assert
        mockNavigation.Verify(x => x.NavigateTo("/"), Times.Once);
        mockKeyStore.Verify(x => x.Clear(), Times.Never);
    }

    [Fact]
    public async Task GivenValidCredentials_WhenLogin_ThenNavigatesHomeAndClearsPassword()
    {
        // Arrange
        mockAuth.Setup(x => x.LoginAsync("bob", "secret")).ReturnsAsync(new LoginResult { Success = true });
        var sut = CreateSut();
        sut.Username = "bob";
        sut.Password = "secret";

        // Act
        await sut.LoginCommand.ExecuteAsync(null);

        // Assert
        mockNavigation.Verify(x => x.NavigateTo("/"), Times.Once);
        Assert.Equal(string.Empty, sut.Password);
        Assert.Null(sut.ErrorMessage);
        Assert.False(sut.IsBusy);
    }

    [Fact]
    public async Task GivenFailedLogin_WhenLogin_ThenShowsErrorAndDoesNotNavigate()
    {
        // Arrange
        mockAuth.Setup(x => x.LoginAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(new LoginResult { Success = false, ErrorCode = AuthErrorCodes.NotAuthorized });
        var sut = CreateSut();

        // Act
        await sut.LoginAsync();

        // Assert
        Assert.Equal("Incorrect email or password.", sut.ErrorMessage);
        Assert.False(sut.IsBusy);
        mockNavigation.Verify(x => x.NavigateTo(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GivenFailedLoginWithoutError_WhenLogin_ThenShowsGenericError()
    {
        // Arrange
        mockAuth.Setup(x => x.LoginAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(new LoginResult { Success = false });
        var sut = CreateSut();

        // Act
        await sut.LoginAsync();

        // Assert
        Assert.Equal("Sign in failed. Please try again.", sut.ErrorMessage);
    }

    [Fact]
    public async Task GivenAuthServiceThrows_WhenLogin_ThenShowsExceptionMessage()
    {
        // Arrange
        mockAuth.Setup(x => x.LoginAsync(It.IsAny<string>(), It.IsAny<string>())).ThrowsAsync(new InvalidOperationException("Boom"));
        var sut = CreateSut();

        // Act
        await sut.LoginAsync();

        // Assert
        Assert.Equal("Sign in failed. Please try again.", sut.ErrorMessage);
        Assert.False(sut.IsBusy);
    }

    [Fact]
    public async Task GivenLoginInProgress_WhenLoginAgain_ThenSecondAttemptIsIgnored()
    {
        // Arrange
        var pending = new TaskCompletionSource<LoginResult>();
        mockAuth.Setup(x => x.LoginAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(pending.Task);
        var sut = CreateSut();

        // Act
        var first = sut.LoginAsync();
        Assert.True(sut.IsBusy);
        await sut.LoginAsync();
        pending.SetResult(new LoginResult { Success = true });
        await first;

        // Assert
        mockAuth.Verify(x => x.LoginAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task GivenPreviousError_WhenLoginSucceeds_ThenErrorIsCleared()
    {
        // Arrange
        mockAuth.SetupSequence(x => x.LoginAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new LoginResult { Success = false, ErrorCode = AuthErrorCodes.NotAuthorized })
            .ReturnsAsync(new LoginResult { Success = true });
        var sut = CreateSut();
        await sut.LoginAsync();

        // Act
        await sut.LoginAsync();

        // Assert
        Assert.Null(sut.ErrorMessage);
    }

    [Fact]
    public async Task GivenLoginPage_WhenToggleTheme_ThenTogglesThemeAndNotifiesIcon()
    {
        // Arrange
        var sut = CreateSut();
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // Act
        await sut.ToggleThemeCommand.ExecuteAsync(null);

        // Assert
        mockTheme.Verify(x => x.ToggleThemeAsync(), Times.Once);
        Assert.Contains(nameof(LoginViewModel.ThemeIcon), changed);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GivenAuthService_WhenGettingIsPlaceholderLogin_ThenReflectsService(bool isPlaceholder)
    {
        // Arrange
        mockAuth.SetupGet(x => x.IsPlaceholder).Returns(isPlaceholder);

        // Act & Assert
        Assert.Equal(isPlaceholder, CreateSut().IsPlaceholderLogin);
    }

    [Fact]
    public async Task GivenNewPasswordRequired_WhenLogin_ThenShowsNewPasswordFormWithoutError()
    {
        // Arrange
        mockAuth.Setup(x => x.LoginAsync("bob", "temp")).ReturnsAsync(new LoginResult { NewPasswordRequired = true });
        var sut = CreateSut();
        sut.Username = "bob";
        sut.Password = "temp";

        // Act
        await sut.LoginAsync();

        // Assert
        Assert.True(sut.IsNewPasswordRequired);
        Assert.Equal(string.Empty, sut.Password);
        Assert.Null(sut.ErrorMessage);
        mockNavigation.Verify(x => x.NavigateTo(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GivenMatchingPasswords_WhenCompleteNewPassword_ThenSignsInAndNavigatesHome()
    {
        // Arrange
        var sut = await CreateSutAwaitingNewPasswordAsync();
        mockAuth.Setup(x => x.CompleteNewPasswordAsync("NewPassword123")).ReturnsAsync(new LoginResult { Success = true });
        sut.NewPassword = "NewPassword123";
        sut.ConfirmNewPassword = "NewPassword123";

        // Act
        await sut.CompleteNewPasswordCommand.ExecuteAsync(null);

        // Assert
        mockNavigation.Verify(x => x.NavigateTo("/"), Times.Once);
        Assert.False(sut.IsNewPasswordRequired);
        Assert.Equal(string.Empty, sut.NewPassword);
        Assert.Equal(string.Empty, sut.ConfirmNewPassword);
        Assert.False(sut.IsBusy);
    }

    [Fact]
    public async Task GivenMismatchedPasswords_WhenCompleteNewPassword_ThenShowsErrorWithoutCallingService()
    {
        // Arrange
        var sut = await CreateSutAwaitingNewPasswordAsync();
        sut.NewPassword = "NewPassword123";
        sut.ConfirmNewPassword = "NewPassword124";

        // Act
        await sut.CompleteNewPasswordAsync();

        // Assert
        Assert.Equal("The passwords do not match.", sut.ErrorMessage);
        mockAuth.Verify(x => x.CompleteNewPasswordAsync(It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData(AuthErrorCodes.InvalidPassword, "That password does not meet the password rules. Choose another one.")]
    [InlineData(null, "Could not set the new password. Please try again.")]
    public async Task GivenRejectedPassword_WhenCompleteNewPassword_ThenShowsErrorAndStaysOnForm(string? error, string expected)
    {
        // Arrange
        var sut = await CreateSutAwaitingNewPasswordAsync();
        mockAuth.Setup(x => x.CompleteNewPasswordAsync(It.IsAny<string>())).ReturnsAsync(new LoginResult { ErrorCode = error });

        // Act
        await sut.CompleteNewPasswordAsync();

        // Assert
        Assert.Equal(expected, sut.ErrorMessage);
        Assert.True(sut.IsNewPasswordRequired);
        Assert.False(sut.IsBusy);
        mockNavigation.Verify(x => x.NavigateTo(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GivenAuthServiceThrows_WhenCompleteNewPassword_ThenShowsExceptionMessage()
    {
        // Arrange
        var sut = await CreateSutAwaitingNewPasswordAsync();
        mockAuth.Setup(x => x.CompleteNewPasswordAsync(It.IsAny<string>())).ThrowsAsync(new InvalidOperationException("Boom"));

        // Act
        await sut.CompleteNewPasswordAsync();

        // Assert
        Assert.Equal("Could not set the new password. Please try again.", sut.ErrorMessage);
        Assert.False(sut.IsBusy);
    }

    [Fact]
    public async Task GivenBusy_WhenCompleteNewPasswordAgain_ThenSecondAttemptIsIgnored()
    {
        // Arrange
        var sut = await CreateSutAwaitingNewPasswordAsync();
        var pending = new TaskCompletionSource<LoginResult>();
        mockAuth.Setup(x => x.CompleteNewPasswordAsync(It.IsAny<string>())).Returns(pending.Task);

        // Act
        var first = sut.CompleteNewPasswordAsync();
        await sut.CompleteNewPasswordAsync();
        pending.SetResult(new LoginResult { Success = true });
        await first;

        // Assert
        mockAuth.Verify(x => x.CompleteNewPasswordAsync(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task GivenNewPasswordForm_WhenCancelNewPassword_ThenReturnsToSignIn()
    {
        // Arrange
        var sut = await CreateSutAwaitingNewPasswordAsync();
        sut.NewPassword = "abc";
        sut.ConfirmNewPassword = "abd";
        await sut.CompleteNewPasswordAsync();

        // Act
        sut.CancelNewPasswordCommand.Execute(null);

        // Assert
        Assert.False(sut.IsNewPasswordRequired);
        Assert.Equal(string.Empty, sut.NewPassword);
        Assert.Equal(string.Empty, sut.ConfirmNewPassword);
        Assert.Null(sut.ErrorMessage);
    }

    [Fact]
    public void GivenNewInstance_WhenCheckingRememberMeState_ThenDefaultValuesAreCorrect()
    {
        // Act
        var sut = CreateSut();

        // Assert
        Assert.True(sut.ShowRememberMe);
        Assert.False(sut.RememberMe);
        Assert.False(sut.ShowUsersList);
        Assert.Empty(sut.SavedUsers);
        Assert.False(sut.ShowWebAuthnPrompt);
        Assert.Null(sut.LoadingUser);
    }

    [Fact]
    public async Task GivenSavedUsers_WhenInitialized_ThenShowsUsersList()
    {
        // Arrange
        SetupSavedUsers(new SavedUser { Email = "user@test.com" });
        var sut = CreateSut();

        // Act
        await sut.OnInitializedAsync();

        // Assert
        Assert.True(sut.ShowUsersList);
        Assert.False(sut.ShowRememberMe);
        Assert.Single(sut.SavedUsers);
    }

    [Fact]
    public async Task GivenExistingSession_WhenInitialized_ThenSavedUsersAreNotLoaded()
    {
        // Arrange
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(new UserSession());
        var sut = CreateSut();

        // Act
        await sut.OnInitializedAsync();

        // Assert
        mockLocalStorage.Verify(x => x.GetObjectAsync<SavedUsersData>(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GivenUserWithNoWebAuthn_WhenSelectUser_ThenUsernameIsSetAndListHidden()
    {
        // Arrange
        var sut = CreateSut();
        var user = new SavedUser { Email = "user@test.com", WebAuthnCredential = null };

        // Act
        await sut.SelectUserCommand.ExecuteAsync(user);

        // Assert
        Assert.Equal("user@test.com", sut.Username);
        Assert.False(sut.ShowUsersList);
        Assert.False(sut.ShowRememberMe);
        Assert.Null(sut.LoadingUser);
        mockWebAuthn.Verify(x => x.AuthenticateAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GivenUserWithWebAuthnAndAuthFails_WhenSelectUser_ThenFallsBackToManualLogin()
    {
        // Arrange
        var sut = CreateSut();
        var user = CreateWebAuthnUser("user@test.com", "cred1");
        mockWebAuthn.Setup(x => x.AuthenticateAsync("cred1")).ReturnsAsync(new WebAuthnAuthenticateResult { Success = false, Error = "auth failed" });

        // Act
        await sut.SelectUserCommand.ExecuteAsync(user);

        // Assert
        Assert.Equal("user@test.com", sut.Username);
        Assert.False(sut.ShowUsersList);
        Assert.Equal("Biometric login error. Please enter your password manually.", sut.ErrorMessage);
        Assert.Null(sut.LoadingUser);
        Assert.False(sut.IsBusy);
        mockAuth.Verify(x => x.LoginAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GivenUserWithWebAuthn_WhenSelectUser_ThenLoadingUserIsThatUserWhileAuthenticating()
    {
        // Arrange
        var sut = CreateSut();
        var user = CreateWebAuthnUser("user@test.com", "cred1");
        SavedUser? capturedLoadingUser = null;
        var capturedIsBusy = false;
        mockWebAuthn.Setup(x => x.AuthenticateAsync("cred1")).Returns(() =>
        {
            capturedLoadingUser = sut.LoadingUser;
            capturedIsBusy = sut.IsBusy;
            return Task.FromResult(new WebAuthnAuthenticateResult { Success = false });
        });

        // Act
        await sut.SelectUserCommand.ExecuteAsync(user);

        // Assert
        Assert.Same(user, capturedLoadingUser);
        Assert.True(capturedIsBusy);
        Assert.Null(sut.LoadingUser);
        Assert.False(sut.IsBusy);
    }

    [Fact]
    public void GivenNullOfflineModeService_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Arrange
        var exception = Assert.Throws<ArgumentNullException>(() => new LoginViewModel(mockAuth.Object, mockNavigation.Object, mockTheme.Object, mockLocalStorage.Object, mockWebAuthn.Object, mockPasswordCrypto.Object, mockKeyStore.Object, null!,
            options,
            optionRegistrations,
            mockLifecycle.Object,
            appSettings,

        // Act
        testText.Ui, testText.Errors));

        // Assert
        Assert.Equal("offlineModeService", exception.ParamName);
    }

    [Fact]
    public async Task GivenSignInOfflineTicked_WhenSelectUser_ThenPasskeyProvesItAndSignsInOfflineWithoutTheSignInService()
    {
        // Arrange
        var sut = CreateSut();
        sut.SignInOffline = true;
        var user = CreateWebAuthnUser("user@test.com", "cred1");
        mockWebAuthn.Setup(x => x.AuthenticateAsync("cred1")).ReturnsAsync(new WebAuthnAuthenticateResult { Success = true, KeysDerived = true });
        mockPasswordCrypto.Setup(x => x.DecryptAsync("enc", It.IsAny<string>())).ReturnsAsync("secret");

        // Act
        await sut.SelectUserCommand.ExecuteAsync(user);

        // Assert
        mockPasswordCrypto.Verify(x => x.DecryptAsync("enc", It.IsAny<string>()), Times.Once);
        mockAuth.Verify(x => x.SignInOfflineAsync("user@test.com", OfflineEntryReason.Chosen), Times.Once);
        mockAuth.Verify(x => x.LoginAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        mockNavigation.Verify(x => x.NavigateTo("/"), Times.Once);
    }

    [Fact]
    public async Task GivenSignInOfflineTickedButPasskeyFails_WhenSelectUser_ThenNotSignedIn()
    {
        // Arrange
        var sut = CreateSut();
        sut.SignInOffline = true;
        var user = CreateWebAuthnUser("user@test.com", "cred1");
        mockWebAuthn.Setup(x => x.AuthenticateAsync("cred1")).ReturnsAsync(new WebAuthnAuthenticateResult { Success = true, KeysDerived = true });
        mockPasswordCrypto.Setup(x => x.DecryptAsync(It.IsAny<string>(), It.IsAny<string>())).ThrowsAsync(new InvalidOperationException("bad tag"));

        // Act
        await sut.SelectUserCommand.ExecuteAsync(user);

        // Assert
        mockAuth.Verify(x => x.SignInOfflineAsync(It.IsAny<string>(), It.IsAny<OfflineEntryReason>()), Times.Never);
        mockNavigation.Verify(x => x.NavigateTo(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GivenSignInServiceUnreachable_WhenSelectUser_ThenSignedInOfflineInstead()
    {
        // Arrange
        var sut = CreateSut();
        var user = CreateWebAuthnUser("user@test.com", "cred1");
        mockWebAuthn.Setup(x => x.AuthenticateAsync("cred1")).ReturnsAsync(new WebAuthnAuthenticateResult { Success = true, KeysDerived = true });
        mockPasswordCrypto.Setup(x => x.DecryptAsync("enc", It.IsAny<string>())).ReturnsAsync("secret");
        mockAuth.Setup(x => x.LoginAsync("user@test.com", "secret")).ReturnsAsync(new LoginResult { Success = false, ServiceUnreachable = true });

        // Act
        await sut.SelectUserCommand.ExecuteAsync(user);

        // Assert
        mockAuth.Verify(x => x.SignInOfflineAsync("user@test.com", OfflineEntryReason.SignInUnreachable), Times.Once);
        mockNavigation.Verify(x => x.NavigateTo("/"), Times.Once);
    }

    [Fact]
    public async Task GivenSignInRefused_WhenSelectUser_ThenNeverSignedInOffline()
    {
        // Arrange
        var sut = CreateSut();
        var user = CreateWebAuthnUser("user@test.com", "cred1");
        mockWebAuthn.Setup(x => x.AuthenticateAsync("cred1")).ReturnsAsync(new WebAuthnAuthenticateResult { Success = true, KeysDerived = true });
        mockPasswordCrypto.Setup(x => x.DecryptAsync("enc", It.IsAny<string>())).ReturnsAsync("secret");
        mockAuth.Setup(x => x.LoginAsync("user@test.com", "secret")).ReturnsAsync(new LoginResult { Success = false, ErrorCode = AuthErrorCodes.SignInFailed });

        // Act
        await sut.SelectUserCommand.ExecuteAsync(user);

        // Assert
        mockAuth.Verify(x => x.SignInOfflineAsync(It.IsAny<string>(), It.IsAny<OfflineEntryReason>()), Times.Never);
        mockNavigation.Verify(x => x.NavigateTo(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GivenOfflineModeOn_WhenSigningInWithAPassword_ThenOfflineModeLeftFirst()
    {
        // Arrange
        offlineMode.Enter(OfflineEntryReason.Chosen);
        var offlineDuringLogin = true;
        mockAuth.Setup(x => x.LoginAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback(() => offlineDuringLogin = offlineMode.IsOffline)
            .ReturnsAsync(new LoginResult { Success = true });
        var sut = CreateSut();
        sut.Username = "bob";
        sut.Password = "pw";

        // Act
        await sut.LoginAsync();

        // Assert
        Assert.False(offlineDuringLogin);
    }

    [Fact]
    public async Task GivenStillInOfflineMode_WhenInitialized_ThenSignInOfflineTicked()
    {
        // Arrange
        offlineMode.Enter(OfflineEntryReason.Chosen);
        var sut = CreateSut();

        // Act
        await sut.OnInitializedAsync();

        // Assert
        Assert.True(sut.SignInOffline);
    }

    [Fact]
    public async Task GivenUserWithWebAuthnAndPrf_WhenSelectUser_ThenDecryptsPasswordAndSignsIn()
    {
        // Arrange
        var sut = CreateSut();
        var user = CreateWebAuthnUser("user@test.com", "cred1");
        mockWebAuthn.Setup(x => x.AuthenticateAsync("cred1")).ReturnsAsync(new WebAuthnAuthenticateResult { Success = true, KeysDerived = true });
        mockPasswordCrypto.Setup(x => x.DecryptAsync("enc", It.IsAny<string>())).ReturnsAsync("secret");
        mockAuth.Setup(x => x.LoginAsync("user@test.com", "secret")).ReturnsAsync(new LoginResult { Success = true });

        // Act
        await sut.SelectUserCommand.ExecuteAsync(user);

        // Assert
        mockNavigation.Verify(x => x.NavigateTo("/"), Times.Once);
        Assert.Null(sut.ErrorMessage);
        mockKeyStore.Verify(x => x.Clear(), Times.Never);
    }

    [Fact]
    public async Task GivenDecryptedPasswordRejected_WhenSelectUser_ThenFallsBackToManualLogin()
    {
        // Arrange
        var sut = CreateSut();
        var user = CreateWebAuthnUser("user@test.com", "cred1");
        mockWebAuthn.Setup(x => x.AuthenticateAsync("cred1")).ReturnsAsync(new WebAuthnAuthenticateResult { Success = true, KeysDerived = true });
        mockPasswordCrypto.Setup(x => x.DecryptAsync("enc", It.IsAny<string>())).ReturnsAsync("stale");
        mockAuth.Setup(x => x.LoginAsync("user@test.com", "stale")).ReturnsAsync(new LoginResult { Success = false });

        // Act
        await sut.SelectUserCommand.ExecuteAsync(user);

        // Assert
        Assert.Equal("Biometric login failed. Please enter your password manually.", sut.ErrorMessage);
        Assert.Equal("user@test.com", sut.Username);
        mockNavigation.Verify(x => x.NavigateTo(It.IsAny<string>()), Times.Never);
        mockKeyStore.Verify(x => x.Clear(), Times.Once);
    }

    [Fact]
    public async Task GivenDecryptThrows_WhenSelectUser_ThenFallsBackToManualLogin()
    {
        // Arrange
        var sut = CreateSut();
        var user = CreateWebAuthnUser("user@test.com", "cred1");
        mockWebAuthn.Setup(x => x.AuthenticateAsync("cred1")).ReturnsAsync(new WebAuthnAuthenticateResult { Success = true, KeysDerived = true });
        mockPasswordCrypto.Setup(x => x.DecryptAsync(It.IsAny<string>(), It.IsAny<string>())).ThrowsAsync(new InvalidOperationException("bad tag"));

        // Act
        await sut.SelectUserCommand.ExecuteAsync(user);

        // Assert
        Assert.Equal("Biometric login error. Please enter your password manually.", sut.ErrorMessage);
        Assert.False(sut.ShowUsersList);
        Assert.False(sut.IsBusy);
        mockKeyStore.Verify(x => x.Clear(), Times.Once);
    }

    [Fact]
    public async Task GivenAuthenticatedButNoKeysDerived_WhenSelectUser_ThenFallsBackToManualLoginWithoutDecrypting()
    {
        // Arrange
        var sut = CreateSut();
        var user = CreateWebAuthnUser("user@test.com", "cred1");
        mockWebAuthn.Setup(x => x.AuthenticateAsync("cred1")).ReturnsAsync(new WebAuthnAuthenticateResult { Success = true, KeysDerived = false });

        // Act
        await sut.SelectUserCommand.ExecuteAsync(user);

        // Assert
        Assert.Equal("Biometric login error. Please enter your password manually.", sut.ErrorMessage);
        mockPasswordCrypto.Verify(x => x.DecryptAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        mockAuth.Verify(x => x.LoginAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GivenTwoSavedUsers_WhenRemoveUser_ThenUserIsRemovedAndListPersisted()
    {
        // Arrange
        SetupSavedUsers(new SavedUser { Email = "user1@test.com" }, new SavedUser { Email = "user2@test.com" });
        var sut = CreateSut();
        await sut.OnInitializedAsync();

        // Act
        await sut.RemoveUserCommand.ExecuteAsync(sut.SavedUsers[0]);

        // Assert
        Assert.Single(sut.SavedUsers);
        Assert.True(sut.ShowUsersList);
        mockLocalStorage.Verify(x => x.SetObjectAsync(LoginViewModel.SavedUsersStorageKey, It.Is<SavedUsersData>(d => d.Users.Count == 1)), Times.Once);
    }

    [Fact]
    public async Task GivenLastUser_WhenRemoveUser_ThenUsersListIsHidden()
    {
        // Arrange
        SetupSavedUsers(new SavedUser { Email = "only@test.com" });
        var sut = CreateSut();
        await sut.OnInitializedAsync();

        // Act
        await sut.RemoveUserCommand.ExecuteAsync(sut.SavedUsers[0]);

        // Assert
        Assert.Empty(sut.SavedUsers);
        Assert.False(sut.ShowUsersList);
        Assert.True(sut.ShowRememberMe);
    }

    [Fact]
    public void GivenUsersList_WhenShowLoginForm_ThenUsersListIsHidden()
    {
        // Arrange
        var sut = CreateSut();
        sut.ShowUsersList = true;
        sut.Username = "bob";

        // Act
        sut.ShowLoginFormCommand.Execute(null);

        // Assert
        Assert.False(sut.ShowUsersList);
        Assert.True(sut.ShowRememberMe);
        Assert.Empty(sut.Username);
        Assert.Empty(sut.Password);
    }

    [Fact]
    public void GivenLoginForm_WhenShowUsersList_ThenUsersListIsShown()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        sut.ShowUsersListCommandCommand.Execute(null);

        // Assert
        Assert.True(sut.ShowUsersList);
        Assert.False(sut.ShowRememberMe);
    }

    [Fact]
    public async Task GivenRememberMe_WhenLoginSucceeds_ThenSavesUserAndShowsWebAuthnPrompt()
    {
        // Arrange
        mockAuth.Setup(x => x.LoginAsync("bob", "secret")).ReturnsAsync(new LoginResult { Success = true });
        var sut = CreateSut();
        sut.Username = "bob";
        sut.Password = "secret";
        sut.RememberMe = true;

        // Act
        await sut.LoginCommand.ExecuteAsync(null);

        // Assert
        mockLocalStorage.Verify(x => x.SetObjectAsync(LoginViewModel.SavedUsersStorageKey, It.Is<SavedUsersData>(d => d.Users.Single().Email == "bob")), Times.Once);
        Assert.True(sut.ShowWebAuthnPrompt);
        Assert.Equal("secret", sut.Password);
        mockNavigation.Verify(x => x.NavigateTo(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GivenRememberMeAndWebAuthnDisabled_WhenLoginSucceeds_ThenSavesUserAndNavigatesHome()
    {
        // Arrange
        appSettings.EnablePortalLoginAuthn = false;
        mockAuth.Setup(x => x.LoginAsync("bob", "secret")).ReturnsAsync(new LoginResult { Success = true });
        var sut = CreateSut();
        sut.Username = "bob";
        sut.Password = "secret";
        sut.RememberMe = true;

        // Act
        await sut.LoginCommand.ExecuteAsync(null);

        // Assert
        mockLocalStorage.Verify(x => x.SetObjectAsync(LoginViewModel.SavedUsersStorageKey, It.IsAny<SavedUsersData>()), Times.Once);
        Assert.False(sut.ShowWebAuthnPrompt);
        Assert.Equal(string.Empty, sut.Password);
        mockNavigation.Verify(x => x.NavigateTo("/"), Times.Once);
    }

    [Fact]
    public async Task GivenRememberMeAndNewPasswordRequired_WhenCompleteNewPassword_ThenShowsWebAuthnPromptWithNewPassword()
    {
        // Arrange
        mockAuth.Setup(x => x.LoginAsync("bob", "temp")).ReturnsAsync(new LoginResult { NewPasswordRequired = true });
        mockAuth.Setup(x => x.CompleteNewPasswordAsync("NewPassword123")).ReturnsAsync(new LoginResult { Success = true });
        var sut = CreateSut();
        sut.Username = "bob";
        sut.Password = "temp";
        sut.RememberMe = true;
        await sut.LoginAsync();
        sut.NewPassword = "NewPassword123";
        sut.ConfirmNewPassword = "NewPassword123";

        // Act
        await sut.CompleteNewPasswordAsync();

        // Assert
        Assert.True(sut.ShowWebAuthnPrompt);
        Assert.Equal("NewPassword123", sut.Password);
        mockLocalStorage.Verify(x => x.SetObjectAsync(LoginViewModel.SavedUsersStorageKey, It.IsAny<SavedUsersData>()), Times.Once);
    }

    [Fact]
    public async Task GivenSuccessfulRegisterWithPrf_WhenHandleWebAuthnSetup_ThenStoresEncryptedPasswordAndNavigatesHome()
    {
        // Arrange
        var sut = CreateSut();
        sut.Username = "user";
        sut.Password = "pass";
        sut.ShowWebAuthnPrompt = true;
        mockWebAuthn
            .Setup(x => x.RegisterAsync("user"))
            .ReturnsAsync(new WebAuthnRegisterResult { Success = true, PrfEnabled = true, KeysDerived = true, CredentialID = "cred1", UserID = "uid1" });
        mockPasswordCrypto.Setup(x => x.EncryptAsync("pass", "user")).ReturnsAsync("encrypted");

        // Act
        await sut.HandleWebAuthnSetupCommand.ExecuteAsync(null);

        // Assert
        mockLocalStorage.Verify(
            x => x.SetObjectAsync(
                LoginViewModel.SavedUsersStorageKey,
                It.Is<SavedUsersData>(d => d.Users.Single().WebAuthnCredential!.CredentialID == "cred1" && d.Users.Single().WebAuthnCredential!.EncryptedPassword == "encrypted")),
            Times.Once);
        mockNavigation.Verify(x => x.NavigateTo("/"), Times.Once);
        Assert.False(sut.IsWebAuthnProcessing);
        Assert.False(sut.ShowWebAuthnPrompt);
        Assert.Equal(string.Empty, sut.Password);
    }

    [Fact]
    public async Task GivenSuccessfulRegisterWithoutPrf_WhenHandleWebAuthnSetup_ThenErrorMessageIsSet()
    {
        // Arrange
        var sut = CreateSut();
        sut.Username = "user";
        mockWebAuthn.Setup(x => x.RegisterAsync("user")).ReturnsAsync(new WebAuthnRegisterResult { Success = true, PrfEnabled = false });

        // Act
        await sut.HandleWebAuthnSetupCommand.ExecuteAsync(null);

        // Assert
        Assert.NotNull(sut.WebAuthnErrorMessage);
        Assert.False(sut.IsWebAuthnProcessing);
        mockPasswordCrypto.Verify(x => x.EncryptAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        mockNavigation.Verify(x => x.NavigateTo(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GivenPrfEnabledButNoKeysDerived_WhenHandleWebAuthnSetup_ThenErrorMessageIsSet()
    {
        // Arrange
        var sut = CreateSut();
        sut.Username = "user";
        mockWebAuthn.Setup(x => x.RegisterAsync("user")).ReturnsAsync(new WebAuthnRegisterResult { Success = true, PrfEnabled = true, KeysDerived = false });

        // Act
        await sut.HandleWebAuthnSetupCommand.ExecuteAsync(null);

        // Assert
        Assert.NotNull(sut.WebAuthnErrorMessage);
        mockPasswordCrypto.Verify(x => x.EncryptAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        mockNavigation.Verify(x => x.NavigateTo(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GivenRegisterThrows_WhenHandleWebAuthnSetup_ThenErrorMessageIsSet()
    {
        // Arrange
        var sut = CreateSut();
        sut.Username = "user";
        mockWebAuthn.Setup(x => x.RegisterAsync(It.IsAny<string>())).ThrowsAsync(new InvalidOperationException("webauthn error"));

        // Act
        await sut.HandleWebAuthnSetupCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("Biometric login could not be set up. Please continue with regular login.", sut.WebAuthnErrorMessage);
        Assert.False(sut.IsWebAuthnProcessing);
    }

    [Fact]
    public void GivenWebAuthnPrompt_WhenSkipWebAuthnSetup_ThenNavigatesHomeAndClearsPassword()
    {
        // Arrange
        var sut = CreateSut();
        sut.ShowWebAuthnPrompt = true;
        sut.Password = "secret";

        // Act
        sut.SkipWebAuthnSetupCommand.Execute(null);

        // Assert
        Assert.False(sut.ShowWebAuthnPrompt);
        Assert.Equal(string.Empty, sut.Password);
        mockNavigation.Verify(x => x.NavigateTo("/"), Times.Once);
    }

    [Fact]
    public void GivenWebAuthnError_WhenDismissWebAuthnError_ThenErrorIsClearedAndNavigatesHome()
    {
        // Arrange
        var sut = CreateSut();
        sut.WebAuthnErrorMessage = "Some error";
        sut.ShowWebAuthnPrompt = true;

        // Act
        sut.DismissWebAuthnErrorCommand.Execute(null);

        // Assert
        Assert.Null(sut.WebAuthnErrorMessage);
        Assert.False(sut.ShowWebAuthnPrompt);
        mockNavigation.Verify(x => x.NavigateTo("/"), Times.Once);
    }

    private static SavedUser CreateWebAuthnUser(string email, string credentialId) => new()
    {
        Email = email,
        WebAuthnCredential = new WebAuthnStoredCredential { CredentialID = credentialId, EncryptedPassword = "enc" },
    };

    private void SetupSavedUsers(params SavedUser[] users)
    {
        mockLocalStorage
            .Setup(x => x.GetObjectAsync<SavedUsersData>(LoginViewModel.SavedUsersStorageKey))
            .ReturnsAsync(new SavedUsersData { Users = [.. users] });
    }

    [Fact]
    public void GivenNullExtensionPoints_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("signInOptions", Assert.Throws<ArgumentNullException>(() => new LoginViewModel(mockAuth.Object, mockNavigation.Object, mockTheme.Object, mockLocalStorage.Object, mockWebAuthn.Object, mockPasswordCrypto.Object, mockKeyStore.Object, offlineMode, null!, optionRegistrations, mockLifecycle.Object, appSettings, testText.Ui, testText.Errors)).ParamName);
        Assert.Equal("signInOptionRegistrations", Assert.Throws<ArgumentNullException>(() => new LoginViewModel(mockAuth.Object, mockNavigation.Object, mockTheme.Object, mockLocalStorage.Object, mockWebAuthn.Object, mockPasswordCrypto.Object, mockKeyStore.Object, offlineMode, options, null!, mockLifecycle.Object, appSettings, testText.Ui, testText.Errors)).ParamName);
        Assert.Equal("sessionLifecycle", Assert.Throws<ArgumentNullException>(() => new LoginViewModel(mockAuth.Object, mockNavigation.Object, mockTheme.Object, mockLocalStorage.Object, mockWebAuthn.Object, mockPasswordCrypto.Object, mockKeyStore.Object, offlineMode, options, optionRegistrations, null!, appSettings, testText.Ui, testText.Errors)).ParamName);
    }

    [Fact]
    public void GivenNoOptions_WhenConstructed_ThenNoAdvancedOptions()
    {
        // Act
        var sut = CreateSut();

        // Assert
        Assert.False(sut.HasSignInOptions);
        Assert.Empty(sut.SignInOptionComponents);
    }

    [Fact]
    public void GivenOptions_WhenConstructed_ThenTheirComponentsInOptionOrder()
    {
        // Arrange
        AddOption(new SecondOption(), typeof(int));
        AddOption(new FakeOption(), typeof(string));

        // Act
        var sut = CreateSut();

        // Assert
        Assert.True(sut.HasSignInOptions);
        Assert.Equal([typeof(string), typeof(int)], sut.SignInOptionComponents);
    }

    [Fact]
    public async Task GivenAnOptionIsSet_WhenInitialized_ThenOptionsLoadedAndAdvancedOptionsExpanded()
    {
        // Arrange
        var option = new FakeOption { SetOnLoad = true };
        AddOption(option, typeof(string));
        var sut = CreateSut();

        // Act
        await sut.OnInitializedAsync();

        // Assert
        Assert.Equal(1, option.Loads);
        Assert.True(sut.IsAdvancedOptionsExpanded);
    }

    [Fact]
    public async Task GivenNoOptionSet_WhenInitialized_ThenAdvancedOptionsCollapsedUntilToggled()
    {
        // Arrange
        AddOption(new FakeOption(), typeof(string));
        var sut = CreateSut();

        // Act
        await sut.OnInitializedAsync();

        // Assert
        Assert.False(sut.IsAdvancedOptionsExpanded);
        sut.ToggleAdvancedOptionsCommand.Execute(null);
        Assert.True(sut.IsAdvancedOptionsExpanded);
    }

    [Fact]
    public async Task GivenOptionFailsToLoad_WhenInitialized_ThenPageStillLoads()
    {
        // Arrange
        AddOption(new FakeOption { FailOnLoad = true }, typeof(string));
        var sut = CreateSut();

        // Act
        var exception = await Record.ExceptionAsync(sut.OnInitializedAsync);

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task GivenOptions_WhenSignedIn_ThenOptionsAppliedThenModulesToldThenNavigates()
    {
        // Arrange
        var order = new List<string>();
        AddOption(new FakeOption { OnApply = () => order.Add("option") }, typeof(string));
        mockLifecycle.Setup(x => x.SignedInAsync()).Callback(() => order.Add("signed in")).Returns(Task.CompletedTask);
        mockNavigation.Setup(x => x.NavigateTo("/")).Callback(() => order.Add("navigate"));
        mockAuth.Setup(x => x.LoginAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(new LoginResult { Success = true });
        var sut = CreateSut();
        sut.Username = "bob";
        sut.Password = "pw";

        // Act
        await sut.LoginAsync();

        // Assert
        Assert.Equal(["option", "signed in", "navigate"], order);
    }

    [Fact]
    public async Task GivenSignedInOffline_WhenSelectUser_ThenOptionsAppliedAndModulesTold()
    {
        // Arrange
        var option = new FakeOption();
        AddOption(option, typeof(string));
        var user = CreateWebAuthnUser("user@test.com", "cred1");
        mockWebAuthn.Setup(x => x.AuthenticateAsync("cred1")).ReturnsAsync(new WebAuthnAuthenticateResult { Success = true, KeysDerived = true });
        mockPasswordCrypto.Setup(x => x.DecryptAsync("enc", It.IsAny<string>())).ReturnsAsync("secret");
        var sut = CreateSut();
        sut.SignInOffline = true;

        // Act
        await sut.SelectUserCommand.ExecuteAsync(user);

        // Assert
        Assert.Equal(1, option.Applies);
        mockLifecycle.Verify(x => x.SignedInAsync(), Times.Once);
    }

    [Fact]
    public async Task GivenOptionFailsToApply_WhenSignedIn_ThenSignInStillCompletes()
    {
        // Arrange
        AddOption(new FakeOption { FailOnApply = true }, typeof(string));
        mockAuth.Setup(x => x.LoginAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(new LoginResult { Success = true });
        var sut = CreateSut();

        // Act
        await sut.LoginAsync();

        // Assert
        mockLifecycle.Verify(x => x.SignedInAsync(), Times.Once);
        mockNavigation.Verify(x => x.NavigateTo("/"), Times.Once);
        Assert.Null(sut.ErrorMessage);
    }

    [Fact]
    public async Task GivenSignInFails_WhenLogin_ThenOptionsNotAppliedAndModulesNotTold()
    {
        // Arrange
        var option = new FakeOption();
        AddOption(option, typeof(string));
        mockAuth.Setup(x => x.LoginAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(new LoginResult { ErrorCode = AuthErrorCodes.NotAuthorized });
        var sut = CreateSut();

        // Act
        await sut.LoginAsync();

        // Assert
        Assert.Equal(0, option.Applies);
        mockLifecycle.Verify(x => x.SignedInAsync(), Times.Never);
    }

    private void AddOption(ISignInOption option, Type component)
    {
        options.Add(option);
        optionRegistrations.Add(new SignInOptionRegistration(option.GetType(), component));
    }

    private async Task<LoginViewModel> CreateSutAwaitingNewPasswordAsync()
    {
        mockAuth.Setup(x => x.LoginAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(new LoginResult { NewPasswordRequired = true });
        var sut = CreateSut();
        await sut.LoginAsync();
        Assert.True(sut.IsNewPasswordRequired);
        return sut;
    }

    private LoginViewModel CreateSut() => new(mockAuth.Object, mockNavigation.Object, mockTheme.Object, mockLocalStorage.Object, mockWebAuthn.Object, mockPasswordCrypto.Object, mockKeyStore.Object, offlineMode,
            options,
            optionRegistrations,
            mockLifecycle.Object,
            appSettings,
        testText.Ui, testText.Errors);

    private class FakeOption : ISignInOption
    {
        public bool SetOnLoad { get; init; }

        public bool FailOnLoad { get; init; }

        public bool FailOnApply { get; init; }

        public Action? OnApply { get; init; }

        public int Loads { get; private set; }

        public int Applies { get; private set; }

        public bool IsSet { get; private set; }

        public Task LoadAsync()
        {
            Loads++;
            if (FailOnLoad)
            {
                throw new InvalidOperationException("load");
            }

            IsSet = SetOnLoad;
            return Task.CompletedTask;
        }

        public Task ApplyAsync()
        {
            Applies++;
            OnApply?.Invoke();
            return FailOnApply ? Task.FromException(new InvalidOperationException("apply")) : Task.CompletedTask;
        }
    }

    private sealed class SecondOption : FakeOption, ISignInOption
    {
        public int Order => 10;
    }
}
