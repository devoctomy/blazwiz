# Devoctomy.Blazwiz.Sync

Offline-first file sync for blazwiz apps. Files are kept on the device (IndexedDB, the origin private file system or a
local folder) and synced with the user's prefix in an S3 bucket, with optional end to end encryption. Changes made
offline are synced when the app is back online.

## Usage

Most apps use the sync module, `AddBlazwizSyncModule` in `Devoctomy.Blazwiz.UI.Sync`, which adds this engine with its
UI, Cognito credentials and encryption keys. It takes the same builder:

```csharp
builder.Services.AddBlazwizSyncModule(builder.Configuration, sync => sync
    .UseIndexedDbEventLog(database: "sync", store: "events")
    .AddProfile("files", profile => profile
        .UseIndexedDbIndexStore(database: "sync", store: "files_index")
        .UseFileStores(stores => stores
            .AddIndexedDb(database: "files", store: "files")
            .AddOpfs(root: "files")
            .AddLocalFolder()
            .UseDefault(SyncStorageKind.IndexedDb))
        .UseRemote("files/**/*")
        .UseEncryption()));
```

A profile is a set of files synced together: where they are kept on the device, which remote paths it syncs, and
whether they are encrypted. To use the engine without the module, call `AddBlazwizSync` and provide its services
yourself (the S3 target and the signed-in user).

### Profiles as classes

A profile can be a class deriving from `SyncProfileDefinition`. `AddProfilesFromAssemblyContaining<T>()` adds every
one in an assembly (public or not, with a parameterless constructor, in order of name), so adding a profile is adding
a class; `AddProfile<TProfile>()` adds one.

```csharp
public sealed class PhotosSyncProfile : SyncProfileDefinition
{
    public override string Name => "photos";

    public override void Configure(SyncProfileBuilder profile) => profile
        .UseRemote("photos/**/*")
        .UseEncryption();
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
        "Remote": "files/**/*",
        "Encrypted": true,
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

Each profile is keyed by its name, and needs `Remote`. Its files are kept either in one IndexedDB store (`FileStore`)
or in `Storages` the user can switch between (keyed `IndexedDb`, `Opfs` or `LocalFolder`). `ConnectivityTimings`
(`CacheFor`, `ProbeTimeout`, `RecheckBackoff`) can be set too. Profiles are added in order of name. An unknown key or
an invalid value stops the app at startup with an error naming the setting. Custom stores and logs (`UseFileStore<T>`,
`UseIndexStore<T>`, `UseEventLog<T>`) are set in code.

## Versions

Every `Devoctomy.Blazwiz.*` package and the `Devoctomy.Blazwiz.Templates` template are released together at one
version. Use the same version of all of them; an app made from the template sets it once, as `BlazwizVersion` in its
`Directory.Packages.props`. Before 1.0 any release may break. See [versioning](https://github.com/devoctomy/blazwiz/blob/main/docs/versioning.md) and
the [changelog](https://github.com/devoctomy/blazwiz/blob/main/CHANGELOG.md).

Part of [blazwiz](https://github.com/devoctomy/blazwiz): a framework for Blazor WebAssembly PWAs that sign in with AWS Cognito, sync files to S3 with end
to end encryption, and work offline.
