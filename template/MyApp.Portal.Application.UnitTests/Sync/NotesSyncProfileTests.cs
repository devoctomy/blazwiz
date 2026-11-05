using Devoctomy.Blazwiz.Sync.Configuration;
using Devoctomy.Blazwiz.Sync.Models;
using MyApp.Portal.Application.Services;
using MyApp.Portal.Application.Sync;

namespace MyApp.Portal.Application.UnitTests.Sync;

public class NotesSyncProfileTests
{
    [Fact]
    public void GivenTheApplicationAssembly_WhenScanned_ThenOnlyTheNotesProfileIsAdded()
    {
        // Arrange
        var options = new SyncOptions();
        var sut = new SyncBuilder(options);

        // Act
        sut.AddProfilesFromAssemblyContaining<NotesSyncProfile>();

        // Assert
        var profile = Assert.Single(options.Profiles);
        Assert.Equal(NoteService.ProfileName, profile.Name);
        Assert.Equal("notes/**/*.md", profile.RemotePattern);
        Assert.True(profile.IncludeEmptyFolders);
        Assert.True(profile.IsEncrypted);
        Assert.Equal("notes_index", profile.ResolvedIndexStoreName);
        Assert.Equal([SyncStorageKind.IndexedDb, SyncStorageKind.Opfs, SyncStorageKind.LocalFolder], profile.FileStores.Select(s => s.Kind));
        Assert.Equal(SyncStorageKind.IndexedDb, profile.ResolvedDefaultStorageKind);
    }

    [Fact]
    public void GivenNullBuilder_WhenConfigured_ThenThrowsArgumentNullException()
    {
        // Arrange
        var sut = new NotesSyncProfile();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => sut.Configure(null!));
    }
}
