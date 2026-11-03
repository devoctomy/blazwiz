using blazwiz.Portal.Application.Shell;
using blazwiz.Portal.Application.Shell.Interfaces;
using blazwiz.Portal.Models.Auth;

namespace blazwiz.Portal.Application.UnitTests.Shell;

public class SessionLifecycleTests
{
    private readonly List<string> calls = [];
    private readonly UserSession session = new() { Username = "bob@example.com" };

    [Fact]
    public void GivenNullHandlers_WhenConstructing_ThenThrowsArgumentNullException()
    {
        Assert.Equal("handlers", Assert.Throws<ArgumentNullException>(() => new SessionLifecycle(null!)).ParamName);
    }

    [Fact]
    public async Task GivenHandlersAddedInAnyOrder_WhenEachStage_ThenCalledInOrderWithTheSession()
    {
        // Arrange
        var sut = new SessionLifecycle([new RecordingHandler("late", 100, calls), new RecordingHandler("early", -10, calls)]);

        // Act
        await sut.SignedInAsync();
        await sut.ShellLoadingAsync(session);
        await sut.SessionStartedAsync(session);
        await sut.SigningOutAsync();
        await sut.SignedOutAsync();

        // Assert
        Assert.Equal(
        [
            "early signed in", "late signed in",
            "early loading bob@example.com", "late loading bob@example.com",
            "early started bob@example.com", "late started bob@example.com",
            "early signing out", "late signing out",
            "early signed out", "late signed out",
        ],
            calls);
    }

    [Fact]
    public async Task GivenAHandlerThrows_WhenAStage_ThenTheOthersStillRunAndNothingThrows()
    {
        // Arrange
        var sut = new SessionLifecycle([new RecordingHandler("first", 0, calls, fail: true), new RecordingHandler("second", 1, calls)]);

        // Act
        var exception = await Record.ExceptionAsync(() => sut.SessionStartedAsync(session));

        // Assert
        Assert.Null(exception);
        Assert.Equal(["first started bob@example.com", "second started bob@example.com"], calls);
    }

    [Fact]
    public async Task GivenHandlerImplementingNothing_WhenEachStage_ThenNothingHappens()
    {
        // Arrange
        var sut = new SessionLifecycle([new EmptyHandler()]);

        // Act
        var exception = await Record.ExceptionAsync(async () =>
        {
            await sut.SignedInAsync();
            await sut.ShellLoadingAsync(session);
            await sut.SessionStartedAsync(session);
            await sut.SigningOutAsync();
            await sut.SignedOutAsync();
        });

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task GivenNullSession_WhenShellLoadingOrStarted_ThenThrowsArgumentNullException()
    {
        var sut = new SessionLifecycle([]);

        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.ShellLoadingAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.SessionStartedAsync(null!));
    }

    private sealed class EmptyHandler : ISessionLifecycleHandler
    {
    }

    private sealed class RecordingHandler(string name, int order, List<string> calls, bool fail = false) : ISessionLifecycleHandler
    {
        public int Order => order;

        public Task OnSignedInAsync() => Record("signed in");

        public Task OnShellLoadingAsync(UserSession session) => Record("loading " + session.Username);

        public Task OnSessionStartedAsync(UserSession session) => Record("started " + session.Username);

        public Task OnSigningOutAsync() => Record("signing out");

        public Task OnSignedOutAsync() => Record("signed out");

        private Task Record(string what)
        {
            calls.Add(name + " " + what);
            return fail ? Task.FromException(new InvalidOperationException("boom")) : Task.CompletedTask;
        }
    }
}
