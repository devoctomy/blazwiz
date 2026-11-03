using Devoctomy.Blazwiz.Sync.Connectivity;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;
using Devoctomy.Blazwiz.UI.Sync.Models;
using Devoctomy.Blazwiz.UI.Sync.Services;
using Devoctomy.Blazwiz.UI.Sync.ViewModels;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace Devoctomy.Blazwiz.UI.Sync.UnitTests.ViewModels;

public class SyncViewModelTests
{
    private readonly TestText testText = new();

    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<ISyncEventLog> mockLog = new();
    private readonly Mock<ISyncService> mockSync = new();
    private readonly Mock<IConnectivityService> mockConnectivity = new();
    private readonly Mock<ISyncScheduler> mockScheduler = new();
    private readonly SyncEventPresenter eventPresenter;
    private readonly FakeTimeProvider time = new(Now);
    private readonly List<SyncEvent> events = [];
    private readonly List<SyncEventQuery> queries = [];
    private ConnectivityStatus connectivity = new() { IsOnline = false, Reason = OfflineReason.NoCredentials, LastCheckedAt = Now };

    public SyncViewModelTests()
    {
        eventPresenter = new SyncEventPresenter(testText.Sync, testText.Plurals, testText.Errors);
        time.SetLocalTimeZone(TimeZoneInfo.Utc);
        mockSync.SetupGet(x => x.Status).Returns(new SyncStatus { PendingCount = 2 });
        mockSync.SetupGet(x => x.Profiles).Returns(["notes"]);
        mockConnectivity.SetupGet(x => x.Status).Returns(() => connectivity);
        mockLog.Setup(x => x.QueryAsync(It.IsAny<SyncEventQuery>())).ReturnsAsync((SyncEventQuery q) =>
        {
            queries.Add(q);
            var matching = events
                .Where(e => q.Kinds == null || q.Kinds.Contains(e.Kind))
                .Where(e => q.Text == null || (e.Key?.Contains(q.Text, StringComparison.OrdinalIgnoreCase) ?? false))
                .OrderByDescending(e => e.Timestamp)
                .ToList();
            return new SyncEventPage { Events = matching.Take(q.Take).ToList(), HasMore = matching.Count > q.Take };
        });
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Arrange
        var presenter = new SyncStatusPresenter(testText.Sync, testText.Plurals, testText.Errors);

        // Act & Assert
        Assert.Equal("eventLog", Assert.Throws<ArgumentNullException>(() => new SyncViewModel(null!, eventPresenter, presenter, mockSync.Object, mockConnectivity.Object, mockScheduler.Object, time, testText.Sync)).ParamName);
        Assert.Equal("eventPresenter", Assert.Throws<ArgumentNullException>(() => new SyncViewModel(mockLog.Object, null!, presenter, mockSync.Object, mockConnectivity.Object, mockScheduler.Object, time, testText.Sync)).ParamName);
        Assert.Equal("statusPresenter", Assert.Throws<ArgumentNullException>(() => new SyncViewModel(mockLog.Object, eventPresenter, null!, mockSync.Object, mockConnectivity.Object, mockScheduler.Object, time, testText.Sync)).ParamName);
        Assert.Equal("syncService", Assert.Throws<ArgumentNullException>(() => new SyncViewModel(mockLog.Object, eventPresenter, presenter, null!, mockConnectivity.Object, mockScheduler.Object, time, testText.Sync)).ParamName);
        Assert.Equal("connectivityService", Assert.Throws<ArgumentNullException>(() => new SyncViewModel(mockLog.Object, eventPresenter, presenter, mockSync.Object, null!, mockScheduler.Object, time, testText.Sync)).ParamName);
        Assert.Equal("syncScheduler", Assert.Throws<ArgumentNullException>(() => new SyncViewModel(mockLog.Object, eventPresenter, presenter, mockSync.Object, mockConnectivity.Object, null!, time, testText.Sync)).ParamName);
        Assert.Equal("timeProvider", Assert.Throws<ArgumentNullException>(() => new SyncViewModel(mockLog.Object, eventPresenter, presenter, mockSync.Object, mockConnectivity.Object, mockScheduler.Object, null!, testText.Sync)).ParamName);
    }

