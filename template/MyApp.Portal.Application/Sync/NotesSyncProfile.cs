using Devoctomy.Blazwiz.Sync.Configuration;
using Devoctomy.Blazwiz.Sync.Models;
using Devoctomy.Blazwiz.UI.Sync.Text;
using MyApp.Portal.Application.Services;

namespace MyApp.Portal.Application.Sync;

/// <summary>
/// The notes sync profile: plain markdown files kept in IndexedDB, OPFS or a folder the user picks, and synced with
/// <c>notes/</c> in the user's prefix, end to end encrypted with its own passphrase, which a user remembered with a
/// passkey may choose to unlock with biometrics on a device. Found by
/// <c>AddProfilesFromAssemblyContaining</c> in <c>Program.cs</c>.
/// </summary>
public sealed class NotesSyncProfile : SyncProfileDefinition
{
    /// <summary>
    /// The profile's id, which names its keys: fixed, so every sign in reaches the same keys.
    /// </summary>
    public const string KeyId = "3f2b8c1e-6d4a-4f7b-9c2e-8a1d5b7e9f40";

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
            .UseKeyId(KeyId)
            .UseProfileSettings(settings => settings.AllowDeviceUnlock = true)
            .UseDisplayText("Sync.Profile.Files", typeof(SyncUiText));
    }
}
