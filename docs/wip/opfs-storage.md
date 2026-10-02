# Switchable file storage: IndexedDB, OPFS and a local folder

> Status: implemented on `s3-sync`. Replaces `opfs-offline-storage.md`. See [As built](#as-built) for where the code
> differs from the plan below, and [Local folder](#local-folder-file-system-access-api) for the third storage.

## As built

- **Storage state lives in IndexedDB, not localStorage.** One `SyncStorageState` per profile and user in
  `blazwiz_sync/storage` (`IndexedDbSyncStorageStateStore`). It holds the storage in use and the move in progress
  (from, to and phase), so the selection and the recovery record are one row. It is read on every file call and
  never cached, so a move made in another tab is seen straight away.
- **No keyed backend services.** `SyncStorageResolver` builds each storage's file store from the factories added with
  `UseFileStores`, and keeps them for the scope. The untracked store of a switchable profile is a
  `SwitchableSyncFileStore`.
- **Changes are refused while files are copying.** `SwitchableSyncFileStore` throws `SyncStorageMigrationException`
  for writes and deletes while the phase is `Copying`, so nothing written in another tab is left behind. Reads still
  work. Other tabs also show a blocking overlay (`Migrating`), then reload (`Changed`) through `SyncStorageChannel`.
- **Copies are checked byte for byte,** by reading each file back from the target, rather than by hash.
- **No single-backend `UseOpfsFileStore`.** OPFS is only offered through `UseFileStores(s => s.AddOpfs(root))`.
- **Recovery runs before sync starts:** `MainLayoutViewModel` calls `RecoverAsync` before `ISyncScheduler.StartAsync`.
  Recovery takes the profile's sync lock and reads the state again, so it never undoes a move another tab is making.
- **The notes OPFS root is `blazwiz_notes`.** Modified times are kept in `blazwiz_sync/notes_opfs`.

## Goal

Let the user choose where synced files are kept on this device: **IndexedDB** (current) or **OPFS**. The choice lives in
the Sync panel in Settings. Switching pauses sync, migrates existing files, switches the store and resumes sync.

## A correction: OPFS has no folder picker

OPFS (`navigator.storage.getDirectory()`) is a private sandbox for each origin. It shows **no dialog**, needs **no
permission** and works in every modern browser, including Safari and Firefox. Its files are not visible in Explorer or
Finder.

The folder dialog belongs to a different API, the **File System Access API** (`showDirectoryPicker`). It reads and writes
a real folder the user can see. That API:

- works only in Chromium (Chrome and Edge, desktop only). It doesn't work in Firefox, Safari or on mobile.
- returns a `FileSystemDirectoryHandle`, which can be stored in **IndexedDB** (not localStorage) and reused.
- needs a permission check on each visit (`queryPermission`, then `requestPermission` after a user gesture). Chrome
  offers "Allow on every visit", which in practice sticks reliably for installed PWAs. That matches the limitation you
  were happy to accept.

**Proposal:** build OPFS now as asked, with no prompt. The design below makes a third "Local folder" backend a small
addition later (see [Follow up](#follow-up-local-folder-backend)). If what you actually wanted was the visible folder,
say so and I'll swap the backend. The switching and migration work stays the same either way.

## Design

### 1. OPFS file store (Core)

- `blazwiz.Core/wwwroot/js/opfs.js` wraps OPFS: `isSupported`, `list`, `read`, `write`, `delete` and `clear` for an owner.
- `Sync/Opfs/OpfsSyncFileStore : ISyncFileStore` calls the module through `IOpfsClient` / `OpfsClient` (an injectable
  wrapper, as `IndexedDbClient` is).
- Layout: `/<root>/<owner>/<key path>`. For example, `notes/Work/Ideas.md` is stored as nested directories plus a file.
  - A folder marker (key ends `/`) is stored as a directory containing a `.folder` marker file. This tells an
    explicit empty folder apart from an implied one.
  - Path segments that OPFS rejects, and the reserved `.folder` name, are percent-encoded.
- `ModifiedAt`: OPFS can't set `lastModified`, so the store keeps a small IndexedDB sidecar store
  (`owner, key -> modifiedAt, size`). `ListAsync` walks the OPFS tree and fills in `ModifiedAt` from the sidecar. If a
  sidecar entry is missing, it falls back to `File.lastModified`. Writes go to the file first, then the sidecar, the same
  order as `TrackingSyncFileStore`.
- Writes use `createWritable()` on the main thread. If that's missing (older Safari), the module falls back to a
  worker that uses `createSyncAccessHandle()`. `byte[]` already passes over interop as binary.
- `QuotaExceededError` is mapped to a typed exception and shown as a clear error.
- Builder: `SyncProfileBuilder.UseOpfsFileStore(root)`.

### 2. Switchable file store (Core)

Each profile's untracked store becomes a router:

- `ISyncStorageSelection`: `GetAsync(profile)` returns the selected `SyncStorageKind` (`IndexedDb`, `Opfs`), and
  there is a matching `SetAsync`. The default implementation keeps the choice in localStorage, keyed per user and
  profile, so it applies to this device only. The setting is not synced.
- `SwitchableSyncFileStore : ISyncFileStore` resolves the active backend for each call from the selection and the keyed
  backends (`<profile>:indexeddb`, `<profile>:opfs`).
- Builder: `profile.UseFileStores(s => s.AddIndexedDb(db, store).AddOpfs(root).Default(SyncStorageKind.IndexedDb))`.
  `UseIndexedDbFileStore` and `UseFileStore<T>` keep working, with a single backend and no switching.
- `TrackingSyncFileStore`, `SyncProfile` and the engine are unchanged. They still see a single `ISyncFileStore`.
- The **index stays in IndexedDB** whatever the file storage. Migration copies the content byte for byte, so hashes and
  ETags stay valid and **nothing is re-uploaded or re-downloaded**.

### 3. Migration (Core)

`ISyncStorageMigrationService.MigrateAsync(profile, target, IProgress<SyncMigrationProgress>, ct)`:

1. **Pause:** `ISyncScheduler.StopAsync()`, then take the cross-tab sync lock (`ISyncLockService`), which waits for any
   pass in progress.
2. **Record intent:** save `{profile, from, to, phase: Copying}` so a reload partway through can resume or roll back.
3. **Copy:** for each file in the source's `ListAsync`, `ReadAsync` it and write it to the target's untracked store with
   the same `ModifiedAt`. Folder markers are copied too. The copy is idempotent, so a resumed run starts again from the
   beginning.
4. **Verify:** hash each target file against the source, or the index hash. If any differ, stop and leave the source
   selected.
5. **Switch:** call `SetAsync(profile, target)` and set the phase to `Cleanup`.
6. **Clean up:** clear the user's files from the source, then clear the intent record.
7. **Resume:** release the lock and call `ISyncScheduler.StartAsync()`, which runs a normal pass.

If the target is empty and the source is empty, the service skips straight to Switch. If the target already has files
(for example, from an earlier abandoned switch), it is cleared before the copy starts.

On startup, a leftover intent record means: phase `Copying` rolls back by clearing the target and keeping the source,
and phase `Cleanup` finishes the cleanup.

**Other tabs:** after switching, a `BroadcastChannel('blazwiz-sync')` message tells other tabs to reload, so they don't
keep writing to the old backend. While the migration holds the lock, tabs' sync passes wait. Edits in another tab during
the few seconds of migration are the remaining risk. The broadcast also sends a "migrating" message, and other tabs show
a read-only banner until "done".

### 4. UI (Portal)

In the Settings → **Sync** panel, under Storage:

- **Storage location** radio buttons: *Browser database (IndexedDB)* / *Browser file system (OPFS)*. OPFS is disabled,
  with a tooltip, when `isSupported` is false.
- Changing the option opens a confirm dialog ("Move N files to …? Sync pauses while they move."), then shows progress
  (`n / total`), then success or failure. If it fails, the previous storage stays selected and the error is shown.
- `SettingsViewModel` gets `StorageKind`, `IsOpfsSupported`, `IsMigrating`, `MigrationProgress` and
  `ChangeStorageCommand`. Sync now and Offline mode are disabled while migrating.

### 5. Wiring (Application)

`ServiceCollectionExtensions` changes the notes profile to `UseFileStores(IndexedDb + Opfs, default IndexedDb)` and
registers the migration service.

## Tests

- **Unit (Core):** `OpfsSyncFileStore` (mocked `IOpfsClient`: key encoding, folder markers, sidecar fallback),
  `SwitchableSyncFileStore` routing, and the migration service using the in-memory stores (happy path, empty source,
  verify failure, both resume phases, scheduler stopped and restarted, lock taken), plus the builder overloads.
- **Unit (Application):** the `SettingsViewModel` storage switch flow.
- **UI (Playwright):** create notes, switch to OPFS, the notes are still there after a reload; switch back to IndexedDB,
  the notes are still there; the sync status shows nothing pending, so nothing is re-uploaded.
- Run all four suites before calling it done. S3 sync after switching is checked by you in the deployed app.

## Out of scope / risks

- The choice applies to one device only. Each device picks its own storage.
- OPFS is still evictable unless `navigator.storage.persist()` is granted. The existing durability UI covers this.
- The migration holds every file in memory one at a time, not all at once. Fine for notes. Very large binaries would
  need streaming.

## Local folder (File System Access API)

Implemented as a third storage, `SyncStorageKind.LocalFolder`, added with `UseFileStores(s => s.AddLocalFolder())`.
This is the option for files the user can see and open in Explorer or Finder; OPFS is not.

- **Picking.** Choosing "Folder on this computer" in Settings opens `showDirectoryPicker` first (it must follow the
  click), then shows the usual move confirmation naming the folder. One folder per user, shared by every profile.
  The `FileSystemDirectoryHandle` is kept by `wwwroot/js/folder.js` in its own IndexedDB database (`blazwiz_folders`),
  because handles cannot pass through .NET.
- **Layout.** Files keep their S3 paths inside the folder, e.g. `<folder>/notes/Work/Ideas.md`. Empty folders have a
  `.folder` marker file. `filetree.js` holds the tree code shared with OPFS.
- **Only the profile's files.** `FolderSyncFileStore` lists only under the remote pattern's prefix (`notes/`) and only
  keys that match it. Anything else in the folder is never listed, moved or deleted.
- **Will not overwrite.** Moving into a folder that already holds the profile's files is refused
  (`SyncStorageMigrationService.FolderNotEmptyMessage`), rather than clearing it. Moving out of the folder deletes the
  moved notes from it, as any move does.
- **Permission.** Every call checks `queryPermission` first and throws `SyncStorageAccessException` if access is not
  granted. On start, if the folder is in use without permission, `MainLayoutViewModel` shows a "Reconnect folder"
  banner and holds sync back. The button calls `requestPermission` (needs the click), then reloads. An installed
  Chromium PWA can keep "Allow on every visit", so the banner should rarely appear there.
- **Edits made outside the app.** Beside each file it writes, the store records the modified time to report and the
  file system's time at that moment (`FolderFileRecord`). If the file system's time later differs, the file was
  changed outside the app, and that time is reported instead. Such edits are found by the verify pass after sign in
  and uploaded like any other change.
- **Browsers.** Chrome and Edge on desktop only. Elsewhere the option is disabled, with a tooltip saying why.
- **UI tests.** Playwright cannot drive the real picker, so the tests replace `showDirectoryPicker` with one that
  returns an OPFS directory handle, and stub `queryPermission`/`requestPermission` to simulate access lapsing.

Also fixed along the way: `channel.js` posted through a new `BroadcastChannel`, which the tab's own listener received,
so the tab doing the move reloaded itself (hiding any error). It now posts through the subscribed channel.