    [Fact]
    public async Task GivenPasses_WhenInitialized_ThenEachPassIsOneRowWithItsFileEntries()
    {
        // Arrange
        AddPass("p1", Now.AddMinutes(-5), (SyncEventKind.UploadNew, "notes/a.md"), (SyncEventKind.DownloadNew, "notes/b.md"));
        Add(new SyncEvent { Kind = SyncEventKind.WentOffline, Code = SyncEventCodes.WentOffline, Arguments = ["NoCredentials"], Timestamp = Now.AddMinutes(-1) });
        var sut = CreateSut();

        // Act
        await sut.OnInitializedAsync();

        // Assert
        Assert.False(sut.IsLoading);
        var day = Assert.Single(sut.Days);
        Assert.Equal("Today", day.Label);
        Assert.Equal(2, day.Rows.Count);
        Assert.Equal("Offline: no credentials", day.Rows[0].Header.Message);
        Assert.Equal("bi-wifi-off", day.Rows[0].Header.Presentation.Icon);
        Assert.Equal("1 min ago", day.Rows[0].Header.RelativeTime);
        Assert.Equal("2026-10-01 11:59:00", day.Rows[0].Header.ExactTime);
        var pass = day.Rows[1];
        Assert.Equal("p1", pass.Id);
        Assert.Equal("Checking for changes · 1 uploaded, 1 downloaded", pass.Header.Message);
        Assert.Equal(["notes/a.md", "notes/b.md"], pass.Children.Select(c => c.Key));
        Assert.False(pass.IsExpanded);
        Assert.Equal(SyncViewModel.PageSize, queries[0].Take);
    }

    [Fact]
    public async Task GivenPassWithFailure_WhenInitialized_ThenExpandedByDefaultUntilCollapsed()
    {
        // Arrange
        AddPass("p1", Now, (SyncEventKind.Failed, "notes/a.md"));
        var sut = CreateSut();
        await sut.OnInitializedAsync();
        var row = sut.Days[0].Rows[0];

        // Act & Assert
        Assert.True(row.HasErrors);
        Assert.True(row.IsExpanded);
        sut.ToggleRowCommand.Execute("p1");
        Assert.False(sut.Days[0].Rows[0].IsExpanded);
        await sut.LoadMoreCommand.ExecuteAsync(null);
        Assert.False(sut.Days[0].Rows[0].IsExpanded);
    }

    [Fact]
    public async Task GivenRow_WhenToggled_ThenStaysExpandedAcrossReloads()
    {
        // Arrange
        AddPass("p1", Now, (SyncEventKind.UploadNew, "notes/a.md"));
        var sut = CreateSut();
        await sut.OnInitializedAsync();

        // Act
        sut.ToggleRowCommand.Execute("p1");
        sut.ToggleRowCommand.Execute("missing");
        mockLog.Raise(x => x.EventsWritten += null, mockLog.Object, EventArgs.Empty);
        await WaitForAsync(() => queries.Count == 2);

        // Assert
        Assert.True(sut.Days[0].Rows[0].IsExpanded);
    }

    [Fact]
    public async Task GivenCollapsedNoChangePasses_WhenInitialized_ThenOneRowWithCount()
    {
        // Arrange
        Add(new SyncEvent { PassId = "p9", Kind = SyncEventKind.PassCompleted, IsNoChangePass = true, Count = 4, Code = SyncEventCodes.NoChanges, Timestamp = Now });
        var sut = CreateSut();

        // Act
        await sut.OnInitializedAsync();

        // Assert
        var row = sut.Days[0].Rows[0];
        Assert.Equal("Checking for changes · up to date", row.Header.Message);
        Assert.Equal(4, row.Header.Count);
        Assert.Empty(row.Children);
    }

    [Fact]
    public async Task GivenPassInProgress_WhenInitialized_ThenHeaderSaysChecking()
    {
        // Arrange
        Add(new SyncEvent { PassId = "p1", Kind = SyncEventKind.PassStarted, Code = SyncEventCodes.Checking, Timestamp = Now });

        // Act
        var sut = CreateSut();
        await sut.OnInitializedAsync();

        // Assert
        Assert.Equal("Checking for changes", sut.Days[0].Rows[0].Header.Message);
    }

