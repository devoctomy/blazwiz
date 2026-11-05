using System.Text;
using Devoctomy.Blazwiz.Sync.Configuration;
using Devoctomy.Blazwiz.Sync.Models;
using Microsoft.Extensions.Configuration;

namespace Devoctomy.Blazwiz.Sync.UnitTests.Configuration;

public class SyncConfigurationTests
{
    private const string FullJson = """
        {
          "Sync": {
            "ChangeDebounce": "00:00:05",
            "ConnectivityTimings": {
              "CacheFor": "00:00:10",
              "ProbeTimeout": "00:00:03",
              "RecheckBackoff": [ "00:00:15", "00:01:00" ]
            },
            "EventLog": { "Database": "db", "Store": "log", "MaxAge": "7.00:00:00", "MaxEventsPerUser": 100 },
            "Profiles": {
              "photos": {
                "Id": "bafcbb1a-0eff-4267-ad1a-e7b94a1b4ead", "Remote": "/photos/**/*",
                "IndexStore": { "Database": "sync", "Store": "photos_index" },
                "Storages": {
                  "LocalFolder": {},
                  "Opfs": { "Root": "photos", "MetadataStore": "photos_meta" },
                  "IndexedDb": { "Database": "files", "Store": "photos" }
                },
                "DefaultStorage": "opfs"
              },
              "documents": {
                "Id": "f5f28a29-0b40-4e6d-be72-2da274499a47", "Remote": "documents/**/*.md",
                "IncludeEmptyFolders": true,
                "FileStore": { "Database": "files", "Store": "documents" }
              }
            }
          }
        }
        """;

    private readonly SyncOptions options = new();

    [Fact]
    public void GivenAFullSection_WhenApplied_ThenEverySettingAndProfileIsSet()
    {
        // Arrange
        var sut = new SyncBuilder(options);

        // Act
        var result = sut.UseConfiguration(Section(FullJson));

        // Assert
        Assert.Same(sut, result);
        Assert.Equal(TimeSpan.FromSeconds(5), options.ChangeDebounce);
        Assert.Equal(TimeSpan.FromSeconds(10), options.ConnectivityTimings.CacheFor);
        Assert.Equal(TimeSpan.FromSeconds(3), options.ConnectivityTimings.ProbeTimeout);
        Assert.Equal([TimeSpan.FromSeconds(15), TimeSpan.FromMinutes(1)], options.ConnectivityTimings.RecheckBackoff);
        Assert.Equal("db", options.EventLogDatabase);
        Assert.Equal("log", options.EventLogStore);
        Assert.Equal(TimeSpan.FromDays(7), options.EventLogMaxAge);
        Assert.Equal(100, options.EventLogMaxEventsPerUser);
        Assert.Equal(["documents", "photos"], options.Profiles.Select(p => p.Name));

        var documents = options.Profiles[0];
        Assert.Equal("documents/**/*.md", documents.RemotePattern);
        Assert.True(documents.IncludeEmptyFolders);
        Assert.NotNull(documents.KeyScope);
        Assert.False(documents.IsSwitchable);
        Assert.Equal("files", documents.FileDatabase);
        Assert.Equal("documents", documents.ResolvedFileStoreName);
        Assert.Equal("documents_index", documents.ResolvedIndexStoreName);

        var photos = options.Profiles[1];
        Assert.Equal("photos/**/*", photos.RemotePattern);
        Assert.False(photos.IncludeEmptyFolders);
        Assert.NotNull(photos.KeyScope);
        Assert.Equal("sync", photos.IndexDatabase);
        Assert.Equal("photos_index", photos.ResolvedIndexStoreName);
        Assert.Equal([SyncStorageKind.IndexedDb, SyncStorageKind.Opfs, SyncStorageKind.LocalFolder], photos.FileStores.Select(s => s.Kind));
        Assert.Equal(SyncStorageKind.Opfs, photos.ResolvedDefaultStorageKind);
    }

    [Fact]
    public void GivenStoragesWithoutADefault_WhenApplied_ThenTheFirstInKindOrderIsTheDefault()
    {
        // Arrange
        var sut = new SyncBuilder(options);

        // Act
        sut.UseConfiguration(Section("""
            { "Sync": { "Profiles": { "files": {
              "Id": "d05ceee5-17fc-48b9-abd0-6c0d60eb550c", "Remote": "files/**/*",
              "Storages": { "LocalFolder": {}, "Opfs": { "Root": "files" } }
            } } } }
            """));

        // Assert
        var profile = Assert.Single(options.Profiles);
        Assert.Equal([SyncStorageKind.Opfs, SyncStorageKind.LocalFolder], profile.FileStores.Select(s => s.Kind));
        Assert.Equal(SyncStorageKind.Opfs, profile.ResolvedDefaultStorageKind);
    }

