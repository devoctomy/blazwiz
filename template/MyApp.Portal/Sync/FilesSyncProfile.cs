using Devoctomy.Blazwiz.Sync.Configuration;
using Devoctomy.Blazwiz.Sync.Models;

namespace MyApp.Portal.Sync;

/// <summary>
/// The app's files: synced with <c>files/</c> in the user's prefix, kept in IndexedDB, OPFS or a folder the user picks,
/// and encrypted when end to end encryption is on. App code gets them with
/// <c>[FromKeyedServices("files")] ISyncFileStore</c>. Found by <c>AddProfilesFromAssemblyContaining</c> in
/// <c>Program.cs</c>; add more profiles as more classes like this one.
/// </summary>
public sealed class FilesSyncProfile : SyncProfileDefinition
{
    /// <inheritdoc/>
    public override string Name => "files";

    /// <inheritdoc/>
    public override void Configure(SyncProfileBuilder profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        profile
            .UseIndexedDbIndexStore(database: "sync", store: "files_index")
            .UseFileStores(stores => stores
                .AddIndexedDb(database: "files", store: "files")
                .AddOpfs(root: "files")
                .AddLocalFolder()
                .UseDefault(SyncStorageKind.IndexedDb))
            .UseRemote("files/**/*")
            .UseEncryption();
    }
}
