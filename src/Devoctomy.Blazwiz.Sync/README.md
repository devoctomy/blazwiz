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

## Versions

Every `Devoctomy.Blazwiz.*` package and the `Devoctomy.Blazwiz.Templates` template are released together at one
version. Use the same version of all of them; an app made from the template sets it once, as `BlazwizVersion` in its
`Directory.Packages.props`. Before 1.0 any release may break. See [versioning](https://github.com/devoctomy/blazwiz/blob/main/docs/versioning.md) and
the [changelog](https://github.com/devoctomy/blazwiz/blob/main/CHANGELOG.md).

Part of [blazwiz](https://github.com/devoctomy/blazwiz): a framework for Blazor WebAssembly PWAs that sign in with AWS Cognito, sync files to S3 with end
to end encryption, and work offline.
