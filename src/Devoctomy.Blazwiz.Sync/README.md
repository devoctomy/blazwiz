# Devoctomy.Blazwiz.Sync

Offline-first file sync for blazwiz apps. Files are kept on the device (IndexedDB, the origin private file system or a
local folder) and synced with the user's prefix in an S3 bucket, end to end encrypted. Changes made
offline are synced when the app is back online.

## Usage

Most apps use the sync module, `AddBlazwizSyncModule` in `Devoctomy.Blazwiz.UI.Sync`, which adds this engine with its
UI, Cognito credentials and encryption keys. It takes the same builder:

```csharp
builder.Services.AddBlazwizSyncModule(builder.Configuration, sync => sync
    .UseIndexedDbEventLog(database: "sync", store: "events")
    .AddProfile("files", profile => profile
        .UseKeyId("3f2b8c1e-6d4a-4f7b-9c2e-8a1d5b7e9f40")
        .UseIndexedDbIndexStore(database: "sync", store: "files_index")
        .UseFileStores(stores => stores
            .AddIndexedDb(database: "files", store: "files")
            .AddOpfs(root: "files")
            .AddLocalFolder()
            .UseDefault(SyncStorageKind.IndexedDb))
        .UseRemote("files/**/*")));
```

A profile is a set of files synced together: where they are kept on the device and which remote paths it syncs. Every
profile is end to end encrypted with its own keys and its own passphrase, named by its id (`UseKeyId`, a GUID). Use a
fixed id to reach the same keys every time, or generate one. Until the passphrase is entered the profile is locked:
its files cannot be read or written, and it does not sync. `UseProfileSettings` sets when it locks itself again, whether
it can be unlocked with the passkey, its passphrase rules and Argon2id costs; `UseProfileDefaults` sets them for the
profiles users create while the app runs. To use the engine without the module, call `AddBlazwizSync` and provide its
services yourself (the S3 target, the signed-in user and the keys).

Each profile's keys document, and the index of the profiles, are kept in the user's prefix of the data bucket under
`keys/` (`S3UserKeysStore`), or in the browser's local storage when no data bucket is configured (local development).
Every write to them is conditional, so two devices changing them at once cannot both succeed: the second reads them
again and reapplies its change. No profile's remote pattern may reach `keys/` (or `profiles/`, where the profiles users
create are kept): `AddProfile` refuses it.

### Profiles as classes

A profile can be a class deriving from `SyncProfileDefinition`. `AddProfilesFromAssemblyContaining<T>()` adds every
one in an assembly (public or not, with a parameterless constructor, in order of name), so adding a profile is adding
a class; `AddProfile<TProfile>()` adds one.

```csharp
public sealed class PhotosSyncProfile : SyncProfileDefinition
{
    public override string Name => "photos";

    public override void Configure(SyncProfileBuilder profile) => profile
        .UseKeyId("7c9e2a4b-1f3d-4e8a-b6c5-2d9f0e1a3b57")
        .UseRemote("photos/**/*");
}
```

```csharp
builder.Services.AddBlazwizSyncModule(builder.Configuration, sync => sync
    .AddProfilesFromAssemblyContaining<Program>());
```

App code gets a profile's files by its name: `[FromKeyedServices("photos")] ISyncFileStore files`.

### From configuration

The same settings and profiles can come from configuration instead: a JSON file embedded in the app
(`UseEmbeddedConfiguration`), or any configuration section, such as `Sync` in `wwwroot/appsettings.json`
(`UseConfiguration(builder.Configuration.GetSection("Sync"))`). Code before and after can change or add to them.

```csharp
builder.Services.AddBlazwizSyncModule(builder.Configuration, sync => sync
    .UseEmbeddedConfiguration(typeof(Program).Assembly, "sync.json")
    .AddProfilesFromAssemblyContaining<Program>());
```

```xml
<EmbeddedResource Include="sync.json" LogicalName="sync.json" />
```

