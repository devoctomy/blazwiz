using Devoctomy.Blazwiz.Core.Configuration;
using Devoctomy.Blazwiz.Core.Configuration.Interfaces;
using Devoctomy.Blazwiz.Sync;
using Devoctomy.Blazwiz.Sync.Connectivity;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Moq;

namespace Devoctomy.Blazwiz.Sync.UnitTests;

public class WebLockSyncLockServiceTests
{
    private readonly Mock<IAppStorageKeys> mockKeys = CreateKeys();

    private readonly Mock<IJSRuntime> mockJs = new();
    private readonly Mock<IJSObjectReference> mockModule = new();

    public WebLockSyncLockServiceTests()
    {
        mockJs.Setup(x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<CancellationToken>(), It.Is<object?[]>(a => (string?)a[0] == BrowserNetworkStatus.ModulePath)))
            .ReturnsAsync(mockModule.Object);
        mockModule.Setup(x => x.InvokeAsync<string>("acquireLock", It.IsAny<CancellationToken>(), It.Is<object?[]>(a => (string?)a[0] == "sync-notes")))
            .ReturnsAsync((string _, CancellationToken _, object?[] a) => (string)a[1]!);
    }

    [Fact]
    public void GivenNullJsRuntime_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("jsRuntime", Assert.Throws<ArgumentNullException>(() => new WebLockSyncLockService(null!, mockKeys.Object)).ParamName);
    }

    [Fact]
    public async Task GivenLockName_WhenAcquiredAndDisposed_ThenWebLockTakenAndReleasedOnce()
    {
        // Arrange
        var sut = new WebLockSyncLockService(mockJs.Object, mockKeys.Object);

        // Act
        var handle = await sut.AcquireAsync("sync-notes");
        await handle.DisposeAsync();
        await handle.DisposeAsync();

        // Assert
        mockModule.Verify(x => x.InvokeAsync<IJSVoidResult>("releaseLock", It.Is<object?[]>(a => (string?)a[0] == RequestId(0))), Times.Once);
    }

    [Fact]
    public async Task GivenPageGone_WhenReleasing_ThenNoException()
    {
        // Arrange
        mockModule.Setup(x => x.InvokeAsync<IJSVoidResult>("releaseLock", It.IsAny<object?[]>())).ThrowsAsync(new JSDisconnectedException("gone"));
        var handle = await new WebLockSyncLockService(mockJs.Object, mockKeys.Object).AcquireAsync("sync-notes");

        // Act & Assert
        await handle.DisposeAsync();
    }

    [Fact]
    public async Task GivenNoName_WhenAcquireAsync_ThenThrows()
    {
        // Act & Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(() => new WebLockSyncLockService(mockJs.Object, mockKeys.Object).AcquireAsync(" "));
    }

    [Fact]
    public async Task GivenTwoRequests_WhenAcquireAsync_ThenEachHasItsOwnRequestId()
    {
        // Arrange
        var sut = new WebLockSyncLockService(mockJs.Object, mockKeys.Object);

        // Act
        await sut.AcquireAsync("sync-notes");
        await sut.AcquireAsync("sync-notes");

        // Assert
        Assert.False(string.IsNullOrEmpty(RequestId(0)));
        Assert.NotEqual(RequestId(0), RequestId(1));
    }

    [Fact]
    public async Task GivenWaitCancelled_WhenAcquireAsync_ThenRequestIsCancelledInTheBrowser()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        mockModule.Setup(x => x.InvokeAsync<string>("acquireLock", cts.Token, It.IsAny<object?[]>()))
            .Returns(async (string _, CancellationToken ct, object?[] _) =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                return string.Empty;
            });
        var sut = new WebLockSyncLockService(mockJs.Object, mockKeys.Object);

        // Act
        var acquire = sut.AcquireAsync("sync-notes", cts.Token);
        await cts.CancelAsync();

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => acquire);
        mockModule.Verify(x => x.InvokeAsync<IJSVoidResult>("cancelLock", It.Is<object?[]>(a => (string?)a[0] == RequestId(0))), Times.Once);
    }

    [Fact]
    public async Task GivenPageGone_WhenCancelling_ThenStillThrowsOperationCanceledException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        mockModule.Setup(x => x.InvokeAsync<string>("acquireLock", cts.Token, It.IsAny<object?[]>())).ThrowsAsync(new OperationCanceledException(cts.Token));
        mockModule.Setup(x => x.InvokeAsync<IJSVoidResult>("cancelLock", It.IsAny<object?[]>())).ThrowsAsync(new JSDisconnectedException("gone"));
        var sut = new WebLockSyncLockService(mockJs.Object, mockKeys.Object);

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.AcquireAsync("sync-notes", cts.Token));
    }

    // The request id passed on the nth acquireLock call.
    private string? RequestId(int call)
    {
        var args = (object?[])mockModule.Invocations.Where(i => i.Arguments[0] is "acquireLock").ElementAt(call).Arguments[2];
        return (string?)args[1];
    }


    [Fact]
    public async Task GivenAppName_WhenAcquireAsync_ThenLockIsNamedAfterTheApp()
    {
        // Arrange
        mockModule.Setup(x => x.InvokeAsync<string>("acquireLock", It.IsAny<CancellationToken>(), It.Is<object?[]>(a => (string?)a[0] == "app_sync-notes")))
            .ReturnsAsync("lock");
        var sut = new WebLockSyncLockService(mockJs.Object, new AppStorageKeys(new BlazwizAppOptions { Name = "app" }));

        // Act
        await using var handle = await sut.AcquireAsync("sync-notes");

        // Assert
        mockModule.Verify(x => x.InvokeAsync<string>("acquireLock", It.IsAny<CancellationToken>(), It.Is<object?[]>(a => (string?)a[0] == "app_sync-notes")), Times.Once);
    }

    [Fact]
    public void GivenNullStorageKeys_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("storageKeys", Assert.Throws<ArgumentNullException>(() => new WebLockSyncLockService(mockJs.Object, null!)).ParamName);
    }

    // Keeps names as they are, so the tests can check them; the prefix itself is checked separately.
    private static Mock<IAppStorageKeys> CreateKeys()
    {
        var keys = new Mock<IAppStorageKeys>();
        keys.Setup(x => x.For(It.IsAny<string>())).Returns<string>(name => name);
        keys.SetupGet(x => x.Prefix).Returns(string.Empty);
        return keys;
    }
}
