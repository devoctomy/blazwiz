using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Application.ViewModels;
using blazwiz.Portal.Models.Auth;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace blazwiz.Portal.Application.UnitTests.ViewModels;

public class SessionTimerViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IAuthenticationService> mockAuth = new();
    private readonly FakeTimeProvider time = new(Now);
    private UserSession? session = new() { Username = "bob@example.com", ExpiresAt = Now.AddMinutes(54).AddSeconds(12) };
    private int reads;

    public SessionTimerViewModelTests()
    {
        mockAuth.Setup(x => x.GetCurrentUserAsync()).Callback(() => reads++).ReturnsAsync(() => session);
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        Assert.Equal("authService", Assert.Throws<ArgumentNullException>(() => new SessionTimerViewModel(null!, time)).ParamName);
        Assert.Equal("timeProvider", Assert.Throws<ArgumentNullException>(() => new SessionTimerViewModel(mockAuth.Object, null!)).ParamName);
    }

    [Fact]
    public async Task GivenSession_WhenInitialized_ThenShowsTimeLeft()
    {
        using var sut = CreateSut();
        await sut.OnInitializedAsync();
        Assert.Equal("Session 54:12", sut.Text);
    }

    [Fact]
    public async Task GivenMoreThanAnHourLeft_WhenInitialized_ThenShowsHours()
    {
        session!.ExpiresAt = Now.AddHours(2);
        using var sut = CreateSut();
        await sut.OnInitializedAsync();
        Assert.Equal("Session 2:00:00", sut.Text);
    }

    [Fact]
    public async Task GivenNoSession_WhenInitialized_ThenShowsExpired()
    {
        session = null;
        using var sut = CreateSut();
        await sut.OnInitializedAsync();
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

    private void AdvanceSeconds(int seconds)
    {
        for (var i = 0; i < seconds; i++)
        {
            time.Advance(TimeSpan.FromSeconds(1));
        }
    }

    private SessionTimerViewModel CreateSut() => new(mockAuth.Object, time);
}
