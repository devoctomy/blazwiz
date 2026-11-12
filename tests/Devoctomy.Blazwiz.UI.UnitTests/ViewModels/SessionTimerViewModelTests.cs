using Devoctomy.Blazwiz.Testing;
using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.Core.Offline;
using Devoctomy.Blazwiz.Core.Offline.Models;
using Devoctomy.Blazwiz.UI.ViewModels;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace Devoctomy.Blazwiz.UI.UnitTests.ViewModels;

public class SessionTimerViewModelTests
{
    private readonly FrameworkText testText = new();

    private static readonly DateTimeOffset Now = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IAuthenticationService> mockAuth = new();
    private readonly FakeTimeProvider time = new(Now);
    private readonly OfflineModeService offlineMode = new();
    private UserSession? session = new() { Username = "bob@example.com", ExpiresAt = Now.AddMinutes(54).AddSeconds(12) };
    private int reads;

    public SessionTimerViewModelTests()
    {
        mockAuth.Setup(x => x.GetCurrentUserAsync()).Callback(() => reads++).ReturnsAsync(() => session);

        // Reading the session in the last five minutes refreshes it, as with Cognito.
        mockAuth.SetupGet(x => x.RefreshMargin).Returns(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("authService", Assert.Throws<ArgumentNullException>(() => new SessionTimerViewModel(null!, offlineMode, time, testText.Ui)).ParamName);
        Assert.Equal("offlineModeService", Assert.Throws<ArgumentNullException>(() => new SessionTimerViewModel(mockAuth.Object, null!, time, testText.Ui)).ParamName);
        Assert.Equal("timeProvider", Assert.Throws<ArgumentNullException>(() => new SessionTimerViewModel(mockAuth.Object, offlineMode, null!, testText.Ui)).ParamName);
    }

    [Fact]
    public async Task GivenOnlineSession_WhenOfflineModeTurnedOn_ThenReadAgainStraightAway()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.OnInitializedAsync();
        session = new UserSession { Username = "bob", ExpiresAt = Now.AddMinutes(15), IsOffline = true };

        // Act
        offlineMode.Enter(OfflineEntryReason.Chosen);

        // Assert
        Assert.Equal("Offline 15:00", sut.Text);
    }

    [Fact]
    public async Task GivenSession_WhenInitialized_ThenShowsTimeLeft()
    {
        // Arrange
        using var sut = CreateSut();

        // Act
        await sut.OnInitializedAsync();

        // Assert
        Assert.Equal("Session 54:12", sut.Text);
    }

    [Fact]
    public async Task GivenMoreThanAnHourLeft_WhenInitialized_ThenShowsHours()
    {
        // Arrange
        session!.ExpiresAt = Now.AddHours(2);
        using var sut = CreateSut();

        // Act
        await sut.OnInitializedAsync();

        // Assert
        Assert.Equal("Session 2:00:00", sut.Text);
    }

    [Fact]
    public async Task GivenOfflineSession_WhenInitialized_ThenShowsOfflineCountdown()
    {
        // Arrange
        session = new UserSession { Username = "bob", ExpiresAt = Now.AddMinutes(12).AddSeconds(34), IsOffline = true };
        using var sut = CreateSut();

        // Act
        await sut.OnInitializedAsync();

        // Assert
        Assert.Equal("Offline 12:34", sut.Text);
    }

    [Fact]
    public async Task GivenNoSession_WhenInitialized_ThenShowsExpired()
    {
        // Arrange
        session = null;
        using var sut = CreateSut();

        // Act
        await sut.OnInitializedAsync();

        // Assert
        Assert.Equal("Session expired", sut.Text);
    }

    [Fact]
    public async Task GivenTimePasses_WhenTicking_ThenCountsDown()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.OnInitializedAsync();

        // Act
        time.Advance(TimeSpan.FromSeconds(12));

