using blazwiz.Core.Sync.Interfaces;
using blazwiz.Portal.Application.Services;
using Moq;

namespace blazwiz.Portal.Application.UnitTests.Services;

public class StartSyncOffSignInOptionTests
{
    private readonly Mock<IConnectivityService> mockConnectivity = new();

    [Fact]
    public void GivenNullConnectivity_WhenConstructing_ThenThrowsArgumentNullException()
    {
        Assert.Equal("connectivityService", Assert.Throws<ArgumentNullException>(() => new StartSyncOffSignInOption(null!)).ParamName);
    }

    [Fact]
    public async Task GivenSyncTurnedOff_WhenLoadAsync_ThenTickedAndSet()
    {
        // Arrange
        mockConnectivity.SetupGet(x => x.IsSyncEnabled).Returns(false);
        var sut = new StartSyncOffSignInOption(mockConnectivity.Object);

        // Act
        await sut.LoadAsync();

        // Assert
        mockConnectivity.Verify(x => x.InitializeAsync(), Times.Once);
        Assert.True(sut.StartWithSyncOff);
        Assert.True(sut.IsSet);
    }

    [Fact]
    public async Task GivenSyncOn_WhenLoadAsync_ThenNotSet()
    {
        // Arrange
        mockConnectivity.SetupGet(x => x.IsSyncEnabled).Returns(true);
        var sut = new StartSyncOffSignInOption(mockConnectivity.Object);

        // Act
        await sut.LoadAsync();

        // Assert
        Assert.False(sut.StartWithSyncOff);
        Assert.False(sut.IsSet);
    }

    [Fact]
    public async Task GivenTicked_WhenApplyAsync_ThenSyncTurnedOff()
    {
        // Arrange
        mockConnectivity.SetupGet(x => x.IsSyncEnabled).Returns(true);
        var sut = new StartSyncOffSignInOption(mockConnectivity.Object) { StartWithSyncOff = true };

        // Act
        await sut.ApplyAsync();

        // Assert
        mockConnectivity.Verify(x => x.SetSyncEnabledAsync(false), Times.Once);
    }

    [Fact]
    public async Task GivenSyncOffButUnticked_WhenApplyAsync_ThenSyncTurnedOn()
    {
        // Arrange
        mockConnectivity.SetupGet(x => x.IsSyncEnabled).Returns(false);
        var sut = new StartSyncOffSignInOption(mockConnectivity.Object) { StartWithSyncOff = false };

        // Act
        await sut.ApplyAsync();

        // Assert
        mockConnectivity.Verify(x => x.SetSyncEnabledAsync(true), Times.Once);
    }

    [Fact]
    public async Task GivenOptionMatchesSync_WhenApplyAsync_ThenSyncLeftAlone()
    {
        // Arrange
        mockConnectivity.SetupGet(x => x.IsSyncEnabled).Returns(true);
        var sut = new StartSyncOffSignInOption(mockConnectivity.Object);

        // Act
        await sut.ApplyAsync();

        // Assert
        mockConnectivity.Verify(x => x.SetSyncEnabledAsync(It.IsAny<bool>()), Times.Never);
    }
}