    [Fact]
    public void GivenAMissingSection_WhenApplied_ThenNothingChanges()
    {
        // Arrange
        var sut = new SyncBuilder(options);
        var configuration = new ConfigurationBuilder().Build();

        // Act
        sut.UseConfiguration(configuration.GetSection(SyncDefaults.ConfigurationSection));

        // Assert
        Assert.Empty(options.Profiles);
        Assert.Equal(TimeSpan.FromSeconds(2), options.ChangeDebounce);
        Assert.Equal("events", options.EventLogStore);
    }

    [Fact]
    public void GivenOnlyRetention_WhenApplied_ThenTheOtherEventLogSettingsAreKept()
    {
        // Arrange
        var sut = new SyncBuilder(options).UseIndexedDbEventLog("db", "log").UseEventLogRetention(TimeSpan.FromDays(3), 10);

        // Act
        sut.UseConfiguration(Section("""{ "Sync": { "EventLog": { "MaxEventsPerUser": 20 } } }"""));

        // Assert
        Assert.Equal("db", options.EventLogDatabase);
        Assert.Equal("log", options.EventLogStore);
        Assert.Equal(TimeSpan.FromDays(3), options.EventLogMaxAge);
        Assert.Equal(20, options.EventLogMaxEventsPerUser);
    }

    [Fact]
    public void GivenProfilesInCodeAndConfiguration_WhenApplied_ThenBothAreAdded()
    {
        // Arrange
        var sut = new SyncBuilder(options).AddProfile("notes", p => p.UseKeyId(Guid.NewGuid().ToString("D")).UseRemote("notes/**/*.md"));

        // Act
        sut.UseConfiguration(Section("""{ "Sync": { "Profiles": { "photos": { "Id": "38811a35-3ae0-4cf5-b935-e2e9f3b7180f", "Remote": "photos/**/*" } } } }"""));

        // Assert
        Assert.Equal(["notes", "photos"], options.Profiles.Select(p => p.Name));
    }

