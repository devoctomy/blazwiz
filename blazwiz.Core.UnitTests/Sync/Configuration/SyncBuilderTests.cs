using blazwiz.Core.Sync.Configuration;
using blazwiz.Core.Sync.InMemory;

namespace blazwiz.Core.UnitTests.Sync.Configuration;

public class SyncBuilderTests
{
    private readonly SyncOptions options = new();

    [Fact]
    public void GivenNoConfiguration_WhenOptionsCreated_ThenDefaultsMatchThePlan()
    {
        Assert.Equal(TimeSpan.FromSeconds(2), options.ChangeDebounce);
        Assert.Equal(TimeSpan.FromSeconds(30), options.ConnectivityTimings.CacheFor);
        Assert.Equal(TimeSpan.FromSeconds(5), options.ConnectivityTimings.ProbeTimeout);
        Assert.Equal([TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(5)], options.ConnectivityTimings.RecheckBackoff);
        Assert.Equal(TimeSpan.FromDays(30), options.EventLogMaxAge);
        Assert.Equal(5000, options.EventLogMaxEventsPerUser);
        Assert.Equal(SyncDefaults.IndexDatabase, options.EventLogDatabase);
        Assert.Equal("events", options.EventLogStore);
    }

    [Fact]
    public void GivenSettings_WhenConfigured_ThenOptionsUpdated()
    {
        // Act
        new SyncBuilder(options)
            .UseIndexedDbEventLog("db", "log")
            .UseEventLogRetention(TimeSpan.FromDays(7), 100)
            .UseChangeDebounce(TimeSpan.FromSeconds(5))
            .UseConnectivityTimings(t =>
            {
                t.CacheFor = TimeSpan.FromSeconds(10);
                t.ProbeTimeout = TimeSpan.FromSeconds(3);
                t.RecheckBackoff = [TimeSpan.FromSeconds(15)];
            });

        // Assert
        Assert.Equal("db", options.EventLogDatabase);
        Assert.Equal("log", options.EventLogStore);
        Assert.Equal(TimeSpan.FromDays(7), options.EventLogMaxAge);
        Assert.Equal(100, options.EventLogMaxEventsPerUser);
        Assert.Equal(TimeSpan.FromSeconds(5), options.ChangeDebounce);
        Assert.Equal(TimeSpan.FromSeconds(10), options.ConnectivityTimings.CacheFor);
        Assert.Equal(TimeSpan.FromSeconds(3), options.ConnectivityTimings.ProbeTimeout);
        Assert.Equal([TimeSpan.FromSeconds(15)], options.ConnectivityTimings.RecheckBackoff);
    }

    [Fact]
    public void GivenInvalidSettings_WhenConfigured_ThenThrows()
    {
        var sut = new SyncBuilder(options);
        Assert.ThrowsAny<ArgumentException>(() => sut.UseIndexedDbEventLog(" ", "log"));
        Assert.ThrowsAny<ArgumentException>(() => sut.UseEventLogRetention(TimeSpan.Zero, 1));
        Assert.ThrowsAny<ArgumentException>(() => sut.UseEventLogRetention(TimeSpan.FromDays(1), 0));
        Assert.ThrowsAny<ArgumentException>(() => sut.UseChangeDebounce(TimeSpan.FromSeconds(-1)));
        Assert.Throws<ArgumentException>(() => sut.UseConnectivityTimings(t => t.RecheckBackoff = []));
        Assert.Throws<ArgumentNullException>(() => sut.UseConnectivityTimings(null!));
        Assert.Throws<ArgumentNullException>(() => new SyncBuilder(null!));
    }

    [Fact]
    public void GivenCustomEventLog_WhenUseEventLog_ThenFactorySetAndIndexedDbSwitchesBack()
    {
        // Arrange
        var sut = new SyncBuilder(options);

        // Act
        sut.UseEventLog<FakeEventLog>();
        var custom = options.EventLogFactory;
        sut.UseIndexedDbEventLog("db", "log");

        // Assert
        Assert.NotNull(custom);
        Assert.Null(options.EventLogFactory);
    }

    [Fact]
    public void GivenProfile_WhenAddProfile_ThenAddedWithDefaultsFromItsName()
    {
        // Act
        new SyncBuilder(options).AddProfile("notes", p => p.UseRemote("/notes/**/*.md", includeEmptyFolders: true));

        // Assert
        var profile = Assert.Single(options.Profiles);
        Assert.Equal("notes", profile.Name);
        Assert.Equal("notes/**/*.md", profile.RemotePattern);
        Assert.True(profile.IncludeEmptyFolders);
        Assert.Equal(SyncDefaults.IndexDatabase, profile.IndexDatabase);
        Assert.Equal("notes_index", profile.ResolvedIndexStoreName);
        Assert.Equal(SyncDefaults.FileDatabase, profile.FileDatabase);
        Assert.Equal("notes", profile.ResolvedFileStoreName);
    }

    [Fact]
    public void GivenProfileWithoutRemote_WhenAddProfile_ThenThrows()
    {
        Assert.Throws<ArgumentException>(() => new SyncBuilder(options).AddProfile("notes", _ => { }));
    }

    [Fact]
    public void GivenDuplicateProfile_WhenAddProfile_ThenThrows()
    {
        var sut = new SyncBuilder(options).AddProfile("notes", p => p.UseRemote("notes/*.md"));
        Assert.Throws<ArgumentException>(() => sut.AddProfile("notes", p => p.UseRemote("notes/*.md")));
    }

    private sealed class FakeEventLog : blazwiz.Core.Sync.Interfaces.ISyncEventLog
    {
        public event EventHandler? EventsWritten;

        public Task WriteAsync(IEnumerable<blazwiz.Core.Sync.Models.SyncEvent> events)
        {
            EventsWritten?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task RecordNoChangePassAsync(string owner, string profile, string passId, DateTimeOffset timestamp) => Task.CompletedTask;

        public Task<blazwiz.Core.Sync.Models.SyncEventPage> QueryAsync(blazwiz.Core.Sync.Models.SyncEventQuery query) => Task.FromResult(new blazwiz.Core.Sync.Models.SyncEventPage());

        public Task ClearAsync() => Task.CompletedTask;

        public Task PruneAsync() => Task.CompletedTask;
    }
}