```json
{
  "Sync": {
    "ChangeDebounce": "00:00:02",
    "EventLog": { "Database": "sync", "Store": "events", "MaxAge": "30.00:00:00", "MaxEventsPerUser": 5000 },
    "Profiles": {
      "files": {
        "Id": "3f2b8c1e-6d4a-4f7b-9c2e-8a1d5b7e9f40",
        "Remote": "files/**/*",
        "IndexStore": { "Database": "sync", "Store": "files_index" },
        "Storages": {
          "IndexedDb": { "Database": "files", "Store": "files" },
          "Opfs": { "Root": "files" },
          "LocalFolder": {}
        },
        "DefaultStorage": "IndexedDb"
      }
    }
  }
}
```

Each profile is keyed by its name, and needs `Id` and `Remote`. Its files are kept either in one IndexedDB store (`FileStore`)
or in `Storages` the user can switch between (keyed `IndexedDb`, `Opfs` or `LocalFolder`). `ConnectivityTimings`
(`CacheFor`, `ProbeTimeout`, `RecheckBackoff`) can be set too. Profiles are added in order of name. An unknown key or
an invalid value stops the app at startup with an error naming the setting. Custom stores and logs (`UseFileStore<T>`,
`UseIndexStore<T>`, `UseEventLog<T>`) are set in code.

### What encryption does and does not hide

Each profile's files are encrypted with its own keys (AES-256-GCM, bound to the profile and the file's path), so
nobody with access to the bucket, the device's storage or a backup can read their content or move a file to another
path or profile without the passphrase. What stays visible to anyone with that access is the **metadata**: file names
and folder structure (the S3 keys), sizes, edit times (`x-amz-meta-modified-at`) and the sync log's keys and error
messages. Keep anything secret inside files, not in their names.

A new random nonce is used for every write. The file key is only replaced when the passphrase changes: there is no
rotation on a schedule or after a number of writes, so a profile whose passphrase never changes keeps one file key for
all its writes.

### Changing a passphrase

Changing a profile's passphrase locks out anyone who had the old one. `ISyncKeyRotationService` runs it:

1. `PrepareAsync(scope)` syncs the profile, so this device holds every file, then times encrypting and uploading one
   throwaway file to estimate how long the change will take. The file is random bytes under a random key, written to
   `scratch/` in the user's prefix (`ISyncScratchStore`, `SyncDefaults.ScratchFolder`) and deleted again. No profile
   may match `scratch/`, so sync never sees it. Offline, or if the sync fails, it throws `SyncKeyRotationException` and
   nothing has changed.
2. `RotateAsync(scope, current, new, progress)` rotates the keys (see `Devoctomy.Blazwiz.Encryption`), encrypts every
   file again on this device with the new key, then uploads them in one sync pass. Progress is reported as
   `E2eStep.ReencryptFiles` and `E2eStep.UploadFiles`, with counts.
3. The caller signs the user out on every device. While it does, `CompleteAsync()` clears the rotation mark of each
   profile in `ReadyToComplete`. The sync module's UI does this.

A change cut short is finished with `FinishAsync(scope, progress)`, on any device that unlocks the profile with the
new passphrase: it syncs, then encrypts again and uploads only the files not yet under the active key. Files encrypted
again but not uploaded are still marked to upload.

`ISyncService.SyncAsync` and `ISyncEngine.SyncAsync` take an optional `IProgress<SyncPassProgress>`, told how many of
a pass's planned changes are done.

When an unlocked profile meets a file under a key it does not hold, every registered `ISyncKeyMissingHandler` is told
before the read fails with `E2e.CannotDecrypt`. The sync module registers one that checks the session, so a device
that missed the sign out finds out.

Without a sync target (local development) nothing is synced or uploaded: the files on the device are encrypted again.

## Versions

Every `Devoctomy.Blazwiz.*` package and the `Devoctomy.Blazwiz.Templates` template are released together at one
version. Use the same version of all of them; an app made from the template sets it once, as `BlazwizVersion` in its
`Directory.Packages.props`. Before 1.0 any release may break. See [versioning](https://github.com/devoctomy/blazwiz/blob/main/docs/versioning.md) and
the [changelog](https://github.com/devoctomy/blazwiz/blob/main/CHANGELOG.md).

Part of [blazwiz](https://github.com/devoctomy/blazwiz): a framework for Blazor WebAssembly PWAs that sign in with AWS Cognito, sync files to S3 with end
to end encryption, and work offline.
