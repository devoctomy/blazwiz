using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Application.ViewModels;
using blazwiz.Portal.Models.Auth;
using blazwiz.Portal.Models.Settings;
using Moq;

namespace blazwiz.Portal.Application.UnitTests.ViewModels;

public class LoginViewModelTests
{
    private readonly Mock<IAuthenticationService> mockAuth = new();
    private readonly Mock<INavigationService> mockNavigation = new();
    private readonly Mock<IThemeService> mockTheme = new();
    private readonly AppSettings appSettings = new() { ApplicationTitle = "Test App" };

    [Fact]
    public void GivenNullAuthService_WhenConstructing_ThenThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new LoginViewModel(null!, mockNavigation.Object, mockTheme.Object, appSettings));
        Assert.Equal("authService", exception.ParamName);
    }

    [Fact]
    public void GivenNullNavigationService_WhenConstructing_ThenThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new LoginViewModel(mockAuth.Object, null!, mockTheme.Object, appSettings));
        Assert.Equal("navigationService", exception.ParamName);
    }

    [Fact]
    public void GivenNullThemeService_WhenConstructing_ThenThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new LoginViewModel(mockAuth.Object, mockNavigation.Object, null!, appSettings));
        Assert.Equal("themeService", exception.ParamName);
    }

    [Fact]
    public void GivenNullAppSettings_WhenConstructing_ThenThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new LoginViewModel(mockAuth.Object, mockNavigation.Object, mockTheme.Object, null!));
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
    public async Task GivenExistingSession_WhenInitialized_ThenNavigatesHome()
    {
        // Arrange
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(new UserSession());
        var sut = CreateSut();

        // Act
        await sut.OnInitializedAsync();

        // Assert
        mockNavigation.Verify(x => x.NavigateTo("/"), Times.Once);
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
        mockAuth.Setup(x => x.LoginAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(new LoginResult { Success = false, Error = "Nope" });
        var sut = CreateSut();

        // Act
        await sut.LoginAsync();

        // Assert
        Assert.Equal("Nope", sut.ErrorMessage);
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
        Assert.Equal("Login failed.", sut.ErrorMessage);
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
        Assert.Equal("Login failed: Boom", sut.ErrorMessage);
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
            .ReturnsAsync(new LoginResult { Success = false, Error = "Nope" })
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
    [InlineData("Password not long enough", "Password not long enough")]
    [InlineData(null, "Could not set the new password.")]
    public async Task GivenRejectedPassword_WhenCompleteNewPassword_ThenShowsErrorAndStaysOnForm(string? error, string expected)
    {
        // Arrange
        var sut = await CreateSutAwaitingNewPasswordAsync();
        mockAuth.Setup(x => x.CompleteNewPasswordAsync(It.IsAny<string>())).ReturnsAsync(new LoginResult { Error = error });

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
        Assert.Equal("Could not set the new password: Boom", sut.ErrorMessage);
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

    private async Task<LoginViewModel> CreateSutAwaitingNewPasswordAsync()
    {
        mockAuth.Setup(x => x.LoginAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(new LoginResult { NewPasswordRequired = true });
        var sut = CreateSut();
        await sut.LoginAsync();
        Assert.True(sut.IsNewPasswordRequired);
        return sut;
    }

    private LoginViewModel CreateSut() => new(mockAuth.Object, mockNavigation.Object, mockTheme.Object, appSettings);
}
