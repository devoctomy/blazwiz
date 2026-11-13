using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.Auth.Sessions.Interfaces;
using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Testing;
using Devoctomy.Blazwiz.UI.ViewModels;
using Moq;

namespace Devoctomy.Blazwiz.UI.UnitTests.ViewModels;

public class ProfileViewModelTests
{
    private readonly FrameworkText testText = new();
    private readonly Mock<IAuthenticationService> mockAuth = new();
    private readonly Mock<IUserDisplayService> mockUserDisplay = new();
    private readonly Mock<ISignOutEverywhereNotifier> mockNotifier = new();
    private readonly Mock<ISessionLifecycle> mockLifecycle = new();
    private readonly Mock<INavigationService> mockNavigation = new();

    public ProfileViewModelTests()
    {
        mockUserDisplay.Setup(x => x.GetInitials("Bob Hoskins")).Returns("BH");
        mockUserDisplay.Setup(x => x.GetInitials(null)).Returns("?");
        mockNotifier.Setup(x => x.HasHandlers).Returns(true);
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("authService", Assert.Throws<ArgumentNullException>(() => new ProfileViewModel(null!, mockUserDisplay.Object, mockNotifier.Object, mockLifecycle.Object, mockNavigation.Object, testText.Ui)).ParamName);
        Assert.Equal("userDisplayService", Assert.Throws<ArgumentNullException>(() => new ProfileViewModel(mockAuth.Object, null!, mockNotifier.Object, mockLifecycle.Object, mockNavigation.Object, testText.Ui)).ParamName);
        Assert.Equal("signOutEverywhereNotifier", Assert.Throws<ArgumentNullException>(() => new ProfileViewModel(mockAuth.Object, mockUserDisplay.Object, null!, mockLifecycle.Object, mockNavigation.Object, testText.Ui)).ParamName);
        Assert.Equal("sessionLifecycle", Assert.Throws<ArgumentNullException>(() => new ProfileViewModel(mockAuth.Object, mockUserDisplay.Object, mockNotifier.Object, null!, mockNavigation.Object, testText.Ui)).ParamName);
        Assert.Equal("navigationService", Assert.Throws<ArgumentNullException>(() => new ProfileViewModel(mockAuth.Object, mockUserDisplay.Object, mockNotifier.Object, mockLifecycle.Object, null!, testText.Ui)).ParamName);
        Assert.Equal("text", Assert.Throws<ArgumentNullException>(() => new ProfileViewModel(mockAuth.Object, mockUserDisplay.Object, mockNotifier.Object, mockLifecycle.Object, mockNavigation.Object, null!)).ParamName);
    }

    [Fact]
    public async Task GivenSignedInUser_WhenInitialized_ThenExposesUserAndInitials()
    {
        // Arrange
        var session = new UserSession { DisplayName = "Bob Hoskins" };
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(session);
        var sut = CreateSut();
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // Act
        await sut.OnInitializedAsync();

        // Assert
        Assert.Same(session, sut.User);
        Assert.Equal("BH", sut.Initials);
        Assert.Contains(nameof(ProfileViewModel.User), changed);
        Assert.Contains(nameof(ProfileViewModel.Initials), changed);
    }