        // Assert
        Assert.Equal("Session 54:00", sut.Text);
    }

    [Fact]
    public async Task GivenWellBeforeExpiry_WhenTicking_ThenReadsAgainEveryReadInterval()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.OnInitializedAsync();
        session = new UserSession { Username = "bob@example.com", ExpiresAt = Now.AddMinutes(90) };

        // Act
        AdvanceSeconds(29);
        var readsBefore = reads;
        AdvanceSeconds(1);

        // Assert
        Assert.Equal(1, readsBefore);
        Assert.Equal(2, reads);
        Assert.Equal("Session 1:29:30", sut.Text);
    }

    [Fact]
    public async Task GivenWithinRefreshMargin_WhenTicking_ThenNotReadUntilExpiredThenPicksUpRefresh()
    {
        // Arrange
        session!.ExpiresAt = Now.AddMinutes(4);
        using var sut = CreateSut();
        await sut.OnInitializedAsync();

        // Act: nothing is read within the margin, as that would refresh or end the session early.
        AdvanceSeconds(239);
        var readsBeforeExpiry = reads;
        var textBeforeExpiry = sut.Text;
        session = new UserSession { Username = "bob@example.com", ExpiresAt = Now.AddMinutes(64) };
        AdvanceSeconds(1);

        // Assert
        Assert.Equal(1, readsBeforeExpiry);
        Assert.Equal("Session 00:01", textBeforeExpiry);
        Assert.Equal(2, reads);
        Assert.Equal("Session 1:00:00", sut.Text);
    }

    [Fact]
    public async Task GivenExpiredAndNotRenewed_WhenTicking_ThenShowsExpiredAndStopsReading()
    {
        // Arrange
        session!.ExpiresAt = Now.AddMinutes(1);
        using var sut = CreateSut();
        await sut.OnInitializedAsync();
        session = null;

        // Act
        AdvanceSeconds(60);
        AdvanceSeconds(120);

        // Assert
        Assert.Equal("Session expired", sut.Text);
        Assert.Equal(2, reads);
    }

    [Fact]
    public async Task GivenExpiresSoonAfterARead_WhenTicking_ThenReadsStraightAwayAtExpiryNotAtTheNextInterval()
    {
        // Arrange
        session!.ExpiresAt = Now.AddSeconds(4);
        using var sut = CreateSut();
        await sut.OnInitializedAsync();

        // Act
        AdvanceSeconds(3);
        var readsBeforeExpiry = reads;
        AdvanceSeconds(1);

        // Assert
        Assert.Equal(1, readsBeforeExpiry);
        Assert.Equal(2, reads);
    }

    [Fact]
    public async Task GivenDisposed_WhenTimePasses_ThenNoLongerUpdates()
    {
        // Arrange
        var sut = CreateSut();
        await sut.OnInitializedAsync();

        // Act
        sut.Dispose();
        time.Advance(TimeSpan.FromMinutes(1));

        // Assert
        Assert.Equal("Session 54:12", sut.Text);
    }

    [Fact]
    public void GivenNullText_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("uiText", Assert.Throws<ArgumentNullException>(() => new SessionTimerViewModel(mockAuth.Object, offlineMode, time, null!)).ParamName);
    }

    [Fact]
    public void GivenNotInitialized_WhenDisposed_ThenDoesNotThrow()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var exception = Record.Exception(sut.Dispose);

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task GivenReadingTheSessionFails_WhenTicking_ThenKeepsCountingDown()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.OnInitializedAsync();
        mockAuth.Setup(x => x.GetCurrentUserAsync()).Callback(() => reads++).ThrowsAsync(new HttpRequestException("network down"));

        // Act
        AdvanceSeconds(30);
        var textAfterFailedRead = sut.Text;
        AdvanceSeconds(1);

        // Assert
        Assert.Equal(2, reads);
        Assert.Equal("Session 53:43", textAfterFailedRead);
        Assert.Equal("Session 53:41", sut.Text);
    }

    [Fact]
    public async Task GivenReadingTheSessionFails_WhenOfflineModeChanges_ThenDoesNotThrowAndKeepsText()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.OnInitializedAsync();
        mockAuth.Setup(x => x.GetCurrentUserAsync()).Callback(() => reads++).ThrowsAsync(new InvalidOperationException("storage unavailable"));

        // Act
        var exception = Record.Exception(() => offlineMode.Enter(OfflineEntryReason.Chosen));

        // Assert
        Assert.Null(exception);
        Assert.Equal(2, reads);
        Assert.Equal("Session 54:12", sut.Text);
    }

    [Fact]
    public async Task GivenExpiredSessionStillReturned_WhenTicking_ThenReadAgainOnlyEveryReadInterval()
    {
        // Arrange
        session!.ExpiresAt = Now.AddSeconds(1);
        using var sut = CreateSut();
        await sut.OnInitializedAsync();

        // Act
        AdvanceSeconds(1);
        var readsAtExpiry = reads;
        AdvanceSeconds(29);
        var readsBeforeInterval = reads;
        AdvanceSeconds(1);

        // Assert
        Assert.Equal(2, readsAtExpiry);
        Assert.Equal(2, readsBeforeInterval);
        Assert.Equal(3, reads);
        Assert.Equal("Session expired", sut.Text);
    }

    private void AdvanceSeconds(int seconds)
    {
        for (var i = 0; i < seconds; i++)
        {
            time.Advance(TimeSpan.FromSeconds(1));
        }
    }

    private SessionTimerViewModel CreateSut() => new(mockAuth.Object, offlineMode, time, testText.Ui);
}
