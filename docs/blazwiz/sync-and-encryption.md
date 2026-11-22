# Sync and end to end encryption

The sync module keeps the user's files on their device and in their own folder of an S3 bucket, encrypted so that
only they can read them. It works offline: changes made without a connection are synced when the app is back online.

This page covers setting it up, sync profiles, reading and writing files, passphrases and unlocking, the Argon2id
settings, and changing a passphrase. The template's notes example, at the end, puts it all together.

Packages: `Devoctomy.Blazwiz.UI.Sync` (the module: wiring and UI), `Devoctomy.Blazwiz.Sync` (the engine) and
`Devoctomy.Blazwiz.Encryption` (the keys).

## Words used on this page

- **Profile**: a set of files synced together. Each has a name, a place on the device, an S3 folder pattern and its
  own encryption keys.
- **Passphrase**: what the user enters to unlock a profile. Each profile has its own, separate from the sign-in
  password. **There is no recovery**: a forgotten passphrase loses the profile's files.
- **Locked / unlocked**: until its passphrase is entered on this device, a profile is locked. Its files can't be read
  or written, and it does not sync.
- **Argon2id**: the algorithm that turns a passphrase into a key. It is deliberately slow and memory-hungry, so
  guessing passphrases is expensive. Its settings (memory, iterations, parallelism) trade safety against speed.

## Setting it up

```csharp
builder.Services.AddBlazwizUi(builder.Configuration);
builder.Services.AddBlazwizCognito(builder.Configuration, allowPlaceholderSignIn: builder.HostEnvironment.IsDevelopment());
builder.Services.AddBlazwizSyncModule(builder.Configuration, sync => sync
    .UseEmbeddedConfiguration(typeof(Program).Assembly, "sync.json")
    .AddProfilesFromAssemblyContaining<Program>());
```

Call it after `AddBlazwizUi` and `AddBlazwizCognito`. It adds:

- the sync engine, end to end encryption, and their storage;
- the Sync page (`/sync`) and its sidebar item;
- the sync status pill, the lock indicator beside it, and banners;
- the Sync, End to end encryption and Argon2id benchmark sections of the Settings page;
- the "Start with sync turned off" sign-in option.

It reads two configuration sections:

```json
"AwsS3": {
  "Region": "eu-west-2",
  "DataBucketName": "..."
},
"Argon2id": {
  "MemoryKib": 19456
}
```