    [Fact]
    public async Task GivenNoUser_WhenInitialized_ThenUserIsNull()
    {
        // Arrange
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync((UserSession?)null);
        var sut = CreateSut();

        // Act
        await sut.OnInitializedAsync();

        // Assert
        Assert.Null(sut.User);
        Assert.Equal("?", sut.Initials);
        Assert.False(sut.CanSignOutEverywhere);
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    public async Task GivenHandlersAndSession_WhenInitialized_ThenSignOutEverywhereIsOfferedOnlyWithHandlersOnline(bool hasHandlers, bool offline, bool expected)
    {
        // Arrange
        mockNotifier.Setup(x => x.HasHandlers).Returns(hasHandlers);
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(new UserSession { Username = "bob", IsOffline = offline });
        var sut = CreateSut();

        // Act
        await sut.OnInitializedAsync();

        // Assert
        Assert.Equal(expected, sut.CanSignOutEverywhere);
    }

    [Fact]
    public void GivenConfirmationShown_WhenCancelled_ThenItCloses_AndNothingHappens()
    {
        // Arrange
        var sut = CreateSut();
        sut.AskSignOutEverywhere();

        // Act
        var shown = sut.IsConfirmingSignOutEverywhere;
        sut.CancelSignOutEverywhere();

        // Assert
        Assert.True(shown);
        Assert.False(sut.IsConfirmingSignOutEverywhere);
        mockAuth.Verify(x => x.SignOutEverywhereAsync(It.IsAny<SignOutReason>()), Times.Never);
    }

    [Fact]
    public async Task GivenConfirmed_WhenSignOutEverywhereSucceeds_ThenModulesStopAndTheLoginPageIsShown()
    {
        // Arrange
        var order = new List<string>();
        mockAuth.Setup(x => x.SignOutEverywhereAsync(SignOutReason.UserRequested)).Callback(() => order.Add("everywhere")).ReturnsAsync(true);
        mockLifecycle.Setup(x => x.SigningOutAsync()).Callback(() => order.Add("signing out")).Returns(Task.CompletedTask);
        mockLifecycle.Setup(x => x.SignedOutAsync()).Callback(() => order.Add("signed out")).Returns(Task.CompletedTask);
        mockNavigation.Setup(x => x.NavigateTo("/login")).Callback(() => order.Add("login"));
        var sut = CreateSut();
        sut.AskSignOutEverywhere();

        // Act
        await sut.SignOutEverywhereCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(["everywhere", "signing out", "signed out", "login"], order);
        Assert.False(sut.IsSigningOutEverywhere);
    }

    [Fact]
    public async Task GivenConfirmed_WhenTheSignInServiceCannotBeReached_ThenSaysNothingChanged_AndStaysSignedIn()
    {
        // Arrange
        mockAuth.Setup(x => x.SignOutEverywhereAsync(SignOutReason.UserRequested)).ReturnsAsync(false);
        var sut = CreateSut();
        sut.AskSignOutEverywhere();

        // Act
        await sut.SignOutEverywhereCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("Couldn't reach the sign-in service, so nothing was changed. Check your connection and try again.", sut.SignOutEverywhereError);
        Assert.False(sut.IsConfirmingSignOutEverywhere);
        mockLifecycle.Verify(x => x.SigningOutAsync(), Times.Never);
        mockNavigation.Verify(x => x.NavigateTo(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GivenSignOutEverywhereInProgress_WhenSignOutEverywhereAgain_ThenSecondIgnored()
    {
        // Arrange
        var pending = new TaskCompletionSource<bool>();
        mockAuth.Setup(x => x.SignOutEverywhereAsync(SignOutReason.UserRequested)).Returns(pending.Task);
        var sut = CreateSut();

        // Act
        var first = sut.SignOutEverywhereAsync();
        var busyDuringFirst = sut.IsSigningOutEverywhere;
        await sut.SignOutEverywhereAsync();
        pending.SetResult(true);
        await first;

        // Assert
        Assert.True(busyDuringFirst);
        Assert.False(sut.IsSigningOutEverywhere);
        mockAuth.Verify(x => x.SignOutEverywhereAsync(It.IsAny<SignOutReason>()), Times.Once);
        mockNavigation.Verify(x => x.NavigateTo("/login"), Times.Once);
    }

    [Fact]
    public async Task GivenPreviousFailure_WhenAskedAgain_ThenErrorCleared()
    {
        // Arrange
        mockAuth.Setup(x => x.SignOutEverywhereAsync(SignOutReason.UserRequested)).ReturnsAsync(false);
        var sut = CreateSut();
        await sut.SignOutEverywhereAsync();
        Assert.NotNull(sut.SignOutEverywhereError);

        // Act
        sut.AskSignOutEverywhereCommand.Execute(null);

        // Assert
        Assert.Null(sut.SignOutEverywhereError);
        Assert.True(sut.IsConfirmingSignOutEverywhere);
    }

    private ProfileViewModel CreateSut() => new(mockAuth.Object, mockUserDisplay.Object, mockNotifier.Object, mockLifecycle.Object, mockNavigation.Object, testText.Ui);
}