    [Fact]
    public void GivenAProfileAlreadyAddedInCode_WhenApplied_ThenThrowsNamingTheSetting()
    {
        // Arrange
        var sut = new SyncBuilder(options).AddProfile("photos", p => p.UseKeyId(Guid.NewGuid().ToString("D")).UseRemote("photos/**/*"));

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => sut.UseConfiguration(Section("""{ "Sync": { "Profiles": { "photos": { "Id": "bd313a4d-a720-490c-8976-29ff3517c422", "Remote": "photos/**/*" } } } }""")));
        Assert.Contains("Sync:Profiles:photos", ex.Message);
        Assert.IsAssignableFrom<ArgumentException>(ex.InnerException);
    }

    [Theory]
    [InlineData("""{ "Sync": { "Debounce": "00:00:01" } }""", "Sync:Debounce")]
    [InlineData("""{ "Sync": { "EventLog": { "Retention": 1 } } }""", "Sync:EventLog:Retention")]
    [InlineData("""{ "Sync": { "Profiles": { "photos": { "Id": "09ba271d-c930-4470-8f0b-ea8c95ebcf23", "Remote": "photos/**/*", "Encrypt": true } } } }""", "Sync:Profiles:photos:Encrypt")]
    [InlineData("""{ "Sync": { "Profiles": { "photos": { "Id": "72f7b075-c86d-4dc8-8876-833f08561c9d", "Remote": "photos/**/*", "Storages": { "S3": {}, "Opfs": { "Root": "p" } } } } } }""", "Sync:Profiles:photos:Storages:S3")]
    [InlineData("""{ "Sync": { "Profiles": { "photos": { "Id": "3ce890bc-88e4-46d4-897d-433c44247b03", "Remote": "photos/**/*", "Storages": { "Opfs": { "Root": "p", "Database": "x" }, "LocalFolder": {} } } } } }""", "Sync:Profiles:photos:Storages:Opfs:Database")]
    [InlineData("""{ "Sync": { "Profiles": { "photos": { } } } }""", "Sync:Profiles:photos:Remote")]
    [InlineData("""{ "Sync": { "Profiles": { "photos": { "Id": "not a guid", "Remote": "photos/**/*" } } } }""", "Sync:Profiles:photos")]
    [InlineData("""{ "Sync": { "Profiles": { "photos": { "Remote": "photos/**/*" } } } }""", "Sync:Profiles:photos:Id")]
    [InlineData("""{ "Sync": { "ChangeDebounce": "soon" } }""", "Sync:ChangeDebounce")]
    [InlineData("""{ "Sync": { "EventLog": { "MaxEventsPerUser": "lots" } } }""", "Sync:EventLog:MaxEventsPerUser")]
    [InlineData("""{ "Sync": { "ChangeDebounce": "-00:00:01" } }""", "Sync")]
    [InlineData("""{ "Sync": { "Profiles": { "photos": { "Id": "739ae05d-522b-48d3-9ec6-732ea43562db", "Remote": "photos/**/*", "IndexStore": { "Database": "sync" } } } } }""", "Sync:Profiles:photos:IndexStore:Store")]
    [InlineData("""{ "Sync": { "Profiles": { "photos": { "Id": "376b3ae0-fe13-4070-a61c-9d3d8c2e1a10", "Remote": "photos/**/*", "Storages": { "Opfs": {}, "LocalFolder": {} } } } } }""", "Sync:Profiles:photos:Storages:Opfs:Root")]
    [InlineData("""{ "Sync": { "Profiles": { "photos": { "Id": "abaaef7b-a08f-4e8a-ac7f-45b14317cc7d", "Remote": "photos/**/*", "Storages": { "Opfs": { "Root": "p" } } } } } }""", "Sync:Profiles:photos")]
    [InlineData("""{ "Sync": { "Profiles": { "photos": { "Id": "5fb7810d-7b97-4c50-8565-f04df2a605c4", "Remote": "photos/**/*", "Storages": { "Opfs": { "Root": "p" }, "LocalFolder": {} }, "DefaultStorage": "IndexedDb" } } } }""", "Sync:Profiles:photos")]
    [InlineData("""{ "Sync": { "Profiles": { "photos": { "Id": "fc63e51b-1133-4f92-b0a7-04e9e67db449", "Remote": "photos/**/*", "Storages": { "Opfs": { "Root": "p" }, "LocalFolder": {} }, "DefaultStorage": "Cloud" } } } }""", "Sync:Profiles:photos:DefaultStorage")]
    [InlineData("""{ "Sync": { "Profiles": { "photos": { "Id": "2d642f4d-b0ec-4ff2-b1ad-e33e5d6fb50b", "Remote": "photos/**/*", "DefaultStorage": "Opfs" } } } }""", "Sync:Profiles:photos")]
    [InlineData("""{ "Sync": { "Profiles": { "photos": { "Id": "b869d587-d37c-4442-8037-27294e8335f4", "Remote": "photos/**/*", "FileStore": { "Database": "f", "Store": "p" }, "Storages": { "Opfs": { "Root": "p" }, "LocalFolder": {} } } } } }""", "Sync:Profiles:photos")]
    public void GivenAnInvalidSetting_WhenApplied_ThenThrowsNamingIt(string json, string path)
    {
        // Arrange
        var sut = new SyncBuilder(options);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => sut.UseConfiguration(Section(json)));
        Assert.Contains(path, ex.Message);
    }

    [Fact]
    public void GivenAnEmbeddedFile_WhenApplied_ThenItsSyncSectionIsUsed()
    {
        // Arrange
        var sut = new SyncBuilder(options);

        // Act
        var result = sut.UseEmbeddedConfiguration(typeof(SyncConfigurationTests).Assembly, "sync.test.json");

        // Assert
        Assert.Same(sut, result);
        Assert.Equal(TimeSpan.FromSeconds(3), options.ChangeDebounce);
        var profile = Assert.Single(options.Profiles);
        Assert.Equal("files", profile.Name);
        Assert.Equal("files/**/*", profile.RemotePattern);
        Assert.NotNull(profile.KeyScope);
        Assert.Equal([SyncStorageKind.IndexedDb, SyncStorageKind.Opfs, SyncStorageKind.LocalFolder], profile.FileStores.Select(s => s.Kind));
    }

    [Fact]
    public void GivenAMissingOrInvalidEmbeddedFile_WhenApplied_ThenThrows()
    {
        // Arrange
        var sut = new SyncBuilder(options);
        var assembly = typeof(SyncConfigurationTests).Assembly;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => sut.UseEmbeddedConfiguration(null!, "sync.json"));
        Assert.ThrowsAny<ArgumentException>(() => sut.UseEmbeddedConfiguration(assembly, " "));
        Assert.Contains("missing.json", Assert.Throws<InvalidOperationException>(() => sut.UseEmbeddedConfiguration(assembly, "missing.json")).Message);
        Assert.Contains("'Sync'", Assert.Throws<InvalidOperationException>(() => sut.UseEmbeddedConfiguration(assembly, "sync.nosection.json")).Message);
        Assert.Contains("not valid JSON", Assert.Throws<InvalidOperationException>(() => sut.UseEmbeddedConfiguration(assembly, "sync.invalid.json")).Message);
    }

    [Fact]
    public void GivenNullSection_WhenApplied_ThenThrowsArgumentNullException()
    {
        // Arrange
        var sut = new SyncBuilder(options);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => sut.UseConfiguration(null!));
    }

    private static IConfigurationSection Section(string json)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return new ConfigurationBuilder().AddJsonStream(stream).Build().GetSection(SyncDefaults.ConfigurationSection);
    }
}
