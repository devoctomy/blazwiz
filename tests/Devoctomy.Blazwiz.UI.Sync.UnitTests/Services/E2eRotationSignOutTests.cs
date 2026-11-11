using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.Auth.Sessions.Interfaces;
using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.UI.Sync.Services;
using Moq;

namespace Devoctomy.Blazwiz.UI.Sync.UnitTests.Services;

public class E2eRotationSignOutTests
{
    private readonly Mock<IAuthenticationService> mockAuth = new();
    private readonly Mock<ISessionLifecycle> mockLifecycle = new();
    private readonly Mock<INavigationService> mockNavigation = new();

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("authService", Assert.Throws<ArgumentNullException>(() => new E2eRotationSignOut(null!, mockLifecycle.Object, mockNavigation.Object)).ParamName);
        Assert.Equal("sessionLifecycle", Assert.Throws<ArgumentNullException>(() => new E2eRotationSignOut(mockAuth.Object, null!, mockNavigation.Object)).ParamName);
        Assert.Equal("navigationService", Assert.Throws<ArgumentNullException>(() => new E2eRotationSignOut(mockAuth.Object, mockLifecycle.Object, null!)).ParamName);
    }

    [Fact]
    public async Task GivenTheSignInServiceReached_WhenSignedOutEverywhere_ThenThisDeviceSignsOutAndTheLoginPageOpens()
    {
        // Arrange
        var calls = new List<string>();
        mockAuth.Setup(x => x.SignOutEverywhereAsync(SignOutReason.KeysRotated)).Callback(() => calls.Add("everywhere")).ReturnsAsync(true);
        mockLifecycle.Setup(x => x.SigningOutAsync()).Callback(() => calls.Add("signing out")).Returns(Task.CompletedTask);
        mockLifecycle.Setup(x => x.SignedOutAsync()).Callback(() => calls.Add("signed out")).Returns(Task.CompletedTask);
        mockNavigation.Setup(x => x.NavigateTo("/login")).Callback(() => calls.Add("login"));
        var sut = new E2eRotationSignOut(mockAuth.Object, mockLifecycle.Object, mockNavigation.Object);

        // Act
        var signedOut = await sut.SignOutEverywhereAsync();

        // Assert
        Assert.True(signedOut);
        Assert.Equal(["everywhere", "signing out", "signed out", "login"], calls);
    }

    [Fact]
    public async Task GivenTheSignInServiceUnreachable_WhenSignedOutEverywhere_ThenNothingChanges()
    {
        // Arrange
        mockAuth.Setup(x => x.SignOutEverywhereAsync(SignOutReason.KeysRotated)).ReturnsAsync(false);
        var sut = new E2eRotationSignOut(mockAuth.Object, mockLifecycle.Object, mockNavigation.Object);

        // Act
        var signedOut = await sut.SignOutEverywhereAsync();

        // Assert
        Assert.False(signedOut);
        mockLifecycle.Verify(x => x.SigningOutAsync(), Times.Never);
        mockNavigation.Verify(x => x.NavigateTo(It.IsAny<string>()), Times.Never);
    }
}
