using blazwiz.Core.Offline;
using blazwiz.Core.Offline.Models;

namespace blazwiz.Core.UnitTests.Offline;

public class OfflineModeServiceTests
{
    [Fact]
    public void GivenNew_WhenRead_ThenOnlineWithNoReason()
    {
        var sut = new OfflineModeService();
        Assert.False(sut.IsOffline);
        Assert.Null(sut.Reason);
    }

    [Fact]
    public void GivenOnline_WhenEnter_ThenOfflineWithReasonAndChangedRaisedOnce()
    {
        // Arrange
        var sut = new OfflineModeService();
        var raised = new List<bool>();
        sut.Changed += (_, v) => raised.Add(v);

        // Act
        sut.Enter(OfflineEntryReason.SignInUnreachable);
        sut.Enter(OfflineEntryReason.SignInUnreachable);

        // Assert
        Assert.True(sut.IsOffline);
        Assert.Equal(OfflineEntryReason.SignInUnreachable, sut.Reason);
        Assert.Equal([true], raised);
    }

    [Fact]
    public void GivenOffline_WhenExit_ThenOnlineAndChangedRaisedOnce()
    {
        // Arrange
        var sut = new OfflineModeService();
        sut.Enter(OfflineEntryReason.Chosen);
        var raised = new List<bool>();
        sut.Changed += (_, v) => raised.Add(v);

        // Act
        sut.Exit();
        sut.Exit();

        // Assert
        Assert.False(sut.IsOffline);
        Assert.Null(sut.Reason);
        Assert.Equal([false], raised);
    }

    [Fact]
    public void GivenSessionLength_WhenRead_ThenFifteenMinutes()
    {
        Assert.Equal(TimeSpan.FromMinutes(15), OfflineModeService.SessionLength);
    }
}
