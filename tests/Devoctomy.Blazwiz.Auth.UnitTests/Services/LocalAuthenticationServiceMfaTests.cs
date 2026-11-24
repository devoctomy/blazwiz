using Devoctomy.Blazwiz.Auth.Exceptions;
using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Auth.Services;
using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.Auth.Sessions.Interfaces;
using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Core.Offline;
using Moq;

namespace Devoctomy.Blazwiz.Auth.UnitTests.Services;

// The placeholder sign in with two-step verification on (LocalMfaService), and remembered devices.
public class LocalAuthenticationServiceMfaTests
{
    private readonly Mock<ILocalStorageService> mockLocalStorage = new();
    private readonly Mock<IUserDisplayService> mockUserDisplay = new();
    private readonly Mock<ISignOutEverywhereNotifier> mockNotifier = new();
    private readonly Mock<TimeProvider> mockTimeProvider = new();
    private readonly OfflineModeService offlineMode = new();
    private readonly InMemoryOfflineSessionStore offlineSessions;

    public LocalAuthenticationServiceMfaTests()
    {
        mockTimeProvider.Setup(x => x.GetUtcNow()).Returns(new DateTimeOffset(2026, 11, 23, 9, 0, 0, TimeSpan.Zero));
        mockUserDisplay.Setup(x => x.GetDisplayNameFromUsername(It.IsAny<string>())).Returns("Bob Hoskins");
        mockLocalStorage.Setup(x => x.GetObjectAsync<List<string>>(LocalMfaService.StorageKey)).ReturnsAsync(["bob@example.com"]);
        offlineSessions = new InMemoryOfflineSessionStore(mockTimeProvider.Object);
    }

    [Fact]
    public async Task GivenMfaOn_WhenLoginAsync_ThenAsksForTheCode_WithoutStartingASession()
    {
        // Act
        var result = await CreateSut().LoginAsync(" Bob@Example.com ", "anything");

        // Assert
        Assert.False(result.Success);
        Assert.True(result.MfaCodeRequired);
        Assert.Null(result.MfaSetupSecret);
        mockLocalStorage.Verify(x => x.SetObjectAsync(LocalAuthenticationService.SessionStorageKey, It.IsAny<UserSession>()), Times.Never);
    }

    [Fact]
    public async Task GivenMfaOff_WhenLoginAsyncWithADevice_ThenSignedIn()
    {
        // Act
        var result = await CreateSut().LoginAsync("alice@example.com", "anything", "device");

        // Assert
        Assert.True(result.Success);
        Assert.False(result.RememberedDeviceRejected);
    }

    [Fact]
    public async Task GivenCodeAskedFor_WhenCompleteMfaAsyncWithSixDigits_ThenSignedIn()
    {
        // Arrange
        var sut = CreateSut();
        await sut.LoginAsync("bob@example.com", "anything");

        // Act
        var result = await sut.CompleteMfaAsync("123456");

        // Assert
        Assert.True(result.Success);
        Assert.Equal("bob@example.com", result.Session?.Username);
        Assert.Equal(AuthErrorCodes.SignInAgain, (await sut.CompleteMfaAsync("123456")).ErrorCode);
    }

    [Theory]
    [InlineData("", AuthErrorCodes.MfaCodeRequired)]
    [InlineData("12345", AuthErrorCodes.CodeMismatch)]
    public async Task GivenCodeAskedFor_WhenCompleteMfaAsyncWithABadCode_ThenRefused_AndCanBeTriedAgain(string code, string expected)
    {
        // Arrange
        var sut = CreateSut();
        await sut.LoginAsync("bob@example.com", "anything");

        // Act
        var bad = await sut.CompleteMfaAsync(code);
        var good = await sut.CompleteMfaAsync("654321");

        // Assert
        Assert.Equal(expected, bad.ErrorCode);
        Assert.True(good.Success);
    }

    [Fact]
    public async Task GivenNoSignInWaiting_WhenCompleteMfaAsync_ThenSignInAgain()
    {
        // Act
        var result = await CreateSut().CompleteMfaAsync("123456");

        // Assert
        Assert.Equal(AuthErrorCodes.SignInAgain, result.ErrorCode);
    }

    [Fact]
    public async Task GivenCodeAskedFor_WhenLogoutAsync_ThenNothingLeftWaiting()
    {
        // Arrange
        var sut = CreateSut();
        await sut.LoginAsync("bob@example.com", "anything");

        // Act
        await sut.LogoutAsync();

        // Assert
        Assert.Equal(AuthErrorCodes.SignInAgain, (await sut.CompleteMfaAsync("123456")).ErrorCode);
    }

    [Fact]
    public async Task GivenPlaceholder_WhenCompleteMfaSetupAsync_ThenSignInAgain()
    {
        // Act
        var result = await CreateSut().CompleteMfaSetupAsync("123456");

        // Assert
        Assert.Equal(AuthErrorCodes.SignInAgain, result.ErrorCode);
    }

    [Fact]
    public async Task GivenPlaceholder_WhenRememberDeviceAsync_ThenNothingToRemember()
    {
        // Act
        var result = await CreateSut().RememberDeviceAsync("Work laptop");

        // Assert
        Assert.Null(result);
    }

    private LocalAuthenticationService CreateSut() => new(mockLocalStorage.Object, mockUserDisplay.Object, offlineMode, offlineSessions, mockNotifier.Object, mockTimeProvider.Object);
}
