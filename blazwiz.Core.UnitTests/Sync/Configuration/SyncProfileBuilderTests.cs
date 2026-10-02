using blazwiz.Core.Sync.Configuration;
using blazwiz.Core.Sync.InMemory;
using blazwiz.Core.Sync.Models;

namespace blazwiz.Core.UnitTests.Sync.Configuration;

public class SyncProfileBuilderTests
{
    [Fact]
    public void GivenStores_WhenProfileBuilderConfigured_ThenIndexAndFileStoresSetIndependently()
    {
        // Arrange
        var profile = new SyncProfileOptions { Name = "notes" };
        var sut = new SyncProfileBuilder(profile);

        // Act
        sut.UseIndexedDbIndexStore("idx", "notes_idx").UseFileStore<InMemorySyncFileStore>();

        // Assert
        Assert.Equal("idx", profile.IndexDatabase);
        Assert.Equal("notes_idx", profile.ResolvedIndexStoreName);
        Assert.Null(profile.IndexStoreFactory);
        Assert.NotNull(profile.FileStoreFactory);

        // Act
        sut.UseIndexStore<InMemorySyncIndexStore>().UseIndexedDbFileStore("files", "notes_files");

        // Assert
        Assert.NotNull(profile.IndexStoreFactory);
        Assert.Null(profile.FileStoreFactory);
        Assert.Equal("files", profile.FileDatabase);
        Assert.Equal("notes_files", profile.ResolvedFileStoreName);
    }

    [Fact]
    public void GivenStorages_WhenUseFileStores_ThenSwitchableWithDefault()
    {
        // Arrange
        var profile = new SyncProfileOptions { Name = "notes" };
        var sut = new SyncProfileBuilder(profile);

        // Act
        sut.UseFileStore<InMemorySyncFileStore>()
            .UseFileStores(s => s.AddIndexedDb("files", "notes").AddOpfs("notes_root").UseDefault(SyncStorageKind.Opfs));

        // Assert
        Assert.True(profile.IsSwitchable);
        Assert.Null(profile.FileStoreFactory);
        Assert.Equal([SyncStorageKind.IndexedDb, SyncStorageKind.Opfs], profile.FileStores.Select(s => s.Kind));
        Assert.Equal(SyncStorageKind.Opfs, profile.ResolvedDefaultStorageKind);
    }

    [Fact]
    public void GivenNoDefault_WhenUseFileStores_ThenFirstAddedIsDefault()
    {
        // Arrange
        var profile = new SyncProfileOptions { Name = "notes" };

        // Act
        new SyncProfileBuilder(profile).UseFileStores(s => s.AddOpfs("root").AddIndexedDb("files", "notes"));

        // Assert
        Assert.Null(profile.DefaultStorageKind);
        Assert.Equal(SyncStorageKind.Opfs, profile.ResolvedDefaultStorageKind);
    }

    [Fact]
    public void GivenSwitchable_WhenSingleFileStoreUsedAfter_ThenNoLongerSwitchable()
    {
        // Arrange
        var profile = new SyncProfileOptions { Name = "notes" };
        var sut = new SyncProfileBuilder(profile);
        sut.UseFileStores(s => s.AddIndexedDb("files", "notes").AddOpfs("root").UseDefault(SyncStorageKind.Opfs));

        // Act
        sut.UseIndexedDbFileStore("files", "notes");

        // Assert
        Assert.False(profile.IsSwitchable);
        Assert.Null(profile.DefaultStorageKind);
    }

    [Fact]
    public void GivenInvalidStorages_WhenUseFileStores_ThenThrows()
    {
        var sut = new SyncProfileBuilder(new SyncProfileOptions { Name = "notes" });
        Assert.Throws<ArgumentNullException>(() => sut.UseFileStores(null!));
        Assert.Contains("at least two", Assert.Throws<ArgumentException>(() => sut.UseFileStores(s => s.AddOpfs("root"))).Message);
        Assert.Contains("already been added", Assert.Throws<ArgumentException>(() => sut.UseFileStores(s => s.AddOpfs("a").AddOpfs("b"))).Message);
        Assert.ThrowsAny<ArgumentException>(() => sut.UseFileStores(s => s.AddIndexedDb(" ", "notes")));
        Assert.ThrowsAny<ArgumentException>(() => sut.UseFileStores(s => s.AddOpfs(" ")));
        Assert.ThrowsAny<ArgumentException>(() => sut.UseFileStores(s => s.AddOpfs("root", metadataDatabase: " ")));
        Assert.Throws<ArgumentNullException>(() => new SyncFileStoresBuilder(null!));
    }

    [Fact]
    public void GivenInvalidProfileSettings_WhenConfigured_ThenThrows()
    {
        var sut = new SyncProfileBuilder(new SyncProfileOptions());
        Assert.ThrowsAny<ArgumentException>(() => sut.UseIndexedDbIndexStore(" ", "s"));
        Assert.ThrowsAny<ArgumentException>(() => sut.UseIndexedDbFileStore("d", " "));
        Assert.ThrowsAny<ArgumentException>(() => sut.UseRemote(" "));
        Assert.Throws<ArgumentNullException>(() => new SyncProfileBuilder(null!));
    }

    [Fact]
    public void GivenRemote_WhenUseRemote_ThenLeadingSlashTrimmedAndFoldersFlagSet()
    {
        var options = new SyncProfileOptions { Name = "notes" };
        new SyncProfileBuilder(options).UseRemote("/notes/*.md", includeEmptyFolders: true);

        Assert.Equal("notes/*.md", options.RemotePattern);
        Assert.True(options.IncludeEmptyFolders);
    }
}