    [Fact]
    public async Task GivenEventsOnSeveralDays_WhenInitialized_ThenGroupedByDayNewestFirst()
    {
        // Arrange
        Add(new SyncEvent { Kind = SyncEventKind.WentOnline, Code = SyncEventCodes.BackOnline, Timestamp = Now });
        Add(new SyncEvent { Kind = SyncEventKind.WentOnline, Code = SyncEventCodes.BackOnline, Timestamp = Now.AddDays(-1) });
        Add(new SyncEvent { Kind = SyncEventKind.WentOnline, Code = SyncEventCodes.BackOnline, Timestamp = new DateTimeOffset(2026, 9, 28, 9, 0, 0, TimeSpan.Zero) });

        // Act
        var sut = CreateSut();
        await sut.OnInitializedAsync();

        // Assert
        Assert.Equal(["Today", "Yesterday", new DateOnly(2026, 9, 28).ToString("dddd d MMMM yyyy", System.Globalization.CultureInfo.CurrentCulture)], sut.Days.Select(d => d.Label));
        Assert.Equal("3 d ago", sut.Days[2].Rows[0].Header.RelativeTime);
        Assert.Equal("1 d ago", sut.Days[1].Rows[0].Header.RelativeTime);
    }

    [Fact]
    public async Task GivenFilterChip_WhenSelected_ThenOnlyThatGroupQueriedAndShownFlat()
    {
        // Arrange
        AddPass("p1", Now, (SyncEventKind.UploadNew, "notes/a.md"));
        Add(new SyncEvent { Kind = SyncEventKind.WentOffline, Code = SyncEventCodes.WentOffline, Arguments = ["NoCredentials"], Timestamp = Now });
        var sut = CreateSut();
        await sut.OnInitializedAsync();

        // Act
        await sut.SelectGroupCommand.ExecuteAsync(SyncEventGroup.Connectivity);

        // Assert
        Assert.Equal(SyncEventGroup.Connectivity, sut.ActiveGroup);
        Assert.True(sut.Chips.Single(c => c.Group == SyncEventGroup.Connectivity).IsActive);
        Assert.False(sut.Chips[0].IsActive);
        Assert.Contains(SyncEventKind.WentOffline, queries[^1].Kinds!);
        Assert.Equal("Offline: no credentials", Assert.Single(sut.Days[0].Rows).Header.Message);

        await sut.SelectGroupCommand.ExecuteAsync(SyncEventGroup.Uploads);
        var upload = Assert.Single(sut.Days[0].Rows);
        Assert.Equal("notes/a.md", upload.Header.Key);
        Assert.Empty(upload.Children);

        await sut.SelectGroupCommand.ExecuteAsync(null);
        Assert.Null(queries[^1].Kinds);
        Assert.True(sut.Chips[0].IsActive);
        Assert.Equal(8, sut.Chips.Count);
    }

    [Fact]
    public async Task GivenSearchText_WhenTypingStops_ThenSearchRunsOnceAfterDebounce()
    {
        // Arrange
        AddPass("p1", Now, (SyncEventKind.UploadNew, "notes/Ideas.md"), (SyncEventKind.UploadNew, "notes/Plan.md"));
        var sut = CreateSut();
        await sut.OnInitializedAsync();

        // Act
        sut.SearchText = "ide";
        time.Advance(TimeSpan.FromMilliseconds(100));
        sut.SearchText = "ideas";
        time.Advance(SyncViewModel.SearchDebounce - TimeSpan.FromMilliseconds(1));
        var queriesBeforeDebounce = queries.Count;
        time.Advance(TimeSpan.FromMilliseconds(1));
        await WaitForAsync(() => queries.Count == 2);

        // Assert
        Assert.Equal(1, queriesBeforeDebounce);
        Assert.Equal(2, queries.Count);
        Assert.Equal("ideas", queries[^1].Text);
        Assert.Equal("notes/Ideas.md", Assert.Single(sut.Days[0].Rows).Header.Key);
    }

