using blazwiz.Core.Sync.Connectivity;
using blazwiz.Core.Sync.Models;
using blazwiz.Portal.Application.Services;

namespace blazwiz.Portal.Application.UnitTests.Services;

public class SyncStatusPresenterTests
{
    private static readonly ConnectivityStatus Online = new() { IsOnline = true, LastCheckedAt = DateTimeOffset.UnixEpoch };

    private readonly SyncStatusPresenter sut = new();

    [Fact]
    public void GivenSyncing_WhenPresent_ThenSyncing()
    {
        var result = sut.Present(new SyncStatus { State = SyncState.Syncing, PendingCount = 3 }, Online);

        Assert.Equal("Syncing", result.Text);
        Assert.True(result.IsSyncing);
    }

    [Fact]
    public void GivenOffline_WhenPresent_ThenOfflineWithReasonInTooltip()
    {
        var result = sut.Present(new SyncStatus(), new ConnectivityStatus { Reason = OfflineReason.NoCredentials });

        Assert.Equal("Offline", result.Text);
        Assert.Equal("Offline: no credentials", result.Tooltip);
        Assert.True(result.IsOffline);
    }

    [Fact]
    public void GivenOfflineWithPendingChanges_WhenPresent_ThenPendingCountShown()
    {
        var result = sut.Present(new SyncStatus { PendingCount = 1 }, new ConnectivityStatus { Reason = OfflineReason.OfflineMode });

        Assert.Equal("Offline · 1 pending", result.Text);
        Assert.Equal("Offline: offline mode. 1 change waiting to sync.", result.Tooltip);
    }

    [Theory]
    [InlineData(0, "Sync off", "Sync is turned off.")]
    [InlineData(2, "Sync off · 2 pending", "Sync is turned off. 2 changes waiting to sync.")]
    public void GivenSyncTurnedOff_WhenPresent_ThenSyncOffNotOffline(int pending, string text, string tooltip)
    {
        var result = sut.Present(new SyncStatus { PendingCount = pending }, new ConnectivityStatus { Reason = OfflineReason.SyncDisabled });

        Assert.Equal(text, result.Text);
        Assert.Equal(tooltip, result.Tooltip);
        Assert.True(result.IsOffline);
    }

    [Fact]
    public void GivenError_WhenPresent_ThenErrorWithLastError()
    {
        var result = sut.Present(new SyncStatus { State = SyncState.Error, LastError = "S3 refused the credentials." }, Online);

        Assert.Equal("Sync error", result.Text);
        Assert.Equal("S3 refused the credentials.", result.Tooltip);
        Assert.Equal("danger", result.Tone);
    }

    [Fact]
    public void GivenPending_WhenPresent_ThenPendingCount()
    {
        var result = sut.Present(new SyncStatus { PendingCount = 3 }, Online);

        Assert.Equal("3 pending", result.Text);
        Assert.Equal("3 changes waiting to sync", result.Tooltip);
    }

    [Fact]
    public void GivenNotCheckedYet_WhenPresent_ThenChecking()
    {
        Assert.Equal("Checking", sut.Present(new SyncStatus(), new ConnectivityStatus()).Text);
    }

    [Theory]
    [InlineData(0, "All changes synced")]
    [InlineData(1, "All changes synced. 1 conflict resolved.")]
    [InlineData(2, "All changes synced. 2 conflicts resolved.")]
    public void GivenUpToDate_WhenPresent_ThenUpToDateWithConflicts(int conflicts, string tooltip)
    {
        var result = sut.Present(new SyncStatus { ConflictsResolved = conflicts }, Online);

        Assert.Equal("Up to date", result.Text);
        Assert.Equal(tooltip, result.Tooltip);
        Assert.Equal("success", result.Tone);
    }

    [Fact]
    public void GivenNulls_WhenPresent_ThenThrows()
    {
        Assert.Throws<ArgumentNullException>(() => sut.Present(null!, Online));
        Assert.Throws<ArgumentNullException>(() => sut.Present(new SyncStatus(), null!));
    }
}
