using Devoctomy.Blazwiz.Auth.Exceptions;
using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Core.Exceptions;
using Devoctomy.Blazwiz.Testing;
using Devoctomy.Blazwiz.UI.ViewModels;
using Moq;

namespace Devoctomy.Blazwiz.UI.UnitTests.ViewModels;

public class ChangePasswordViewModelTests
{
    private readonly Mock<IPasswordService> mockPasswords = new();
    private readonly Mock<IAuthenticationService> mockAuth = new();
    private readonly Mock<ILocalStorageService> mockStorage = new();
    private readonly Mock<IPasswordCryptoService> mockCrypto = new();
    private readonly FrameworkText text = new();

    public ChangePasswordViewModelTests()
    {
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(new UserSession { Username = "bob@example.com" });
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("passwordService", Assert.Throws<ArgumentNullException>(() => new ChangePasswordViewModel(null!, mockAuth.Object, mockStorage.Object, mockCrypto.Object, text.Ui, text.Errors)).ParamName);
        Assert.Equal("authService", Assert.Throws<ArgumentNullException>(() => new ChangePasswordViewModel(mockPasswords.Object, null!, mockStorage.Object, mockCrypto.Object, text.Ui, text.Errors)).ParamName);
        Assert.Equal("localStorageService", Assert.Throws<ArgumentNullException>(() => new ChangePasswordViewModel(mockPasswords.Object, mockAuth.Object, null!, mockCrypto.Object, text.Ui, text.Errors)).ParamName);
        Assert.Equal("passwordCryptoService", Assert.Throws<ArgumentNullException>(() => new ChangePasswordViewModel(mockPasswords.Object, mockAuth.Object, mockStorage.Object, null!, text.Ui, text.Errors)).ParamName);
        Assert.Equal("text", Assert.Throws<ArgumentNullException>(() => new ChangePasswordViewModel(mockPasswords.Object, mockAuth.Object, mockStorage.Object, mockCrypto.Object, null!, text.Errors)).ParamName);
        Assert.Equal("errorText", Assert.Throws<ArgumentNullException>(() => new ChangePasswordViewModel(mockPasswords.Object, mockAuth.Object, mockStorage.Object, mockCrypto.Object, text.Ui, null!)).ParamName);
    }