    [Fact]
    public async Task GivenSearchText_WhenSearching_ThenTheShownTextIsSearchedToo()
    {
        // Arrange
        Add(new SyncEvent { Kind = SyncEventKind.WentOffline, Code = SyncEventCodes.WentOffline, Arguments = ["NoCredentials"], Timestamp = Now });
        var sut = CreateSut();
        await sut.OnInitializedAsync();

        // Act
        sut.SearchText = "credentials";
        time.Advance(SyncViewModel.SearchDebounce);
        await WaitForAsync(() => queries.Count == 2);

        // Assert: the query describes events as the page shows them.
        var describe = queries[^1].Describe;
        Assert.NotNull(describe);
        Assert.Contains("credentials", describe(new SyncEvent { Kind = SyncEventKind.WentOffline, Code = SyncEventCodes.WentOffline, Arguments = ["NoCredentials"] }), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GivenMoreEntries_WhenLoadMore_ThenNextPageRequested()
    {
        // Arrange
        for (var i = 0; i < SyncViewModel.PageSize + 1; i++)
        {
            Add(new SyncEvent { Kind = SyncEventKind.WentOnline, Code = SyncEventCodes.BackOnline, Timestamp = Now.AddSeconds(-i) });
        }

        var sut = CreateSut();
        await sut.OnInitializedAsync();

        // Act
        var hadMore = sut.HasMore;
        await sut.LoadMoreCommand.ExecuteAsync(null);

        // Assert
        Assert.True(hadMore);
        Assert.False(sut.HasMore);
        Assert.Equal(SyncViewModel.PageSize * 2, queries[^1].Take);
        Assert.Equal(SyncViewModel.PageSize + 1, sut.Days.Sum(d => d.Rows.Count));
    }

    [Fact]
    public async Task GivenNewEvents_WhenWritten_ThenLogReloadsLive()
    {
        // Arrange
        var sut = CreateSut();
        await sut.OnInitializedAsync();
        Add(new SyncEvent { Kind = SyncEventKind.WentOnline, Code = SyncEventCodes.BackOnline, Timestamp = Now });

        // Act
        mockLog.Raise(x => x.EventsWritten += null, mockLog.Object, EventArgs.Empty);
        await WaitForAsync(() => sut.Days.Count == 1);

        // Assert
        Assert.Single(sut.Days);
    }

    [Fact]
    public async Task GivenClearDialog_WhenConfirmed_ThenLogCleared()
    {
        // Arrange
        var sut = CreateSut();
        await sut.OnInitializedAsync();

        // Act
        sut.ShowClearDialogCommand.Execute(null);
        var opened = sut.IsClearDialogOpen;
        sut.CloseClearDialogCommand.Execute(null);
        var closed = !sut.IsClearDialogOpen;
        sut.ShowClearDialogCommand.Execute(null);
        await sut.ClearLogCommand.ExecuteAsync(null);

        // Assert
        Assert.True(opened);
        Assert.True(closed);
        Assert.False(sut.IsClearDialogOpen);
        mockLog.Verify(x => x.ClearAsync(), Times.Once);
    }

    [Fact]
    public async Task GivenStatus_WhenInitialized_ThenHeaderShowsStatusPendingAndSyncOff()
    {
        // Arrange
        mockConnectivity.SetupGet(x => x.IsSyncEnabled).Returns(false);
        connectivity = new ConnectivityStatus { Reason = OfflineReason.SyncDisabled, LastCheckedAt = Now };
        var sut = CreateSut();

        // Act
        await sut.OnInitializedAsync();

        // Assert
        Assert.Equal("Sync off · 2 pending", sut.Status.Text);
        Assert.Equal(2, sut.SyncStatus.PendingCount);
        Assert.False(sut.IsSyncEnabled);
        Assert.False(sut.CanSyncNow);
        Assert.False(sut.ShowProfileFilter);
    }

    [Fact]
    public async Task GivenOnline_WhenSyncNowAndToggleSync_ThenDelegated()
    {
        // Arrange
        connectivity = new ConnectivityStatus { IsOnline = true, LastCheckedAt = Now };
        mockConnectivity.SetupGet(x => x.IsSyncEnabled).Returns(true);
        var sut = CreateSut();

        // Act
        await sut.SyncNowCommand.ExecuteAsync(null);
        await sut.ToggleSyncCommand.ExecuteAsync(null);

        // Assert
        mockScheduler.Verify(x => x.SyncNowAsync(), Times.Once);
        mockConnectivity.Verify(x => x.SetSyncEnabledAsync(false), Times.Once);
    }

    [Fact]
    public async Task GivenOffline_WhenSyncNow_ThenNothingHappens()
    {
        // Act
        await CreateSut().SyncNowCommand.ExecuteAsync(null);

        // Assert
        mockScheduler.Verify(x => x.SyncNowAsync(), Times.Never);
    }

    [Fact]
    public async Task GivenSeveralProfiles_WhenProfileSelected_ThenFilterShownAndQueried()
    {
        // Arrange
        mockSync.SetupGet(x => x.Profiles).Returns(["notes", "photos"]);
        var sut = CreateSut();

        // Act
        await sut.SelectProfileCommand.ExecuteAsync("photos");
        var selected = queries[^1].Profile;
        await sut.SelectProfileCommand.ExecuteAsync(string.Empty);

        // Assert
        Assert.True(sut.ShowProfileFilter);
        Assert.Equal("photos", selected);
        Assert.Null(sut.ActiveProfile);
        Assert.Null(queries[^1].Profile);
    }

    [Fact]
    public async Task GivenStatusChanges_WhenRaised_ThenHeaderNotifiedUntilDisposed()
    {
        // Arrange
        var sut = CreateSut();
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // Act
        mockSync.Raise(x => x.StatusChanged += null, mockSync.Object, new SyncStatus());
        mockConnectivity.Raise(x => x.StatusChanged += null, mockConnectivity.Object, connectivity);
        var before = changed.Count(c => c == nameof(SyncViewModel.Status));
        sut.Dispose();
        mockSync.Raise(x => x.StatusChanged += null, mockSync.Object, new SyncStatus());
        mockLog.Raise(x => x.EventsWritten += null, mockLog.Object, EventArgs.Empty);
        await Task.Delay(20);

        // Assert
        Assert.Equal(2, before);
        Assert.Equal(2, changed.Count(c => c == nameof(SyncViewModel.Status)));
        Assert.Empty(queries);
    }

    [Fact]
    public async Task GivenLogFails_WhenInitialized_ThenPageStillLoads()
    {
        // Arrange
        mockLog.Setup(x => x.QueryAsync(It.IsAny<SyncEventQuery>())).ThrowsAsync(new InvalidOperationException());
        var sut = CreateSut();

        // Act
        await sut.OnInitializedAsync();

        // Assert
        Assert.False(sut.IsLoading);
        Assert.Empty(sut.Days);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(5);
        }
    }

    private void Add(SyncEvent entry)
    {
        entry.Owner = "bob";
        entry.Profile = string.IsNullOrEmpty(entry.Profile) ? "notes" : entry.Profile;
        events.Add(entry);
    }

    private void AddPass(string passId, DateTimeOffset at, params (SyncEventKind Kind, string Key)[] files)
    {
        Add(new SyncEvent { PassId = passId, Kind = SyncEventKind.PassStarted, Code = SyncEventCodes.Checking, Timestamp = at.AddSeconds(-2) });
        foreach (var (kind, key) in files)
        {
            Add(new SyncEvent { PassId = passId, Kind = kind, Key = key, Code = kind.ToString(), Timestamp = at.AddSeconds(-1) });
        }

        string Count(params SyncEventKind[] kinds) => files.Count(f => kinds.Contains(f.Kind)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var summary = new List<string>
        {
            Count(SyncEventKind.UploadNew, SyncEventKind.UploadLocalNewer),
            Count(SyncEventKind.DownloadNew, SyncEventKind.DownloadRemoteNewer),
            Count(SyncEventKind.DeleteLocal, SyncEventKind.DeleteRemote),
            Count(SyncEventKind.ConflictResolved),
            Count(SyncEventKind.Failed),
        };
        Add(new SyncEvent { PassId = passId, Kind = SyncEventKind.PassCompleted, Code = SyncEventCodes.Summary, Arguments = summary, Timestamp = at });
    }

    private SyncViewModel CreateSut() =>
        new(mockLog.Object, eventPresenter, new SyncStatusPresenter(testText.Sync, testText.Plurals, testText.Errors), mockSync.Object, mockConnectivity.Object, mockScheduler.Object, time, testText.Sync);
}
