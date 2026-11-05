using Devoctomy.Blazwiz.Sync.Configuration;
using Devoctomy.Blazwiz.Sync.Models;

namespace MyApp.Portal.Sync;

/// <summary>
/// The app's files: synced with <c>files/</c> in the user's prefix, kept in IndexedDB, OPFS or a folder the user picks,
/// and end to end encrypted with its own passphrase. App code gets them with
/// <c>[FromKeyedServices("files")] ISyncFileStore</c>. Found by <c>AddProfilesFromAssemblyContaining</c> in
/// <c>Program.cs</c>; add more profiles as more classes like this one.
/// </summary>
public sealed class FilesSyncProfile : SyncProfileDefinition
{
    /// <summary>
    /// The profile's id, which names its keys: fixed, so every sign in reaches the same keys.
    /// </summary>
    public const string KeyId = "7c9e2a4b-1f3d-4e8a-b6c5-2d9f0e1a3b57";

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
            .UseKeyId(KeyId);
    }
}
