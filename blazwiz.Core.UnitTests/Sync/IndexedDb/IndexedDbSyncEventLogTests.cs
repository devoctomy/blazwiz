using blazwiz.Core.Sync.Configuration;
using blazwiz.Core.Sync.IndexedDb;
using blazwiz.Core.Sync.Interfaces;
using blazwiz.Core.Sync.Models;
using blazwiz.Core.Time.Interfaces;
using Moq;

namespace blazwiz.Core.UnitTests.Sync.IndexedDb;

public class IndexedDbSyncEventLogTests
{
    private const string Owner = "bob";
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly IndexedDbStore Store = IndexedDbSyncEventLog.DescribeStore("blazwiz_sync", "events");

    private readonly Mock<IIndexedDbClient> mockClient = new();
    private readonly Mock<ISyncUserContext> mockUser = new();
    private readonly Mock<ISystemClock> mockClock = new();
    private readonly SyncOptions options = new();
    private readonly List<SyncEvent> stored = [];

    public IndexedDbSyncEventLogTests()
    {
        mockUser.Setup(x => x.GetUserIdAsync()).ReturnsAsync(Owner);
        mockClock.SetupGet(x => x.UtcNow).Returns(Now);
        mockClient.Setup(x => x.GetAllByIndexAsync<SyncEvent>(Store, IndexedDbSyncEventLog.OwnerIndex, Owner, null))
            .ReturnsAsync(() => stored.Where(e => e.Owner == Owner).ToList());
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        Assert.Equal("client", Assert.Throws<ArgumentNullException>(() => new IndexedDbSyncEventLog(null!, mockUser.Object, mockClock.Object, options, Store)).ParamName);
        Assert.Equal("userContext", Assert.Throws<ArgumentNullException>(() => new IndexedDbSyncEventLog(mockClient.Object, null!, mockClock.Object, options, Store)).ParamName);
        Assert.Equal("systemClock", Assert.Throws<ArgumentNullException>(() => new IndexedDbSyncEventLog(mockClient.Object, mockUser.Object, null!, options, Store)).ParamName);
        Assert.Equal("options", Assert.Throws<ArgumentNullException>(() => new IndexedDbSyncEventLog(mockClient.Object, mockUser.Object, mockClock.Object, null!, Store)).ParamName);
        Assert.Equal("store", Assert.Throws<ArgumentNullException>(() => new IndexedDbSyncEventLog(mockClient.Object, mockUser.Object, mockClock.Object, options, null!)).ParamName);
    }

    [Fact]
    public void GivenDatabaseAndName_WhenDescribeStore_ThenIndexedByOwnerAndByOwnerProfileTime()
    {
        Assert.Equal(["id"], Store.KeyPath);
        Assert.Equal([IndexedDbSyncEventLog.OwnerIndex, IndexedDbSyncEventLog.OwnerProfileTimeIndex], Store.Indexes.Select(i => i.Name));
        Assert.Equal(["owner", "profile", "timestamp"], Store.Indexes[1].KeyPath);
    }

    [Fact]
    public async Task GivenEvents_WhenWriteAsync_ThenPutInOneTransactionAndEventsWrittenRaised()
    {
        // Arrange
        var sut = CreateSut();
        var raised = 0;
        sut.EventsWritten += (_, _) => raised++;
        var events = new[] { Event(SyncEventKind.PassStarted, Now), Event(SyncEventKind.PassCompleted, Now) };

        // Act
        await sut.WriteAsync(events);
        await sut.WriteAsync([]);

        // Assert
        mockClient.Verify(x => x.PutManyAsync(Store, It.Is<IEnumerable<SyncEvent>>(e => e.Count() == 2)), Times.Once);
        Assert.Equal(1, raised);
    }

