using Devoctomy.Blazwiz.Sync.Configuration;
using Devoctomy.Blazwiz.Sync.Models;
using MyApp.Portal.Application.Services;

namespace MyApp.Portal.Application.Sync;

/// <summary>
/// The notes sync profile: plain markdown files kept in IndexedDB, OPFS or a folder the user picks, and synced with
/// <c>notes/</c> in the user's prefix, encrypted when end to end encryption is on. Found by
/// <c>AddProfilesFromAssemblyContaining</c> in <c>Program.cs</c>.
/// </summary>
public sealed class NotesSyncProfile : SyncProfileDefinition
{
    /// <inheritdoc/>
    public override string Name => NoteService.ProfileName;

    /// <inheritdoc/>
    public override void Configure(SyncProfileBuilder profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        profile
            .UseIndexedDbIndexStore(database: "sync", store: "notes_index")
            .UseFileStores(stores => stores
                .AddIndexedDb(database: IndexedDbNotesStateStore.Database, store: "files")
                .AddOpfs(root: "notes")
                .AddLocalFolder()
                .UseDefault(SyncStorageKind.IndexedDb))
            .UseRemote("notes/**/*.md", includeEmptyFolders: true)
            .UseEncryption();
    }
}
