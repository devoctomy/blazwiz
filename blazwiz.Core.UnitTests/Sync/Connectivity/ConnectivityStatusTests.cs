using blazwiz.Core.Sync.Connectivity;

namespace blazwiz.Core.UnitTests.Sync.Connectivity;

public class ConnectivityStatusTests
{
    [Theory]
    [InlineData(true, null, "Online")]
    [InlineData(false, OfflineReason.OfflineMode, "Offline: offline mode")]
    [InlineData(false, OfflineReason.SyncDisabled, "Sync turned off")]
    [InlineData(false, OfflineReason.NoNetwork, "Offline: no network")]
    [InlineData(false, OfflineReason.NoCredentials, "Offline: no credentials")]
    [InlineData(false, OfflineReason.Unreachable, "Offline: S3 unreachable")]
    [InlineData(false, null, "Checking connection")]
    public void GivenStatus_WhenDescribe_ThenReasonIsReadable(bool isOnline, OfflineReason? reason, string expected)
    {
        Assert.Equal(expected, new ConnectivityStatus { IsOnline = isOnline, Reason = reason }.Describe());
    }

    [Fact]
    public void GivenStatus_WhenClone_ThenCopyIsIndependent()
    {
        var sut = new ConnectivityStatus { IsOnline = true };
        var copy = sut.Clone();
        copy.IsOnline = false;

        Assert.True(sut.IsOnline);
    }
}