`AwsS3` is written by the deploy script. `Argon2id` is optional (see [Argon2id settings](#argon2id-settings)).

**Without a data bucket** (local development), nothing is synced: the files stay on the device, still encrypted, and
the keys are kept in the browser's local storage instead of the bucket.

## Sync profiles

A profile is best written as a class deriving from `SyncProfileDefinition`. `AddProfilesFromAssemblyContaining<T>()`
finds every such class in an assembly (public or not, with a parameterless constructor), so adding a profile is adding
a class. The template's, when created without the notes example, is `MyApp.Portal/Sync/FilesSyncProfile.cs`:

```csharp
public sealed class FilesSyncProfile : SyncProfileDefinition
{
    public const string KeyId = "7c9e2a4b-1f3d-4e8a-b6c5-2d9f0e1a3b57";

    public override string Name => "files";

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
            .UseKeyId(KeyId)
            .UseDisplayText("Sync.Profile.Files", typeof(SyncUiText));
    }
}
```

What each part does:

| Method | What it sets |
|---|---|
| `UseKeyId(id)` | **Required.** The profile's id, a GUID, which names its keys and passphrase. Use a fixed id, so every sign in reaches the same keys. Each profile needs its own. |
| `UseRemote(pattern, includeEmptyFolders)` | **Required.** Which files in the user's S3 folder belong to the profile, as a glob: `*` does not cross `/`, `**` does. `includeEmptyFolders: true` also syncs empty folders. |
| `UseFileStores(...)` | Where files are kept on the device, choosing from IndexedDB (`AddIndexedDb`), the origin private file system (`AddOpfs`) or a folder the user picks (`AddLocalFolder`). The user switches between them on the Settings page; `UseDefault` picks the first choice. |
| `UseIndexedDbFileStore(database, store)` | Instead of `UseFileStores`: always keep the files in one IndexedDB store. |
| `UseIndexedDbIndexStore(database, store)` | Where the sync index (what was last synced) is kept. |
| `UseDisplayText(key, resourceType)` | The name shown for the profile in Settings, from a text resource. Without it, the profile is shown by its name. |
| `UseProfileSettings(settings => ...)` | Its security settings (below). |

Database and store names are short names: the app prefix is added for you ([Browser storage](storage.md)).

Some rules, checked when the app starts:

- Two profiles may not share a name, an id, or files.
- No profile's remote pattern may reach `keys/` (the user's keys), `profiles/` (profiles users create) or `scratch/`
  (short-lived files used while changing a passphrase).

You can also add a profile inline, without a class:

```csharp
builder.Services.AddBlazwizSyncModule(builder.Configuration, sync => sync
    .AddProfile("photos", profile => profile
        .UseKeyId("...a GUID...")
        .UseRemote("photos/**/*")));
```

### Security settings

`UseProfileSettings` sets an `E2eProfileSettings` for the profile; `UseProfileDefaults` on the sync builder sets them
for the profiles users create while the app runs:

| Property | Default | What it does |
|---|---|---|
| `IdleTimeout` | 15 minutes | How long the profile stays unlocked without its files being read or written. Null keeps it unlocked until locked or signed out. |
| `AllowDeviceUnlock` | `false` | Whether the user may unlock it with their passkey (biometrics) on a device. |
| `MinimumPassphraseLength` | 12 | The shortest passphrase allowed. |
| `Argon2` | null (the app's) | The Argon2id settings for its passphrase key. |

```csharp
profile.UseProfileSettings(settings => settings.AllowDeviceUnlock = true);

sync.UseProfileDefaults(profile => profile.AllowDeviceUnlock = true);
```

### Settings from a file

The sync settings, and profiles too, can come from configuration: a JSON file embedded in the app
(`UseEmbeddedConfiguration(assembly, resourceName)`) or any configuration section
(`UseConfiguration(builder.Configuration.GetSection("Sync"))`). The template keeps its general settings in
`MyApp.Portal/sync.json`:

```json
{
  "Sync": {
    "ChangeDebounce": "00:00:02",
    "EventLog": {
      "Database": "sync",
      "Store": "events",
      "MaxAge": "30.00:00:00",
      "MaxEventsPerUser": 5000
    }
  }
}
```

To embed it, the portal's project file has `<EmbeddedResource Include="sync.json" LogicalName="sync.json" />`.

| Key | What it sets | In code |
|---|---|---|
| `ChangeDebounce` | How long after a local change a sync runs | `UseChangeDebounce` |
| `EventLog` | Where the sync log is kept (`Database`, `Store`), and how much is kept (`MaxAge`, `MaxEventsPerUser`) | `UseIndexedDbEventLog`, `UseEventLogRetention` |
| `ConnectivityTimings` | `CacheFor`, `ProbeTimeout`, `RecheckBackoff` | `UseConnectivityTimings` |
| `Profiles` | Profiles, keyed by name, each with `Id` and `Remote`, and optionally `IncludeEmptyFolders`, `IndexStore`, `FileStore`, or `Storages` (keyed `IndexedDb`, `Opfs`, `LocalFolder`) with `DefaultStorage` | `AddProfile` |

`UseMaxFileSize` (code only) sets the largest file a sync downloads; the default is 100 MiB. An unknown key or a bad
value stops the app at startup with an error naming the setting. The `Devoctomy.Blazwiz.Sync` package README has a
full example with profiles.

## Reading and writing files

Get a profile's files by its name, as a keyed service:

```csharp
public class ThingsService([FromKeyedServices("files")] ISyncFileStore files) : IThingsService
{
    public async Task SaveAsync(string name, string text)
    {
        await files.WriteAsync($"files/{name}.txt", Encoding.UTF8.GetBytes(text));
    }
}
```

Or ask the registry, when the profile is chosen at run time (as the notes example does for each notebook):

```csharp
var files = registry.GetFileStore(profileName);   // ISyncProfileRegistry
```

`ISyncFileStore` has:

| Method | What it does |
|---|---|
| `ListAsync()` | Lists the signed-in user's files and folder markers (`SyncFileInfo`: `Key`, `ModifiedAt`, `Size`, `IsFolder`). |
| `ReadAsync(key)` | Reads a file (`SyncFile`: `Key`, `Content`, `ModifiedAt`), or null if it doesn't exist. |
| `WriteAsync(key, content, modifiedAt)` | Adds or replaces a file. |
| `CreateFolderAsync(key)` | Adds a folder marker (a key ending in `/`), so an empty folder is kept. |
| `DeleteAsync(key)` | Deletes a file or folder marker. |

Keys are paths in the user's folder, and must match the profile's remote pattern: for a profile with
`UseRemote("files/**/*")`, write `files/Work/plan.txt`.

This store does the work for you: it encrypts on write and decrypts on read, keeps each user's files apart, and tracks
every change so a sync runs shortly after. You never touch the sync index.

While the profile is locked, reads and writes fail. Wrap the page that shows a profile's files in `E2eUnlockPanel`
(below), so the user is asked for the passphrase first.

### When files change from elsewhere

When a sync downloads or deletes files, `ISyncService` raises `FilesChanged`, with the profile and the keys that
changed (`SyncFilesChangedEventArgs`). A view model showing files subscribes, refreshes, and unsubscribes in
`Dispose`.

If the same file changed on two devices, the newer change wins (`NewestWinsConflictResolver`), and the sync log
records the conflict.

### Sync status

- `ISyncService.Status` (`SyncStatus`) has the state, when it last synced, the last error, and how many changes are
  waiting. `StatusChanged` is raised when it changes. The sync pill in the corner shows it.
- `IConnectivityService` says whether sync can reach the cloud (offline mode, sync turned off, no network, no
  credentials, S3 unreachable), and turns sync on or off (`SetSyncEnabledAsync`).
- The Sync page (`/sync`) shows the log.

## Passphrases and unlocking

Every profile is encrypted with its own keys, under its own passphrase. Encryption is AES-256-GCM, and each file is
bound to its profile and its path, so a file can't be read or moved elsewhere without the passphrase.

**What stays visible.** Anyone with access to the bucket, the device's storage or a backup can still see the
*metadata*: file names and folders (the S3 keys), sizes and edit times. Keep secrets inside files, not in their names.

### The first passphrase

A profile set up in code has no passphrase until the user sets one. Until then a banner (`E2eBanner`) says it needs
one, and the user sets it on the Settings page (End to end encryption) or in the unlock panel.

### The unlock panel

Wrap a page's content in `E2eUnlockPanel` with the profile's name:

```razor
<E2eUnlockPanel Profile="files">
    @* shown only while the profile is unlocked *@
</E2eUnlockPanel>
```

While the profile is unlocked, the panel shows its content. Otherwise, in its place, it asks for the passphrase (or
offers to set one, if there is none yet), and offers to unlock with biometrics where that is turned on. If a
passphrase change was cut short, it shows a warning and a button to finish it.

### Locking

Profiles lock themselves after `IdleTimeout` without use, and all lock when the user signs out. The lock indicator
beside the sync pill shows how many are unlocked, with Lock all. To lock or check one in code, use
`IE2eProtectedProfileManager` (`Lock(scope)`, `IsUnlocked(scope)`, and the `LockStateChanged` event). A profile's
`E2eKeyScope` (the id of its keys) is in `ISyncProtectedProfileService.Profiles` (`SyncProtectedProfile.Scope`).

### Profiles users create

Besides the profiles set up in code, users can create **protected profiles** of their own while the app runs, each
with its own passphrase. Each is named by a GUID, and its files are under `profiles/<id>/` in their folder. Use
`ISyncProtectedProfileService` (`Devoctomy.Blazwiz.Sync.Encryption.Interfaces`):

| Member | What it does |
|---|---|
| `Profiles`, `ProfilesChanged` | The user's profiles: those set up in code, then those they created (`IsCreatedByUser`). |
| `CreateAsync(passphrase, argon2)` | Creates a profile, unlocked, and returns its scope. |
| `SetUpAsync(scope, passphrase, argon2)` | Sets the first passphrase of a profile set up in code. |
| `DeleteAsync(scope)` | Deletes a profile: its keys first, so nothing left can be decrypted, then its files. While it runs, the profile has `IsDeleting`. |
| `SetLabelAsync(scope, label)` | Labels a profile on this device, so it is shown by name rather than id. The label is not synced. |
| `SetIdleTimeoutAsync(scope, timeout)` | Changes how long it stays unlocked without use, on every device. |
| `GetArgon2ParametersAsync(scope)` | The Argon2id settings a new passphrase would start from. |

Settings lists every profile, with buttons to set or change its passphrase, lock it, and delete the ones users
created.

### Unlocking with biometrics

If a profile's settings have `AllowDeviceUnlock`, a user remembered on this device with a passkey ("Remember me" at
sign in) can tick "Unlock with biometrics next time on this device" when unlocking it in the panel. Next time, the
passkey unlocks it.

To offer that from elsewhere, such as a list of profiles, inject `IE2eDeviceUnlockService`:

- `CanUnlockAsync(scope)`: whether biometrics can unlock it on this device. Show it on locked profiles, e.g. with a
  fingerprint icon.
- `UnlockAsync(scope)`: syncs the profile first when online, asks for the passkey, and unlocks it. It returns false if
  the passkey was cancelled or could not unlock it; open the profile's page anyway, and the panel asks for the
  passphrase.
- `ForgetAsync(scope)`: stops unlocking it with biometrics on this device. Ask the user to confirm first.

The passphrase always still works.

## Argon2id settings

The passphrase key is derived with Argon2id. Its settings:

| Setting | Default in a Release build | What it costs |
|---|---|---|
| `MemoryKib` | 524 288 (512 MiB) | Memory used for each derivation |
| `Iterations` | 2 | Passes over that memory |
| `Parallelism` | 1 | Lanes worked in parallel |
| `KeyLength` | 32 | The key's length in bytes (fixed) |

The packages are published as Release builds, so apps get these. (Only a Debug build of the framework itself, as in
the blazwiz repository, uses 19 MiB.)

The settings are chosen in three places, each overriding the one before:

1. **The app**, in the `Argon2id` configuration section. The template's `appsettings.Development.json` lowers the
   memory so passphrases are quick to enter while developing. Lowering it in production makes passphrases easier to
   guess.
2. **A profile**, with `E2eProfileSettings.Argon2`.
3. **The user**, for one passphrase, on the **Advanced** tab of any dialog that sets a passphrase. It starts at the
   settings in use. There is no upper limit, but changed settings are only accepted once they have been benchmarked on
   this device (so the user sees how long unlocking will take).

The settings are stored with each profile's keys, and read back whenever it is unlocked. Raising them later never
breaks existing data.

The Settings page also has an Argon2id benchmark panel, where users can try settings on their device.

## Your own passphrase dialog

To set a passphrase in a dialog of your own (for example, creating a profile with a name of your app's), use
`E2ePassphraseForm`. It has two tabs: the passphrase (entered twice, with the no-recovery acknowledgement) and Advanced
(the Argon2id settings and their benchmark). Your own fields go at the top of the first tab. See
[`E2ePassphraseForm`](components.md#e2epassphraseform) for the component, and
[A view model that owns another](views-and-view-models.md#a-view-model-that-owns-another) for wiring it to your view
model.

In the view model:

```csharp
await PassphraseForm.ResetAsync();                 // when the dialog opens: ResetAsync(scope, asksForCurrentPassphrase: true) when changing one
var error = PassphraseForm.Validate();             // null when it can be used
var scope = await profiles.CreateAsync(PassphraseForm.Passphrase, PassphraseForm.GetArgon2Parameters());
PassphraseForm.ClearPassphrases();                 // once used
```

Disable your confirm button while `PassphraseForm.CanConfirm` is false (changed Argon2id settings not yet
benchmarked).

## Changing a passphrase

Changing a profile's passphrase does more than re-wrap the old keys. It gives the profile **new keys**, encrypts every
file again, and signs the user out on every device. Anyone who had the old passphrase can read nothing written
afterwards.

What the user sees (in Settings, or your own page):

1. They enter the current and new passphrases. The profile is synced, and an upload is timed. Offline, the change is
   refused and nothing changes.
2. An estimate of how long it will take, with a warning that it can't be stopped and every device will be signed out.
   They can still cancel.
3. A progress bar while files are encrypted again and uploaded.
4. Every device, this one included, is signed out.

To offer it from a page of your own, render `E2eChangePassphraseDialog` while it should be open:

```razor
@if (ViewModel.IsChangingPassphrase)
{
    <E2eChangePassphraseDialog Profile="@ViewModel.ProfileName" OnClose="() => ViewModel.CloseChangePassphraseCommand.Execute(null)" />
}
```

If a change is cut short (the tab closed, the connection dropped), the profile can't be used until it is finished: the
unlock panel offers to finish it, on whichever device unlocks it next with the new passphrase.

Under the hood, `ISyncKeyRotationService` runs it: `PrepareAsync` (sync and estimate), `RotateAsync` (new keys,
encrypt again, upload), `FinishAsync` (finish one cut short) and `CompleteAsync` (clear the mark while signing out).

## Doing it in your own pages instead of Settings

If your app sets and changes passphrases on its own pages, take the framework's section off the Settings page:

```csharp
builder.Services.RemoveSettingsSection<E2eProfilesSection>();
```

The Sync and Argon2id benchmark sections (`SyncSettingsSection`, `Argon2idBenchmarkPanel`) can be removed the same
way.

## Worked example: the template's notes

The notes example (created with `--notes`, the default) is an app feature built on the sync module, not part of the
framework. It shows most of this page in use:

| What | Where (in the template) |
|---|---|
| A profile set up in code, the default notebook, with biometric unlock allowed | `MyApp.Portal.Application/Sync/NotesSyncProfile.cs` |
| Every other notebook is a protected profile the user creates, its name kept encrypted inside it | `Services/NotebookService.cs` (`CreateAsync`, `SetLabelAsync`) |
| A file service per notebook, over the profile's file store | `Services/NoteServiceFactory.cs` (`registry.GetFileStore(notebook.Name)`), `Services/NoteService.cs` |
| Its own IndexedDB record per user | `Services/IndexedDbNotesStateStore.cs` |
| A page wrapped in the unlock panel | `MyApp.Portal/Pages/Files.razor` (`<E2eUnlockPanel Profile="@ViewModel.Notebook.Name">`) |
| A new-notebook dialog built on the framework's passphrase form | `MyApp.Portal/Pages/Notebooks.razor`, `ViewModels/NotebooksViewModel.cs` |
| Biometric unlock from a list | `NotebooksViewModel` with `IE2eDeviceUnlockService` (through `INotebookService`) |
| A session lifecycle handler | `Services/NotebookLabelHandler.cs` |
| The sync module's wording changed from "files" to "notes" | `Text/NotesText.resx`, added with `AddBlazwizText(text => text.AddOverrides<NotesText>())` |
| A test that those overrides still match the framework | `MyApp.Portal.Application.UnitTests/Text/NotesTextTests.cs` |

Pages are `.md` files and folders are paths (`notes/Work/Ideas.md`), synced with `UseRemote("notes/**/*.md",
includeEmptyFolders: true)`.

## More detail

- The `Devoctomy.Blazwiz.Sync`, `Devoctomy.Blazwiz.UI.Sync` and `Devoctomy.Blazwiz.Encryption` package READMEs.
- [docs/diagrams/sync.md](../diagrams/sync.md) (how a sync runs) and
  [docs/diagrams/sync-framework.md](../diagrams/sync-framework.md) (setting up profiles).
