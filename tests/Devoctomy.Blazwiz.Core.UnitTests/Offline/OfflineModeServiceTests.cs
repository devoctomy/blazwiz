using Devoctomy.Blazwiz.Core.Offline;
using Devoctomy.Blazwiz.Core.Offline.Models;

namespace Devoctomy.Blazwiz.Core.UnitTests.Offline;

public class OfflineModeServiceTests
{
    [Fact]
    public void GivenNew_WhenRead_ThenOnlineWithNoReason()
    {
        // Arrange
        var sut = new OfflineModeService();

        // Act & Assert
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
        // Act & Assert
        Assert.Equal(TimeSpan.FromMinutes(15), OfflineModeService.SessionLength);
    }
}
