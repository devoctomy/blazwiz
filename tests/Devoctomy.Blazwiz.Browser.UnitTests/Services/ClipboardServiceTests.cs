using Devoctomy.Blazwiz.Browser.Services;
using Microsoft.Extensions.Time.Testing;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Moq;

namespace Devoctomy.Blazwiz.Browser.UnitTests.Services;

public class ClipboardServiceTests
{
    private readonly Mock<IJSRuntime> mockJs = new();
    private readonly Mock<IJSObjectReference> mockModule = new();
    private readonly FakeTimeProvider time = new();
    private readonly List<string> cleared = [];

    public ClipboardServiceTests()
    {
        mockJs.Setup(x => x.InvokeAsync<IJSObjectReference>("import", It.Is<object?[]>(a => (string?)a[0] == ClipboardService.ModulePath)))
            .ReturnsAsync(mockModule.Object);
        mockModule.Setup(x => x.InvokeAsync<bool>("clearIfUnchanged", It.IsAny<CancellationToken>(), It.IsAny<object?[]>()))
            .Callback<string, CancellationToken, object?[]?>((_, _, a) => cleared.Add((string)a![0]!))
            .ReturnsAsync(true);
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("jsRuntime", Assert.Throws<ArgumentNullException>(() => new ClipboardService(null!, time)).ParamName);
        Assert.Equal("timeProvider", Assert.Throws<ArgumentNullException>(() => new ClipboardService(mockJs.Object, null!)).ParamName);
    }

    [Fact]
    public async Task GivenText_WhenCopied_ThenTheModuleCopiesIt_AndNothingIsCleared()
    {
        // Arrange
        using var sut = new ClipboardService(mockJs.Object, time);

        // Act
        await sut.CopyAsync("bob");
        time.Advance(TimeSpan.FromHours(1));

        // Assert
        mockModule.Verify(x => x.InvokeAsync<IJSVoidResult>("copy", It.Is<object?[]>(a => (string?)a[0] == "bob")), Times.Once);
        Assert.Empty(cleared);
    }

    [Fact]
    public async Task GivenAClearTime_WhenItPasses_ThenTheClipboardIsCleared()
    {
        // Arrange
        using var sut = new ClipboardService(mockJs.Object, time);
        await sut.CopyAsync("hunter2", TimeSpan.FromSeconds(30));

        // Act
        time.Advance(TimeSpan.FromSeconds(29));
        var early = cleared.Count;
        time.Advance(TimeSpan.FromSeconds(1));
        await WaitForAsync(() => cleared.Count == 1);

        // Assert
        Assert.Equal(0, early);
        Assert.Equal(["hunter2"], cleared);
    }

    [Fact]
    public async Task GivenAClearWaiting_WhenSomethingElseIsCopied_ThenOnlyTheLatestIsCleared()
    {
        // Arrange
        using var sut = new ClipboardService(mockJs.Object, time);
        await sut.CopyAsync("first", TimeSpan.FromSeconds(30));
        time.Advance(TimeSpan.FromSeconds(20));

        // Act
        await sut.CopyAsync("second", TimeSpan.FromSeconds(30));
        time.Advance(TimeSpan.FromSeconds(30));
        await WaitForAsync(() => cleared.Count >= 1);

        // Assert
        Assert.Equal(["second"], cleared);
    }

    [Fact]
    public async Task GivenAClearWaiting_WhenTextIsCopiedWithoutOne_ThenNothingIsCleared()
    {
        // Arrange
        using var sut = new ClipboardService(mockJs.Object, time);
        await sut.CopyAsync("secret", TimeSpan.FromSeconds(30));

        // Act
        await sut.CopyAsync("bob");
        time.Advance(TimeSpan.FromMinutes(1));
        await Task.Delay(20);

        // Assert
        Assert.Empty(cleared);
    }

    [Fact]
    public async Task GivenAClearWaiting_WhenDisposed_ThenNothingIsCleared()
    {
        // Arrange
        var sut = new ClipboardService(mockJs.Object, time);
        await sut.CopyAsync("secret", TimeSpan.FromSeconds(30));

        // Act
        sut.Dispose();
        time.Advance(TimeSpan.FromMinutes(1));
        await Task.Delay(20);

        // Assert
        Assert.Empty(cleared);
    }

    [Fact]
    public async Task GivenTheBrowserRefusesToClear_WhenTheTimePasses_ThenNothingIsThrown()
    {
        // Arrange
        mockModule.Setup(x => x.InvokeAsync<bool>("clearIfUnchanged", It.IsAny<CancellationToken>(), It.IsAny<object?[]>()))
            .ThrowsAsync(new JSException("NotAllowedError"));
        using var sut = new ClipboardService(mockJs.Object, time);
        await sut.CopyAsync("secret", TimeSpan.FromSeconds(1));

        // Act
        var ex = await Record.ExceptionAsync(async () =>
        {
            time.Advance(TimeSpan.FromSeconds(1));
            await Task.Delay(20);
        });

        // Assert
        Assert.Null(ex);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GivenAClearTimeThatIsNotPositive_WhenCopied_ThenThrowsArgumentOutOfRangeException(int seconds)
    {
        // Arrange
        using var sut = new ClipboardService(mockJs.Object, time);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => sut.CopyAsync("x", TimeSpan.FromSeconds(seconds)));
    }

    [Fact]
    public async Task GivenNoText_WhenCopied_ThenThrowsArgumentNullException()
    {
        // Arrange
        using var sut = new ClipboardService(mockJs.Object, time);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.CopyAsync(null!));
    }

    [Fact]
    public async Task GivenSeveralCopies_WhenCopied_ThenTheModuleIsImportedOnce()
    {
        // Arrange
        using var sut = new ClipboardService(mockJs.Object, time);

        // Act
        await sut.CopyAsync("a");
        await sut.CopyAsync("b");

        // Assert
        mockJs.Verify(x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object?[]>()), Times.Once);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(5);
        }
    }
}