    [Fact]
    public async Task GivenPreviousNoChangePass_WhenRecordNoChangePassAsync_ThenItIsCollapsedWithACount()
    {
        // Arrange
        var previous = Event(SyncEventKind.PassCompleted, Now.AddMinutes(-5));
        previous.IsNoChangePass = true;
        previous.Count = 2;
        SetupLast(previous);
        SyncEvent? saved = null;
        mockClient.Setup(x => x.PutAsync(Store, It.IsAny<SyncEvent>())).Callback<IndexedDbStore, SyncEvent>((_, e) => saved = e).Returns(Task.CompletedTask);

        // Act
        await CreateSut().RecordNoChangePassAsync(Owner, "notes", "pass2", Now);

        // Assert
        Assert.Equal(previous.Id, saved!.Id);
        Assert.Equal(3, saved.Count);
        Assert.Equal(Now, saved.Timestamp);
        Assert.Equal("pass2", saved.PassId);
    }

    [Fact]
    public async Task GivenPreviousPassChangedFiles_WhenRecordNoChangePassAsync_ThenNewEntryStarted()
    {
        // Arrange
        SetupLast(Event(SyncEventKind.PassCompleted, Now.AddMinutes(-5)));
        SyncEvent? saved = null;
        mockClient.Setup(x => x.PutAsync(Store, It.IsAny<SyncEvent>())).Callback<IndexedDbStore, SyncEvent>((_, e) => saved = e).Returns(Task.CompletedTask);
        var sut = CreateSut();
        var raised = false;
        sut.EventsWritten += (_, _) => raised = true;

        // Act
        await sut.RecordNoChangePassAsync(Owner, "notes", "pass", Now);

        // Assert
        Assert.Equal(1, saved!.Count);
        Assert.True(saved.IsNoChangePass);
        Assert.Equal(SyncEventKind.PassCompleted, saved.Kind);
        Assert.Equal(IndexedDbSyncEventLog.NoChangeMessage, saved.Message);
        Assert.True(raised);
    }

    [Fact]
    public async Task GivenEvents_WhenQueryAsync_ThenNewestFirstAndPaged()
    {
        // Arrange
        for (var i = 0; i < 5; i++)
        {
            stored.Add(Event(SyncEventKind.UploadNew, Now.AddMinutes(i), $"notes/{i}.md"));
        }

        stored.Add(new SyncEvent { Owner = "alice", Timestamp = Now.AddDays(1), Kind = SyncEventKind.UploadNew });

        // Act
        var first = await CreateSut().QueryAsync(new SyncEventQuery { Take = 2 });
        var last = await CreateSut().QueryAsync(new SyncEventQuery { Skip = 4, Take = 2 });

        // Assert
        Assert.Equal(["notes/4.md", "notes/3.md"], first.Events.Select(e => e.Key));
        Assert.True(first.HasMore);
        Assert.Equal(["notes/0.md"], last.Events.Select(e => e.Key));
        Assert.False(last.HasMore);
    }

    [Fact]
    public async Task GivenFilters_WhenQueryAsync_ThenOnlyMatchingEventsReturned()
    {
        // Arrange
        stored.Add(Event(SyncEventKind.UploadNew, Now, "notes/Ideas.md"));
        stored.Add(Event(SyncEventKind.DownloadNew, Now, "notes/Plan.md"));
        stored.Add(new SyncEvent { Owner = Owner, Profile = "photos", Timestamp = Now, Kind = SyncEventKind.UploadNew, Key = "photos/a.png", Message = "New local file" });
        stored.Add(new SyncEvent { Owner = Owner, Timestamp = Now.AddDays(-2), Kind = SyncEventKind.WentOffline, Message = "Offline: no credentials" });
        var sut = CreateSut();

        // Act & Assert
        Assert.Equal(["notes/Ideas.md"], (await sut.QueryAsync(new SyncEventQuery { Text = "ideas" })).Events.Select(e => e.Key));
        Assert.Single((await sut.QueryAsync(new SyncEventQuery { Text = "CREDENTIALS" })).Events);
        Assert.Single((await sut.QueryAsync(new SyncEventQuery { Text = "photos" })).Events);
        Assert.Equal(2, (await sut.QueryAsync(new SyncEventQuery { Kinds = [SyncEventKind.UploadNew] })).Events.Count);
        Assert.Single((await sut.QueryAsync(new SyncEventQuery { Profile = "photos" })).Events);
        Assert.Single((await sut.QueryAsync(new SyncEventQuery { To = Now.AddDays(-1) })).Events);
        Assert.Equal(3, (await sut.QueryAsync(new SyncEventQuery { From = Now.AddDays(-1) })).Events.Count);
    }

