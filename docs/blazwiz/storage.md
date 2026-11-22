# Browser storage

A blazwiz app keeps its data in the browser: settings in local storage, files and records in IndexedDB (the browser's
built-in database), and, with the sync module, files in the origin private file system (OPFS) or a folder the user
picks. This page explains how names work and which service to use.

## Short names and the app prefix

Several apps can run on the same domain (for example, while you develop more than one on `localhost`). To keep them
apart, everything an app keeps in the browser is named after the app.

In code you always use a **short name**, such as `settings`. The storage services add the app's **prefix**, made from
`AppIdentity.Name` (set with `AddBlazwizApp`):

| You write | Stored as (app named `myapp`) |
|---|---|
| `settings` | `myapp_settings` |
| an IndexedDB database `notes` | `myapp_notes` |

**Never write the prefix yourself**, and never write a literal like `"myapp_session"`. If the app is renamed, or a
second app is made from the same code, literal names would clash or be lost.

The prefix comes from `IAppStorageKeys` (`Devoctomy.Blazwiz.Core.Configuration.Interfaces`): `Prefix`, and
`For(name)` which turns a short name into the stored one. You rarely need it directly, because the storage services
below apply it. The one place the app writes it by hand is `index.html`, whose `data-app-name` attribute must match
`AppIdentity.Name`, so the script there can read the saved theme before Blazor starts.

## Local storage

For small values, inject `ILocalStorageService` (`Devoctomy.Blazwiz.Browser.Services.Interfaces`):

```csharp
await localStorage.SetObjectAsync("recent_things", recentThings);
var recent = await localStorage.GetObjectAsync<List<string>>("recent_things");

await localStorage.SetItemAsync("last_tab", "details");
var lastTab = await localStorage.GetItemAsync("last_tab");

await localStorage.RemoveItemAsync("last_tab");
```

`SetObjectAsync` and `GetObjectAsync` store objects as JSON. Local storage is per device and per browser, and is not
synced.

## IndexedDB

For records, inject `IIndexedDbClient` (`Devoctomy.Blazwiz.Sync.Interfaces`). It comes from the sync package and is
registered by the sync module, so it is there in apps created with `--sync`. Describe your
store with `IndexedDbStore`: the database, the store's name and its key path. The database name is a short name: the
client adds the prefix.

The template's notes example keeps a small record per user this way, in `IndexedDbNotesStateStore`:

```csharp
public class IndexedDbNotesStateStore(IIndexedDbClient client) : INotesStateStore
{
    public const string Database = "notes";

    public static readonly IndexedDbStore Store = new IndexedDbStore(Database, "state", ["owner"]);

    private readonly IIndexedDbClient client = client ?? throw new ArgumentNullException(nameof(client));

    public Task<NotesState?> GetAsync(string owner) => client.GetAsync<NotesState>(Store, owner);

    public Task SaveAsync(NotesState state) => client.PutAsync(Store, state);
}
```

`IIndexedDbClient` also has `PutManyAsync`, `DeleteManyAsync`, `GetAllByIndexAsync` and `GetLastByIndexAsync`.

Notice the store is keyed by owner (the signed-in user), so two people signing in on the same device don't see each
other's records. Do the same with your own data.

## Files that sync

For files the user should have on all their devices, use a sync profile's file store (`ISyncFileStore`), not
IndexedDB directly. It stores the files on the device, encrypts them, and tracks every change for sync. See
[Sync and end to end encryption](sync-and-encryption.md#reading-and-writing-files).

## Per-user settings

The framework keeps the user's appearance choices (`UserSettings`: `Theme` and `SidebarCollapsed`) through
`IUserSettingsService`, in local storage under `user_settings`. If your app needs its own settings, keep them under a
name of your own, in the same way.

## What the framework keeps

For reference, here is what the framework stores, by short name. Your own names must not clash with these.

| Short name | What |
|---|---|
| `cognito_session` | The signed-in session (tokens) |
| `saved_users` | Users remembered on this device, with their passkey-encrypted passwords |
| `user_settings` | Theme and sidebar |
| `culture` | The language the user chose |
| `device_name` | This device's name |
| `data_versions` | Which data migrations have run |
| `sync_disabled` | Whether sync is turned off on this device |
| `e2e_keys_<profile>_<user>`, `e2e_index_<user>`, `e2e_labels_<user>` | This device's copies of the user's encryption keys documents, their index, and profile labels |

Kept only in memory, and so lost on a reload: the refresh token, offline mode and its session, every key derived from
a passkey, the keys of unlocked sync profiles, and AWS credentials.

[docs/diagrams/session-management.md](../diagrams/session-management.md) has the full picture.