    [Fact]
    public async Task GivenEmptyField_WhenChangePasswordAsync_ThenAskedToFillEveryFieldWithoutACall()
    {
        // Arrange
        var sut = CreateSut("old", "new", string.Empty);

        // Act
        await sut.ChangePasswordAsync();

        // Assert
        Assert.Equal(text.Errors.Describe(AuthErrorCodes.PasswordsRequired, []), sut.ErrorMessage);
        mockPasswords.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GivenDifferentConfirmation_WhenChangePasswordAsync_ThenTheyDoNotMatchWithoutACall()
    {
        // Arrange
        var sut = CreateSut("old", "new", "other");

        // Act
        await sut.ChangePasswordAsync();

        // Assert
        Assert.Equal(text.Errors.Describe(AuthErrorCodes.PasswordsDoNotMatch, []), sut.ErrorMessage);
        mockPasswords.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GivenNothingRemembered_WhenChangePasswordAsync_ThenChangedAndFieldsCleared()
    {
        // Arrange
        var sut = CreateSut("old", "new", "new");

        // Act
        await sut.ChangePasswordAsync();

        // Assert
        mockPasswords.Verify(x => x.ChangePasswordAsync("old", "new"), Times.Once);
        Assert.Equal(text.Ui["Profile.ChangePassword.Done"].Value, sut.SuccessMessage);
        Assert.Null(sut.ErrorMessage);
        Assert.Equal((string.Empty, string.Empty, string.Empty), (sut.CurrentPassword, sut.NewPassword, sut.ConfirmNewPassword));
        Assert.False(sut.IsBusy);
        mockStorage.Verify(x => x.SetObjectAsync(It.IsAny<string>(), It.IsAny<SavedUsersData>()), Times.Never);
    }

    [Fact]
    public async Task GivenRememberedPassword_WhenChangePasswordAsync_ThenTheNewOneIsRemembered()
    {
        // Arrange
        var users = Remembered("old-encrypted");
        mockCrypto.Setup(x => x.EncryptAsync("new", It.IsAny<string>())).ReturnsAsync("new-encrypted");
        var sut = CreateSut("old", "new", "new");

        // Act
        await sut.ChangePasswordAsync();

        // Assert
        Assert.Equal("new-encrypted", users.Users[0].WebAuthnCredential!.EncryptedPassword);
        mockStorage.Verify(x => x.SetObjectAsync(LoginViewModel.SavedUsersStorageKey, users), Times.Once);
        Assert.Equal(text.Ui["Profile.ChangePassword.Done"].Value, sut.SuccessMessage);
    }

    [Fact]
    public async Task GivenRememberedPasswordButNoPasskeyKey_WhenChangePasswordAsync_ThenTheOldOneIsForgotten()
    {
        // Arrange
        var users = Remembered("old-encrypted");
        mockCrypto.Setup(x => x.EncryptAsync(It.IsAny<string>(), It.IsAny<string>())).ThrowsAsync(new InvalidOperationException("no key"));
        var sut = CreateSut("old", "new", "new");

        // Act
        await sut.ChangePasswordAsync();

        // Assert
        Assert.Null(users.Users[0].WebAuthnCredential!.EncryptedPassword);
        mockStorage.Verify(x => x.SetObjectAsync(LoginViewModel.SavedUsersStorageKey, users), Times.Once);
        Assert.Equal(text.Ui["Profile.ChangePassword.DonePasskeyForgotten"].Value, sut.SuccessMessage);
    }

    [Fact]
    public async Task GivenServiceRefuses_WhenChangePasswordAsync_ThenSaysWhyAndKeepsTheFields()
    {
        // Arrange
        var refused = new BlazwizException(AuthErrorCodes.CurrentPasswordWrong);
        mockPasswords.Setup(x => x.ChangePasswordAsync(It.IsAny<string>(), It.IsAny<string>())).ThrowsAsync(refused);
        var sut = CreateSut("old", "new", "new");

        // Act
        await sut.ChangePasswordAsync();

        // Assert
        Assert.Equal(text.Errors.Describe(refused), sut.ErrorMessage);
        Assert.Null(sut.SuccessMessage);
        Assert.Equal("old", sut.CurrentPassword);
        Assert.False(sut.IsBusy);
    }

    [Theory]
    [InlineData("", "new", "new")]
    [InlineData("old", "", "new")]
    [InlineData("old", "new", "")]
    public async Task GivenAnyFieldEmpty_WhenChangePasswordAsync_ThenAskedToFillEveryFieldWithoutACall(string current, string next, string confirm)
    {
        // Arrange
        var sut = CreateSut(current, next, confirm);

        // Act
        await sut.ChangePasswordAsync();

        // Assert
        Assert.Equal(text.Errors.Describe(AuthErrorCodes.PasswordsRequired, []), sut.ErrorMessage);
        Assert.Null(sut.SuccessMessage);
        mockPasswords.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GivenOffline_WhenChangePasswordAsync_ThenSaysItCannotBeChangedOffline()
    {
        // Arrange
        var offline = new BlazwizException(AuthErrorCodes.PasswordChangeOffline);
        mockPasswords.Setup(x => x.ChangePasswordAsync(It.IsAny<string>(), It.IsAny<string>())).ThrowsAsync(offline);
        var sut = CreateSut("old", "new", "new");

        // Act
        await sut.ChangePasswordAsync();

        // Assert
        Assert.Equal(text.Errors.Describe(AuthErrorCodes.PasswordChangeOffline, []), sut.ErrorMessage);
        Assert.Null(sut.SuccessMessage);
        Assert.False(sut.IsBusy);
        mockStorage.Verify(x => x.SetObjectAsync(It.IsAny<string>(), It.IsAny<SavedUsersData>()), Times.Never);
    }

    [Fact]
    public async Task GivenNetworkError_WhenChangePasswordAsync_ThenGeneralErrorNeverTheMessage()
    {
        // Arrange
        mockPasswords.Setup(x => x.ChangePasswordAsync(It.IsAny<string>(), It.IsAny<string>())).ThrowsAsync(new HttpRequestException("socket closed"));
        var sut = CreateSut("old", "new", "new");

        // Act
        await sut.ChangePasswordAsync();

        // Assert
        Assert.Equal("Something went wrong.", sut.ErrorMessage);
        Assert.Equal("new", sut.NewPassword);
        Assert.False(sut.IsBusy);
    }

    [Fact]
    public async Task GivenCancelled_WhenChangePasswordAsync_ThenCancellationIsNotTurnedIntoAnError()
    {
        // Arrange
        mockPasswords.Setup(x => x.ChangePasswordAsync(It.IsAny<string>(), It.IsAny<string>())).ThrowsAsync(new OperationCanceledException());
        var sut = CreateSut("old", "new", "new");

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(sut.ChangePasswordAsync);
        Assert.Null(sut.ErrorMessage);
        Assert.False(sut.IsBusy);
    }

    [Fact]
    public async Task GivenPreviousError_WhenChangePasswordSucceeds_ThenErrorCleared()
    {
        // Arrange
        mockPasswords.SetupSequence(x => x.ChangePasswordAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new BlazwizException(AuthErrorCodes.CurrentPasswordWrong))
            .Returns(Task.CompletedTask);
        var sut = CreateSut("old", "new", "new");
        await sut.ChangePasswordAsync();
        Assert.NotNull(sut.ErrorMessage);

        // Act
        await sut.ChangePasswordAsync();

        // Assert
        Assert.Null(sut.ErrorMessage);
        Assert.Equal(text.Ui["Profile.ChangePassword.Done"].Value, sut.SuccessMessage);
    }

    [Fact]
    public async Task GivenNobodySignedIn_WhenChangePasswordAsync_ThenRememberedPasswordUntouched()
    {
        // Arrange
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync((UserSession?)null);
        var users = Remembered("old-encrypted");
        var sut = CreateSut("old", "new", "new");

        // Act
        await sut.ChangePasswordAsync();

        // Assert
        Assert.Equal("old-encrypted", users.Users[0].WebAuthnCredential!.EncryptedPassword);
        Assert.Equal(text.Ui["Profile.ChangePassword.Done"].Value, sut.SuccessMessage);
        mockStorage.Verify(x => x.SetObjectAsync(It.IsAny<string>(), It.IsAny<SavedUsersData>()), Times.Never);
        mockCrypto.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GivenRememberedWithoutPassword_WhenChangePasswordAsync_ThenNothingToUpdate()
    {
        // Arrange
        Remembered(string.Empty);
        var sut = CreateSut("old", "new", "new");

        // Act
        await sut.ChangePasswordAsync();

        // Assert
        Assert.Equal(text.Ui["Profile.ChangePassword.Done"].Value, sut.SuccessMessage);
        mockStorage.Verify(x => x.SetObjectAsync(It.IsAny<string>(), It.IsAny<SavedUsersData>()), Times.Never);
        mockCrypto.VerifyNoOtherCalls();
    }

    private SavedUsersData Remembered(string encryptedPassword)
    {
        var users = new SavedUsersData
        {
            Users = [new SavedUser { Email = "Bob@Example.com", WebAuthnCredential = new WebAuthnStoredCredential { EncryptedPassword = encryptedPassword } }],
        };
        mockStorage.Setup(x => x.GetObjectAsync<SavedUsersData>(LoginViewModel.SavedUsersStorageKey)).ReturnsAsync(users);
        return users;
    }

    private ChangePasswordViewModel CreateSut(string current, string next, string confirm) =>
        new(mockPasswords.Object, mockAuth.Object, mockStorage.Object, mockCrypto.Object, text.Ui, text.Errors)
        {
            CurrentPassword = current,
            NewPassword = next,
            ConfirmNewPassword = confirm,
        };
}
