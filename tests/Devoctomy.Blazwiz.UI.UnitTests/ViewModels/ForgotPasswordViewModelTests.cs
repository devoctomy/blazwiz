using Devoctomy.Blazwiz.Auth.Exceptions;
using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Core.Exceptions;
using Devoctomy.Blazwiz.Testing;
using Devoctomy.Blazwiz.UI.Models;
using Devoctomy.Blazwiz.UI.ViewModels;
using Moq;

namespace Devoctomy.Blazwiz.UI.UnitTests.ViewModels;

public class ForgotPasswordViewModelTests
{
    private readonly Mock<IPasswordService> mockPasswords = new();
    private readonly Mock<INavigationService> mockNavigation = new();
    private readonly FrameworkText text = new();
    private readonly AppSettings appSettings = new() { ApplicationTitle = "Acme" };

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("passwordService", Assert.Throws<ArgumentNullException>(() => new ForgotPasswordViewModel(null!, mockNavigation.Object, text.Errors, appSettings)).ParamName);
        Assert.Equal("navigationService", Assert.Throws<ArgumentNullException>(() => new ForgotPasswordViewModel(mockPasswords.Object, null!, text.Errors, appSettings)).ParamName);
        Assert.Equal("errorText", Assert.Throws<ArgumentNullException>(() => new ForgotPasswordViewModel(mockPasswords.Object, mockNavigation.Object, null!, appSettings)).ParamName);
        Assert.Equal("appSettings", Assert.Throws<ArgumentNullException>(() => new ForgotPasswordViewModel(mockPasswords.Object, mockNavigation.Object, text.Errors, null!)).ParamName);
    }

    [Fact]
    public async Task GivenEmail_WhenRequestCodeAsync_ThenCodeSentAndAskedForIt()
    {
        // Arrange
        var sut = CreateSut();
        sut.Email = "bob@example.com";

        // Act
        await sut.RequestCodeAsync();

        // Assert
        mockPasswords.Verify(x => x.RequestPasswordResetAsync("bob@example.com"), Times.Once);
        Assert.Equal(ForgotPasswordStep.Reset, sut.Step);
        Assert.Null(sut.ErrorMessage);
        Assert.False(sut.IsBusy);
    }

    [Fact]
    public async Task GivenServiceRefuses_WhenRequestCodeAsync_ThenSaysWhyAndStays()
    {
        // Arrange
        var refused = new BlazwizException(AuthErrorCodes.EmailRequired);
        mockPasswords.Setup(x => x.RequestPasswordResetAsync(It.IsAny<string>())).ThrowsAsync(refused);
        var sut = CreateSut();

        // Act
        await sut.RequestCodeAsync();

        // Assert
        Assert.Equal(text.Errors.Describe(refused), sut.ErrorMessage);
        Assert.Equal(ForgotPasswordStep.Request, sut.Step);
    }

    [Fact]
    public async Task GivenCodeAndMatchingPasswords_WhenResetPasswordAsync_ThenResetAndDone()
    {
        // Arrange
        var sut = CreateSut();
        sut.Email = "bob@example.com";
        await sut.RequestCodeAsync();
        sut.Code = "123456";
        sut.NewPassword = "new-pass";
        sut.ConfirmNewPassword = "new-pass";

        // Act
        await sut.ResetPasswordAsync();

        // Assert
        mockPasswords.Verify(x => x.ResetPasswordAsync("bob@example.com", "123456", "new-pass"), Times.Once);
        Assert.Equal(ForgotPasswordStep.Done, sut.Step);
        Assert.Equal(string.Empty, sut.NewPassword);
    }

    [Fact]
    public async Task GivenDifferentConfirmation_WhenResetPasswordAsync_ThenTheyDoNotMatchWithoutACall()
    {
        // Arrange
        var sut = CreateSut();
        sut.NewPassword = "new-pass";
        sut.ConfirmNewPassword = "other";

        // Act
        await sut.ResetPasswordAsync();

        // Assert
        Assert.Equal(text.Errors.Describe(AuthErrorCodes.PasswordsDoNotMatch, []), sut.ErrorMessage);
        mockPasswords.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GivenWrongCode_WhenResetPasswordAsync_ThenSaysWhyAndStays()
    {
        // Arrange
        var refused = new BlazwizException(AuthErrorCodes.CodeMismatch);
        mockPasswords.Setup(x => x.ResetPasswordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ThrowsAsync(refused);
        var sut = CreateSut();
        await sut.RequestCodeAsync();

        // Act
        await sut.ResetPasswordAsync();

        // Assert
        Assert.Equal(text.Errors.Describe(refused), sut.ErrorMessage);
        Assert.Equal(ForgotPasswordStep.Reset, sut.Step);
    }

    [Fact]
    public async Task GivenCodeAsked_WhenStartAgain_ThenBackToTheEmailWithTheCodeCleared()
    {
        // Arrange
        var sut = CreateSut();
        await sut.RequestCodeAsync();
        sut.Code = "123";

        // Act
        sut.StartAgain();

        // Assert
        Assert.Equal(ForgotPasswordStep.Request, sut.Step);
        Assert.Equal(string.Empty, sut.Code);
    }

    [Fact]
    public void GivenAnyStep_WhenBackToSignIn_ThenTheLoginPage()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        sut.BackToSignIn();

        // Assert
        mockNavigation.Verify(x => x.NavigateTo("/login"), Times.Once);
        Assert.Same(appSettings, sut.AppSettings);
    }

    private ForgotPasswordViewModel CreateSut() => new(mockPasswords.Object, mockNavigation.Object, text.Errors, appSettings);
}