    [Fact]
    public async Task GivenSignedOut_WhenQueryAsync_ThenEmpty()
    {
        // Arrange
        mockUser.Setup(x => x.GetUserIdAsync()).ReturnsAsync((string?)null);

        // Act
        var result = await CreateSut().QueryAsync(new SyncEventQuery());

        // Assert
        Assert.Empty(result.Events);
    }

    [Fact]
    public async Task GivenEvents_WhenClearAsync_ThenOwnEventsDeleted()
    {
        // Arrange
        stored.Add(Event(SyncEventKind.UploadNew, Now));
        stored.Add(Event(SyncEventKind.UploadNew, Now));
        var sut = CreateSut();
        var raised = false;
        sut.EventsWritten += (_, _) => raised = true;

        // Act
        await sut.ClearAsync();

        // Assert
        mockClient.Verify(x => x.DeleteManyAsync(Store, It.Is<IEnumerable<object>>(k => k.Count() == 2)), Times.Once);
        Assert.True(raised);
    }

    [Fact]
    public async Task GivenOldAndExcessEvents_WhenPruneAsync_ThenOlderThanMaxAgeAndBeyondMaxCountDeleted()
    {
        // Arrange
        options.EventLogMaxAge = TimeSpan.FromDays(30);
        options.EventLogMaxEventsPerUser = 3;
        var expired = Event(SyncEventKind.UploadNew, Now.AddDays(-31));
        var keep = Enumerable.Range(0, 3).Select(i => Event(SyncEventKind.UploadNew, Now.AddMinutes(-i))).ToList();
        var excess = Event(SyncEventKind.UploadNew, Now.AddDays(-1));
        stored.AddRange([expired, .. keep, excess]);
        IEnumerable<object>? deleted = null;
        mockClient.Setup(x => x.DeleteManyAsync(Store, It.IsAny<IEnumerable<object>>())).Callback<IndexedDbStore, IEnumerable<object>>((_, k) => deleted = k.ToList()).Returns(Task.CompletedTask);

        // Act
        await CreateSut().PruneAsync();

        // Assert
        Assert.Equal(new object[] { excess.Id, expired.Id }, deleted);
    }

    [Fact]
    public async Task GivenNothingToPrune_WhenPruneAsync_ThenNothingDeleted()
    {
        // Arrange
        stored.Add(Event(SyncEventKind.UploadNew, Now));

        // Act
        await CreateSut().PruneAsync();

        // Assert
        mockClient.Verify(x => x.DeleteManyAsync(It.IsAny<IndexedDbStore>(), It.IsAny<IEnumerable<object>>()), Times.Never);
    }

    private static SyncEvent Event(SyncEventKind kind, DateTimeOffset timestamp, string? key = null) =>
        new() { Owner = Owner, Profile = "notes", Timestamp = timestamp, Kind = kind, Key = key, Message = kind.ToString() };

    private void SetupLast(SyncEvent last)
    {
        mockClient.Setup(x => x.GetLastByIndexAsync<SyncEvent>(
                Store,
                IndexedDbSyncEventLog.OwnerProfileTimeIndex,
                It.Is<object>(k => ((object[])k).SequenceEqual(new object[] { Owner, "notes", string.Empty })),
                It.Is<object>(k => ((object[])k).SequenceEqual(new object[] { Owner, "notes", "￿" }))))
            .ReturnsAsync(last);
    }

    private IndexedDbSyncEventLog CreateSut() => new(mockClient.Object, mockUser.Object, mockClock.Object, options, Store);
}
