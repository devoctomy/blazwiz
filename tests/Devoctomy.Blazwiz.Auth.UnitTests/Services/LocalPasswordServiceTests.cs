using Devoctomy.Blazwiz.Auth.Exceptions;
using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Auth.Services;
using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.Core.Exceptions;
using Moq;

namespace Devoctomy.Blazwiz.Auth.UnitTests.Services;

public class LocalPasswordServiceTests
{
    private readonly Mock<IAuthenticationService> mockAuth = new();

    [Fact]
    public void GivenNullAuthenticationService_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("authenticationService", Assert.Throws<ArgumentNullException>(() => new LocalPasswordService(null!)).ParamName);
    }

    [Fact]
    public async Task GivenSignedIn_WhenChangePasswordAsync_ThenAccepted()
    {
        // Arrange
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(new UserSession { Username = "bob@example.com" });
        var sut = new LocalPasswordService(mockAuth.Object);

        // Act & Assert
        await sut.ChangePasswordAsync("old", "new");
    }

    [Fact]
    public async Task GivenNobodySignedIn_WhenChangePasswordAsync_ThenNotSignedIn()
    {
        // Arrange
        var sut = new LocalPasswordService(mockAuth.Object);

        // Act
        var exception = await Assert.ThrowsAsync<BlazwizException>(() => sut.ChangePasswordAsync("old", "new"));

        // Assert
        Assert.Equal(AuthErrorCodes.NotSignedIn, exception.Code);
    }

    [Theory]
    [InlineData("", "new")]
    [InlineData("old", "")]
    public async Task GivenEmptyPassword_WhenChangePasswordAsync_ThenPasswordsRequired(string current, string next)
    {
        // Arrange
        var sut = new LocalPasswordService(mockAuth.Object);

        // Act
        var exception = await Assert.ThrowsAsync<BlazwizException>(() => sut.ChangePasswordAsync(current, next));

        // Assert
        Assert.Equal(AuthErrorCodes.PasswordsRequired, exception.Code);
    }

    [Fact]
    public async Task GivenAnyEmailAndCode_WhenResettingThePassword_ThenAccepted()
    {
        // Arrange
        var sut = new LocalPasswordService(mockAuth.Object);

        // Act & Assert
        await sut.RequestPasswordResetAsync("bob@example.com");
        await sut.ResetPasswordAsync("bob@example.com", "anything", "new");
    }

    [Theory]
    [InlineData(" ", "123", "new", AuthErrorCodes.EmailRequired)]
    [InlineData("bob@example.com", " ", "new", AuthErrorCodes.CodeRequired)]
    [InlineData("bob@example.com", "123", "", AuthErrorCodes.PasswordsRequired)]
    public async Task GivenMissingField_WhenResetPasswordAsync_ThenSaysWhich(string email, string code, string password, string expected)
    {
        // Arrange
        var sut = new LocalPasswordService(mockAuth.Object);

        // Act
        var exception = await Assert.ThrowsAsync<BlazwizException>(() => sut.ResetPasswordAsync(email, code, password));

        // Assert
        Assert.Equal(expected, exception.Code);
    }

    [Fact]
    public async Task GivenNoEmail_WhenRequestPasswordResetAsync_ThenEmailRequired()
    {
        // Arrange
        var sut = new LocalPasswordService(mockAuth.Object);

        // Act
        var exception = await Assert.ThrowsAsync<BlazwizException>(() => sut.RequestPasswordResetAsync(" "));

        // Assert
        Assert.Equal(AuthErrorCodes.EmailRequired, exception.Code);
    }
}
