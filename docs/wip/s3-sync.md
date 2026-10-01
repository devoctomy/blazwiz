# S3 Sync Engine (Core)

> Status: WIP / plan, not implemented. Builds on the notes in [`opfs-offline-storage.md`](opfs-offline-storage.md).

## Goal

A reusable sync engine in `blazwiz.Core` that keeps a set of local files in step with a set of objects in the user's
S3 prefix. It works with any feature and any file type. Setting it up should take a few lines:

```csharp
services.AddBlazwizSync(sync => sync
    .UseIndexedDbEventLog(database: "blazwiz_sync", store: "events")
    .UseEventLogRetention(maxAge: TimeSpan.FromDays(30), maxEventsPerUser: 5000)
    .UseChangeDebounce(TimeSpan.FromSeconds(2))
    .UseConnectivityTimings(t =>
    {
        t.CacheFor = TimeSpan.FromSeconds(30);
        t.ProbeTimeout = TimeSpan.FromSeconds(5);
        t.RecheckBackoff = [TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(5)];
    })
    .AddProfile("notes", profile => profile
        .UseIndexedDbIndexStore(database: "blazwiz_sync", store: "notes_index")
        .UseIndexedDbFileStore(database: "blazwiz_notes", store: "files")
        .UseRemote("notes/**/*.md", includeEmptyFolders: true)));
```

Each profile has three independently configured parts:

- **Index store**: where the sync index sits. It records what each file looked like the last time both sides agreed.
  IndexedDB to start (`UseIndexedDbIndexStore`), or any `ISyncIndexStore` (`UseIndexStore<T>()`).
- **File store**: where the file contents sit locally. IndexedDB to start (`UseIndexedDbFileStore`), or any
  `ISyncFileStore` (`UseFileStore<T>()`), e.g. OPFS later.
- **Remote**: a glob relative to the signed in user's S3 root (`<identity id>/`), e.g. `notes/**/*.md`.

The index and files can go in different databases (or different kinds of storage), or in two object stores of the
same database. Changing one never requires changing the other. If `UseIndexedDb…Store` is not called, the defaults
are database `blazwiz_sync`, store `<profile>_index` for the index and database `blazwiz_files`, store `<profile>`
for the files.

Files are stored and synced exactly as they are: bytes, any file type, with nothing wrapped around them. A markdown
note is a `.md` file in S3, the same as on the device.

Everything the engine does is written to a **sync log** in IndexedDB. A **Sync** page shows the log with icons and
search (see [Sync log and Sync page](#sync-log-and-sync-page)).

The Notes feature (in `Portal.Application`) is the first consumer, and uses IndexedDB for its file store too. The
whole sync system ships with the template's `IncludeNotes` option (D6).

### Sync triggers (v1: events only)

1. **On sign in** (and app start with an existing session).
2. **When a file is added, changed or removed** (debounced).
3. **When the app comes back online** (see [Connectivity](#connectivity)) and when it regains focus (`visibilitychange`).

A timer with a configurable interval is a future feature (D5). The scheduler is built so it can be added without
changing anything else.

### Non goals (v1)

- Large files and multipart uploads (see [Later](#later)). Files of any type are supported, but each is held in
  memory while it syncs.
- **Multi user collaboration.** This syncs one user's own files between their own devices. Nothing is shared
  between users, and everything stays under the user's own identity id prefix. Two devices changing the same file
  between syncs is a rare edge case, so it's handled simply and safely, not with collaboration features like merging
  or live editing.
- Background sync while the app is closed (Background Sync is Chromium only).
- Merging content. When both sides changed, the newest version of the whole file wins (D3).
- Signing in with no network (D9).

## Concepts

| Term | Meaning |
|---|---|
| **Profile** | One named set of index store, file store and remote pattern, e.g. `notes`. An app can have several. |
| **Key** | A file's path relative to the user's root, the same on both sides, e.g. `notes/Work/Ideas.md`. |
| **Folder marker** | An empty folder, kept as a zero byte key ending in `/` (e.g. `notes/Work/`), the same convention the S3 console uses. Only synced when the profile sets `includeEmptyFolders`. |
| **Local file store** | Where the app reads and writes files. `ISyncFileStore`. |
| **Remote store** | The S3 side. `ISyncRemoteStore`. |
| **Sync index** | Per profile, per user, per key: the base (last agreed) state. `ISyncIndexStore`. |
| **Base** | The content hash and S3 ETag at the last successful sync of a file. Changes are measured against it. |

## Architecture

```
Portal (Sync page, Settings page, bottom right status pill, login "Advanced options")
   │ binds to
Portal.Application
   ├─ NoteService ──uses──▶ ISyncFileStore (keyed "notes")
   ├─ CognitoS3TargetProvider : IS3SyncTargetProvider   (credentials + identity id from the identity pool)
   ├─ AuthSyncUserContext     : ISyncUserContext         (current user id for local partitioning)
   ├─ LocalStorageOfflineModeStore : IOfflineModeStore   (offline mode flag in local storage)
   ├─ SyncEventPresenter : ISyncEventPresenter            (event kind → icon, colour, label, filter group)
   └─ SyncViewModel                                       (Sync page: log, search, filters, live updates)
   │ uses
Core/Sync
   ├─ ConnectivityService : IConnectivityService   online? offline mode → browser → credentials → S3 reachable
   ├─ S3ReachabilityProbe : IS3ReachabilityProbe   ListObjectsV2 MaxKeys=1 on the user's prefix
   ├─ BrowserNetworkStatus : IBrowserNetworkStatus navigator.onLine + online / offline / visibility events
   ├─ SyncScheduler   : ISyncScheduler   triggers, debounce, single flight
   ├─ SyncService     : ISyncService     runs every profile, status + events
   ├─ SyncEngine      : ISyncEngine      one pass for one profile: snapshot → plan → execute
   ├─ SyncPlanner     : ISyncPlanner     pure decision table (no I/O)
   ├─ IndexedDbSyncEventLog : ISyncEventLog   the sync log in IndexedDB (write, query, prune)
   ├─ TrackingSyncFileStore (decorator)  marks the index dirty + requests a sync on every write / delete
   ├─ IndexedDbSyncFileStore             local files in IndexedDB
   ├─ IndexedDbSyncIndexStore            the sync index in IndexedDB
   ├─ S3SyncRemoteStore                  AWSSDK.S3, conditional writes (D2)
   ├─ GlobSyncPathMatcher                Microsoft.Extensions.FileSystemGlobbing
   ├─ Sha256ContentHasher                System.Security.Cryptography.SHA256
   └─ NewestWinsConflictResolver         default conflict policy (D3)
```

Everything is an interface + implementation registered in DI (no static helpers), so each piece can be mocked in
unit tests. Crypto and signing come from official libraries only (`SHA256`, the AWS SDK's SigV4), nothing hand rolled.

### Folder layout

`blazwiz.Core` becomes a Razor class library (`Microsoft.NET.Sdk.Razor`, no components), so it ships its own
scripts at `_content/blazwiz.Core/js/…` and is self contained (D1).

```
blazwiz.Core/Sync/
  Configuration/   SyncBuilder, SyncProfileBuilder, SyncProfileOptions
  Interfaces/      ISyncFileStore, ISyncRemoteStore, ISyncIndexStore, ISyncEngine, ISyncPlanner,
                   ISyncService, ISyncScheduler, ISyncConflictResolver, ISyncPathMatcher,
                   IContentHasher, ISyncUserContext, IS3SyncTargetProvider, IIndexedDbClient, ISyncEventLog,
                   ISyncLockService
  Models/          SyncFile, SyncFileInfo, RemoteObject, RemoteObjectInfo, SyncIndexRecord, SyncAction,
                   SyncPlan, SyncResult, SyncStatus, SyncState, SyncConflict, S3SyncTarget,
                   SyncEvent, SyncEventKind, SyncEventQuery
  IndexedDb/       IndexedDbClient, IndexedDbSyncFileStore, IndexedDbSyncIndexStore,
                   IndexedDbSyncEventLog
  S3/              S3SyncRemoteStore, S3ReachabilityProbe
  Connectivity/    IConnectivityService, IOfflineModeStore, IBrowserNetworkStatus, IS3ReachabilityProbe (Interfaces/),
                   ConnectivityService, BrowserNetworkStatus, ConnectivityStatus, OfflineReason
  Storage/         IStorageDurabilityService (Interfaces/), StorageDurabilityService, StorageDurabilityStatus
  (root)           SyncEngine, SyncPlanner, SyncService, SyncScheduler, TrackingSyncFileStore,
                   GlobSyncPathMatcher, Sha256ContentHasher, NewestWinsConflictResolver
blazwiz.Core/wwwroot/js/indexeddb.js   generic IndexedDB module
blazwiz.Core/wwwroot/js/network.js     navigator.onLine, online / offline / visibilitychange events, Web Locks
blazwiz.Core/wwwroot/js/storage.js     navigator.storage.persist() / persisted() / estimate()
```

`AddBlazwizSync` lives next to `AddBlazwizCoreServices` in `Core/Extensions`.

## Interfaces (sketch)

```csharp
// Local side. Keys are relative to the user's root. Implementations partition by user (ISyncUserContext) internally.
public interface ISyncFileStore
{
    Task<IReadOnlyList<SyncFileInfo>> ListAsync(CancellationToken ct = default);   // key, modifiedAt, size, isFolder
    Task<SyncFile?> ReadAsync(string key, CancellationToken ct = default);          // key, content (byte[]), modifiedAt
    Task WriteAsync(string key, byte[] content, CancellationToken ct = default);
    Task CreateFolderAsync(string key, CancellationToken ct = default);             // folder marker, key ends with '/'
    Task DeleteAsync(string key, CancellationToken ct = default);
}

// Remote side. ETags are opaque.
public interface ISyncRemoteStore
{
    Task<IReadOnlyList<RemoteObjectInfo>> ListAsync(CancellationToken ct = default); // key, etag, lastModified, size
    Task<RemoteObject?> GetAsync(string key, CancellationToken ct = default);        // content, etag, modifiedAt
    Task<RemoteObjectInfo?> GetInfoAsync(string key, CancellationToken ct = default); // HEAD: etag, modifiedAt; null if gone. Conflicts only
    Task<string> PutAsync(string key, byte[] content, DateTimeOffset modifiedAt, string? ifMatch, bool ifNoneMatch,
        CancellationToken ct = default); // new etag; throws SyncPreconditionFailedException on 412
    Task DeleteAsync(string key, string? ifMatch, CancellationToken ct = default); // throws SyncPreconditionFailedException on 412
}

public interface ISyncIndexStore
{
    Task<IReadOnlyList<SyncIndexRecord>> GetAllAsync(string profile, string owner, CancellationToken ct = default);
    Task SaveAsync(SyncIndexRecord record, CancellationToken ct = default);
    Task DeleteAsync(string profile, string owner, string key, CancellationToken ct = default);
}

public interface ISyncService
{
    SyncStatus Status { get; }                       // state, last synced, last error, pending count
    event EventHandler<SyncStatus>? StatusChanged;
    event EventHandler<SyncFilesChangedEventArgs>? FilesChanged;   // profile + keys changed by a download / remote delete
    Task<SyncResult> SyncAsync(string? profile = null, CancellationToken ct = default);
}

public interface ISyncScheduler
{
    Task StartAsync();                 // on sign in: immediate sync, hook online / visibility events
    Task StopAsync();                  // on sign out: cancel any pass, unhook events
    void RequestSync(string profile);  // debounced (default 2s)
}

// Supplied by the app, so Core does not know about Cognito.
public interface IS3SyncTargetProvider
{
    Task<S3SyncTarget?> GetTargetAsync(CancellationToken ct = default); // IAmazonS3 client, bucket, root prefix; null = no credentials
}

public interface ISyncUserContext
{
    Task<string?> GetUserIdAsync(); // partitions local data and the index; null = not signed in
}
```

Content is `byte[]`, so any file type works. Since .NET 6, JS interop passes `byte[]` as binary (a `Uint8Array`)
rather than base64, so this is cheap for small and medium files.

Each profile registers its file store and its index store as keyed services under the profile name. The engine gets
both for the profile it is syncing. App code only ever asks for the file store
(`[FromKeyedServices("notes")] ISyncFileStore`). That store comes already wrapped by `TrackingSyncFileStore`, which
writes to the profile's index store, so app code cannot forget to mark changes and never touches the index directly.

Because the file store and index store can be in different databases, a file write and its index update can't share
one transaction. The order of operations covers this: the tracker writes the file first, then marks the index. A crash
between the two leaves a file whose hash no longer matches the index's `baseHash`. The verify pass after sign in
compares hashes, finds it and uploads it.

## Data

### Sync index record (the profile's index store)

| Field | Notes |
|---|---|
| `profile`, `owner`, `key` | Compound key. Index on `[profile, owner]`. Keeping `profile` in the key lets several profiles share one index store if configured that way. |
| `baseHash` | SHA-256 of the content at the last sync. Null if never synced. |
| `baseETag` | S3 ETag at the last sync. Null if never synced. |
| `dirty` | Set by `TrackingSyncFileStore` on write. |
| `deleted` / `deletedAt` | Tombstone set by `TrackingSyncFileStore` on delete, so the file is not downloaded again. `deletedAt` is used when a delete and an edit compete (newest wins). |
| `lastSyncedAt` | For status / diagnostics. |

The index only holds bookkeeping (no content), so it stays small wherever the files are kept.

### Local file record (the profile's file store, IndexedDB implementation)

`{ owner, key, content (bytes), modifiedAt, size }`, compound key `[owner, key]`, index on `owner`. Folder markers have
empty content.

### Remote layout

`s3://<data bucket>/<identity id>/<key>`. For `notes/**/*.md` the engine calls
`ListObjectsV2(Prefix = "<identity id>/notes/")` and then matches each relative key against the glob. It uses the
literal part of the pattern before the first wildcard as the S3 prefix, to keep listings small. `*` does not cross
`/`, `**` does. With `includeEmptyFolders`, keys ending in `/` under the prefix are synced too.

Each upload sets `x-amz-meta-modified-at` to the file's local modified time. It is only read when both sides have
changed (D3), because `ListObjectsV2` does not return metadata.

The existing IAM role already allows List / Get / Put / Delete under the identity id prefix, and the bucket CORS
already exposes `ETag`. The CORS rules may need `ExposedHeaders` to include `x-amz-meta-modified-at`. Phase 0 checks this.

## The algorithm

One pass for one profile (`SyncEngine`), single flight per profile:

1. **Snapshot**: list local files, list remote objects (paginated, filtered by the matcher), load the index.
2. **Plan** (`SyncPlanner`, pure): for each key in the union, decide an action from the table below.
3. **Execute** each action, updating the index record for that file straight after it succeeds, so a crash part way
   through loses at most one file's bookkeeping, and the next pass heals it (see "no base" rows).
4. **Report**: `SyncResult` (counts, conflicts resolved, failures), raise `FilesChanged` for downloaded / removed keys,
   update status.

Per file failures are recorded and the pass continues with the other files.

### Decision table

"Local changed" = `dirty`, or (on a full verify pass) local hash ≠ `baseHash`. "Remote changed" = ETag ≠ `baseETag`.
Like cachy, the ETag is the main signal. The index tells us *which side* changed, so timestamps are only needed when
both sides did.

| Local | Remote | Action |
|---|---|---|
| unchanged | unchanged | nothing |
| changed | unchanged | **upload** with `If-Match: baseETag` |
| unchanged | changed | **download** |
| changed | changed | **conflict** → newest wins (D3) |
| new (no base) | absent | **upload** with `If-None-Match: *` |
| absent | new (no base) | **download** |
| new (no base) | new (no base) | if hashes equal → adopt, else **conflict** → newest wins |
| deleted (tombstone) | unchanged | **delete remote** with `If-Match: baseETag`, drop record. A 412 (changed since the list) re-plans the file as deleted vs changed. Already gone (404) just drops the record. (D14) |
| deleted (tombstone) | changed | **conflict** → newest wins (`deletedAt` vs remote modified time) |
| deleted (tombstone) | absent | drop record |
| unchanged | absent (had base) | **delete local** without asking, drop record, logged as `DeleteLocal` (D17) |
| changed | absent (had base) | **conflict** → the local edit wins and is uploaded again (there's no remote delete time to compare against) |

A `412 Precondition Failed` on upload means someone else changed the file since we listed: re-plan that one file.

The "hashes equal → adopt" rule means an interrupted pass, or a first sync where both sides already have the same
content, resolves without a conflict.

### Conflicts (D3: newest wins)

A conflict means **both sides changed the same file since the last sync**. Examples:

- You edit `Ideas.md` on your laptop while it's offline (on a train), and edit the same note on your phone before
  the laptop syncs. When the laptop comes back online, both the local copy and S3 have changed since the last sync.
- You delete `Old.md` on one device while it's offline, and edit it on another.

Both examples need the same user to change the same file on two devices before either has synced, so this is a
rare edge case, as it was in cachy. The aim is a simple, predictable rule, not collaboration.

`NewestWinsConflictResolver` (the default, pluggable through `ISyncConflictResolver`) compares edit times and keeps
the newest version of the whole file. The other version is overwritten.

- **Local time**: the file's `modifiedAt` (or the tombstone's `deletedAt`).
- **Remote time**: `x-amz-meta-modified-at`, read with a HEAD request only for files in conflict. If it's missing
  (e.g. the file was uploaded by another tool), S3's `LastModified` is used instead.
- S3's `LastModified` is the **upload** time, not the edit time. A file edited offline at 10:00 and uploaded at
  12:00 has a `LastModified` of 12:00. Comparing against it would let a stale edit win, which is why the edit time
  goes in object metadata.
- Each conflict is recorded in `SyncResult` and the status shows "N conflicts resolved", so it's visible when it happens.

## Triggers and scheduling (`SyncScheduler`)

- **Sign in**: `LoginViewModel` (after a successful sign in) and `MainLayoutViewModel` (app start with a saved session)
  call `StartAsync`. The first pass after sign in is a full verify pass (hash local files against the index).
- **Sign out**: `StopAsync` cancels any pass and unhooks events. Local data stays, partitioned by user.
- **Changes**: `TrackingSyncFileStore` calls `RequestSync(profile)`. Requests are debounced (2s by default, set with `UseChangeDebounce`) and coalesced: a request
  during a pass sets a "run again" flag instead of starting a second pass.
- **Focus**: `visibilitychange` (visible) requests a sync, through `IBrowserNetworkStatus`.
- **Back online**: when `IConnectivityService` goes from offline to online (see [Connectivity](#connectivity)), every
  profile is synced.
- **Timer**: not in v1 (D5). It would be another trigger source in the scheduler, with the interval from settings.
- **Several tabs** (D16): passes take a Web Lock (`navigator.locks.request("blazwiz-sync-<profile>")`) through
  `ISyncLockService`, so two tabs (or two windows of the installed PWA) never sync the same profile at once. A tab
  that has to wait then finds nothing left to do. The browser releases the lock if a tab closes mid-sync.
- **Offline**: every trigger checks `IConnectivityService` first. While offline, nothing runs, changes keep being
  recorded in the index, and the pending count goes up. A network failure during a pass is reported to
  `IConnectivityService`, which marks the app as offline. A 403 refreshes the credentials once, then fails the pass.

## Connectivity

Sync only runs when the app is **online**. `IConnectivityService` (Core) decides this and checks the reasons in a set
order. The cheapest checks come first, and the first one that fails sets the reason:

| # | Check | Offline reason | Network call? |
|---|---|---|---|
| 1 | Has the user turned on offline mode? | `ForcedOffline` | No |
| 2 | Does the browser report a network (`navigator.onLine`)? | `NoNetwork` | No |
| 3 | Do we have AWS credentials (`IS3SyncTargetProvider` returns a target)? | `NoCredentials` | Yes, to Cognito, unless cached |
| 4 | Is S3 reachable? (`ListObjectsV2` on the user's prefix with `MaxKeys = 1`, `ProbeTimeout`, default 5s) | `Unreachable` | Yes, to S3 |

- Check 1 comes first so that offline mode makes **no network calls at all**, including to Cognito.
- Check 2 can only prove the app is offline. `navigator.onLine == true` does not mean S3 is reachable, so checks 3 and
  4 still run.
- Check 3 covers the placeholder sign-in and a failed identity pool exchange.
- Check 4 uses the user's own prefix, because `HeadBucket` needs bucket wide `s3:ListBucket`, which the IAM role does
  not grant. It also proves the credentials work.

```csharp
public interface IConnectivityService
{
    ConnectivityStatus Status { get; }               // IsOnline, Reason (null when online), LastCheckedAt
    event EventHandler<ConnectivityStatus>? StatusChanged;
    bool IsForcedOffline { get; }
    Task SetForcedOfflineAsync(bool forcedOffline);  // persisted through IOfflineModeStore
    Task<ConnectivityStatus> CheckAsync(CancellationToken ct = default);
    void ReportNetworkFailure(Exception exception);  // from the engine; marks Unreachable and schedules a recheck
}
```

Supporting pieces (interface + implementation each):

- `IOfflineModeStore`: saves the offline mode flag. Implemented in `Portal.Application` over `ILocalStorageService`,
  because Core does not know about local storage.
- `IBrowserNetworkStatus`: `navigator.onLine` plus the `online` / `offline` / `visibilitychange` events (`network.js`).
- `IS3ReachabilityProbe`: runs check 4 against the target from `IS3SyncTargetProvider`.

**When it checks**:

- on sign in
- before each sync pass. If the last check is within `CacheFor` (default 30s), the cached result is reused.
- on the browser `online` / `offline` events
- when the engine reports a network failure
- when offline mode is turned off

While offline for any reason other than `ForcedOffline`, it rechecks using `RecheckBackoff` (default 30s, 1m, 2m, then every 5m; the last value repeats). It
never polls in offline mode. Going from offline to online raises `StatusChanged`, and the scheduler then syncs.

**Offline mode does not stop the app working.** Notes can still be read and edited locally. Changes are tracked and
upload when the app goes back online. The auth service also checks `IsForcedOffline` and skips token refreshes, so it
keeps the stored session like it already does when the network is down.

### Offline mode UI

Offline mode is **remembered** (saved in local storage) across reloads and sign outs until it is switched off (D8).

- **Status pill, bottom right** (when signed in, in `MainLayout`). It shows connectivity and sync state together, as
  one of: up to date, syncing, N pending, offline, or error. The reason is in the tooltip, e.g. "Offline: S3
  unreachable". Clicking it opens a small popover with:
  - an **Offline mode** switch
  - **Sync now**, disabled while offline
  - the last synced time

  Logic lives in `MainLayoutViewModel`.
- **Login page, "Advanced options"**: an expandable section under the sign-in form with a **Start in offline mode**
  checkbox.
  - Opening the login page with offline mode saved as on: the section starts **expanded** and the box is **ticked**.
  - Otherwise, if no other advanced option is set either: the section starts **collapsed**.
  - The general rule: the section is expanded whenever any advanced option differs from its default, so a setting
    that changes behaviour is never hidden.
  - Ticking the box turns offline mode on before the first sync after sign in starts, so that sync never makes a
    network call. Unticking it turns offline mode off.
  - Logic lives in `LoginViewModel` (`IsAdvancedOptionsExpanded`, `StartInOfflineMode`).
- Signing in with Cognito still needs the network. Signing in with no network is future work (D9).

## Sync log and Sync page

### What gets logged

The engine, scheduler and connectivity service write a `SyncEvent` for everything they do, through `ISyncEventLog`.
File events carry the key, so each one says which file and what happened to it. Every event in one pass shares a
`passId`, so the page can group them.

| Kind (`SyncEventKind`) | Message | Icon (Bootstrap Icons) | Group |
|---|---|---|---|
| `PassStarted` | Checking for changes | `bi-arrow-repeat` | Activity |
| `PassCompleted` | Up to date / 3 uploaded, 1 downloaded | `bi-check-circle` | Activity |
| `UploadNew` | New local file, uploading | `bi-cloud-plus` | Uploads |
| `UploadLocalNewer` | Local newer than remote, uploading | `bi-cloud-arrow-up` | Uploads |
| `DownloadNew` | New remote file, downloading | `bi-file-earmark-arrow-down` | Downloads |
| `DownloadRemoteNewer` | Remote newer than local, downloading | `bi-cloud-arrow-down` | Downloads |
| `DeleteRemote` | Deleted locally, removing from S3 | `bi-cloud-slash` | Deletes |
| `DeleteLocal` | Deleted remotely, removing local copy | `bi-trash` | Deletes |
| `ConflictResolved` | Changed on both sides, kept the newer local / remote version | `bi-exclamation-triangle` | Conflicts |
| `AlreadyInSync` | Same on both sides, nothing to do (no base, hashes equal) | `bi-check2-all` | Activity |
| `Retried` | Changed in S3 during sync, checking again (412) | `bi-arrow-clockwise` | Activity |
| `Failed` | Upload / download / delete failed: reason | `bi-x-circle` | Errors |
| `WentOffline` | Offline: S3 unreachable / no network / no credentials | `bi-wifi-off` | Connectivity |
| `WentOnline` | Back online | `bi-wifi` | Connectivity |
| `OfflineModeChanged` | Offline mode turned on / off | `bi-airplane` | Connectivity |

Icons, colours, labels and groups are mapped in `Portal.Application` (`ISyncEventPresenter`), not in Core, so Core stays
free of UI concerns. Colours use the existing theme variables, so they work in light and dark mode.

### Storage

- `SyncEvent`: `{ id, owner, profile, passId, timestamp, kind, key, message, detail }`. `detail` holds error text or
  extra information, e.g. which side won a conflict.
- Kept by `IndexedDbSyncEventLog` in its own configurable store (`UseIndexedDbEventLog(database, store)`, default
  `blazwiz_sync` / `events`), with an index on `[owner, timestamp]`. One log covers every profile, and each event
  records its profile. Like the other stores, it can be replaced (`UseEventLog<T>()`).
- `ISyncEventLog`: `WriteAsync(events)`, `QueryAsync(SyncEventQuery)` (newest first, paged, optional profile / groups /
  text / date range), `ClearAsync()`, `PruneAsync()`, and an `EventsWritten` event so the page updates live.
- **Retention** (D19): after each pass, prune to the last 30 days and at most 5,000 events per user. Both limits
  are set when configuring sync:
  `.UseEventLogRetention(maxAge: TimeSpan.FromDays(30), maxEventsPerUser: 5000)`.
- **Noise** (D18): a pass that changes nothing isn't kept as two rows every time the app regains focus. Consecutive
  no-change passes collapse into one "Checking for changes · up to date" row, with a count and the last checked time.

### Sync page

A new **Sync** page (`/sync`, sidebar item with `bi-arrow-repeat`, added to `NavigationMenuService`):

- **Header**: current status (the same as the pill), last synced time, pending count, **Sync now**, and the
  **Offline mode** switch.
- **Search box**: matches the message, file key and profile as you type (debounced).
- **Filter chips**: All, Uploads, Downloads, Deletes, Conflicts, Errors, Connectivity, plus a profile filter when
  there is more than one profile.
- **Log**: newest first, grouped by day. Each pass is one row ("Checking for changes · 3 uploaded, 1 downloaded")
  that expands to show its file events, each with an icon, a relative time (exact time in a tooltip), the key and the
  message. Errors are expanded by default.
- **Live**: new events appear at the top while the page is open.
- **Paging**: loads 200 events at a time with "Load more" (or Blazor `Virtualize`). In v1, search runs in .NET over the
  loaded page and fetches more as needed. Searching inside IndexedDB can come later if logs get large.
- **Clear log** button, with a confirmation.
- The status pill's popover gets a **View sync log** link to this page.

All logic lives in `SyncViewModel` (search, filters, grouping, paging, live updates), so the page is markup only.

## Credentials (`Portal.Application`)

`CognitoS3TargetProvider : IS3SyncTargetProvider`:

- Uses the official libraries (`Amazon.Extensions.CognitoAuthentication` → `CognitoUser.GetCognitoAWSCredentials`, or
  `AWSSDK.CognitoIdentity` `GetId` + `GetCredentialsForIdentity` with the ID token) to get temporary credentials and the
  identity id. This is the README's "AWS credentials in the browser" roadmap item.
- Creates an `AmazonS3Client` with the existing `BrowserHttpClientFactory` / `BufferingHttpHandler` (same reason as the
  Cognito client: the SDK reads responses synchronously). Cached until the credentials are close to expiry.
- Returns null when signed in with the placeholder `LocalAuthenticationService`, so connectivity reports
  `Offline` / `NoCredentials` rather than failing. It is never called in offline mode.
- There is no local stand-in for S3 (D12). Without AWS (the placeholder sign-in, local development, the UI tests)
  the app simply has no cloud sync: everything works locally, changes stay pending, and the status says why.

## Settings page and status

- Settings page gets a **Sync** card: **Sync now** button, last synced time, state, pending changes count, last
  error, current connectivity status and reason, and the **Offline mode** switch. Logic in `SettingsViewModel` via
  `ISyncService` / `IConnectivityService`.
- No sync interval setting in v1 (D5).

### Persistent storage (D15)

Browsers treat a site's storage as "best effort" by default. They may clear it when the disk is low, and Safari
clears it after 7 days without use unless the app is on the home screen. Until a change has synced, the local copy
is the only one, so the app asks for persistent storage.

- **When**: once, straight after the first successful sign in, through `IStorageDurabilityService` (Core, interface +
  implementation over a small `storage.js` helper). Asking right after the sign-in click means Firefox's permission
  prompt appears at a sensible moment. It asks again on later sign ins only if the browser said no and the app has
  since been installed.
- **What the user sees**:
  - Chrome / Edge: nothing. The browser decides silently, based on things like the app being installed or used often.
  - Firefox: a small permission bar under the address bar: "Allow *blazwiz* to store data in persistent storage?"
    with **Allow** / **Don't allow**.
  - Safari: nothing. It decides by its own rules.
- **Settings Sync card** gets a **Storage** line:
  - `✓ Persistent · 2.1 MB used` when granted.
  - `⚠ Best effort · 2.1 MB used`, plus a hint "Install the app so your browser keeps your data", when refused.

  Usage comes from `navigator.storage.estimate()`.
- **Covers future storage too**: persistence applies to the whole site's storage (IndexedDB, OPFS and the Cache API),
  not one database. A later OPFS file store gets the same protection without changes. OPFS itself is not part of
  this piece of work.
- The bottom right status pill is the quick way to reach the same things (see [Offline mode UI](#offline-mode-ui)).

## Notes integration (`Portal.Application`)

### Files are the notes (D4)

Notes are plain markdown files, and folders are paths:

```
<identity id>/notes/Welcome.md
<identity id>/notes/Work/Ideas.md
<identity id>/notes/Work/Meetings/2026-10-01.md
<identity id>/notes/Empty Folder/          (folder marker)
```

Name, folder and modified time all come from the key and the file store, so no extra metadata is stored. Anything
that can read markdown can read the notes straight from S3.

### Changes to Notes

- `NoteService` reads and writes through the keyed `ISyncFileStore` ("notes"), UTF-8 encoding the markdown, instead of
  using `INoteIndexStore` + `INoteContentStore`. The file tree is built from the keys (`NoteTreeService`).
- A note's id becomes its key (e.g. `notes/Work/Ideas.md`). The editor route `/files/edit/{NoteId}` takes the URL
  encoded key.
- **Rename / move**: S3 has no rename, so this is a write of the new key and a delete of the old key. A folder
  rename or move does this for every file under it, plus the folder marker if there is one.
- **Name validation** (`NoteNameService`) also rejects characters that are unsafe in S3 keys. Names are still compared
  case-insensitively within a folder. S3 keys are case sensitive, so the same name in a different case on two devices
  would become two files.
- `IndexedDbNoteIndexStore`, `IndexedDbNoteContentStore`, `INoteIndexStore`, `INoteContentStore`,
  `NoteStorageProviders`, `NoteContent` and `NoteEntry.StorageProvider` are removed. The file store is now chosen per
  profile, which is what makes it swappable. `NoteEntry` stays as a view model shape, built from keys.
- `notes.js` keeps only the editor helpers. The IndexedDB code moves to Core's generic module.
- The Files page and open editor listen for `FilesChanged` and refresh. If the open note changed remotely and the
  editor has unsaved edits, ask the user (keep mine / take theirs), using the same prompt pattern as the existing
  unsaved changes check (D13).
- **Welcome note** is `notes/Welcome.md`. It is only created when the user's notes are empty **after** the first
  sync, or straight away if sync can't run (offline / no credentials). That way, deleting it on one device doesn't
  bring it back from a new device. `NotesState.WelcomeNoteCreated` stays local.
- **Migration**: on first load, existing `entries` + `content` records are converted to files at their folder paths
  (marked dirty, so they upload). Then the old object stores are removed in the database upgrade.

### Swapping stores later

The file store and index store are swapped independently:

- **File store**: a future `OpfsSyncFileStore` is registered with `.UseFileStore<OpfsSyncFileStore>()`. Moving
  existing data means copying it from the old store to the new one. The index doesn't change and the hashes still
  match, so nothing is uploaded again.
- **Index store**: moving the index means copying its records across, and the files stay where they are. If the index
  is lost or reset instead, the next pass treats every file as having no base. Identical content on both sides is
  adopted by the "hashes equal" rule, so the index rebuilds itself; only real differences go through the conflict
  policy.

## Template (D6)

Everything sync related ships only with `IncludeNotes`, because without Notes there is nothing to sync and the
Notes feature is the example of how to set it up:

- `blazwiz.Core/Sync/**` (including the sync log), `blazwiz.Core/wwwroot/js/indexeddb.js`, `network.js` and `storage.js`, the
  matching tests in `blazwiz.Core.UnitTests` / `blazwiz.Core.IntTests`, `AddBlazwizSync`, and the AWS S3 package
  reference.
- The portal pieces: `CognitoS3TargetProvider`, `AuthSyncUserContext`, `LocalStorageOfflineModeStore`, the Sync page,
  `SyncViewModel`, `SyncEventPresenter`, the Sync navigation item, the status pill, the Settings Sync card, and the
  login "Advanced options" section.
- These are added to the `(!IncludeNotes)` exclude list in `.template.config/template.json`. Code inside shared files
  uses `//#if (IncludeNotes)` markers, as today.
- `blazwiz.Core` stays a Razor class library either way. It's harmless with no `wwwroot` content.

## Decisions

| # | Question | Decision | Status |
|---|---|---|---|
| D1 | Where does the generic IndexedDB JS live? | In Core, which becomes a Razor class library so it's self contained. | Decided |
| D2 | S3 client: `AWSSDK.S3` in .NET, or the bundled JS SDK like clypse? | Full .NET preferred. The phase 0 spike proves it in the browser. clypse switched to the JS SDK, but the reason isn't written down in that repo, so the spike tests the likely causes (see R1). If .NET can't be made to work, the JS SDK goes behind `ISyncRemoteStore`, so nothing else changes. | Decided, pending spike |
| D3 | Conflict policy | Newest wins, by edit time (`x-amz-meta-modified-at`, not S3's upload time). ETag differences drive everything else. Pluggable through `ISyncConflictResolver`. | Decided |
| D4 | Notes file format | Plain `.md` files, folders as paths, no JSON. The engine handles any file type (`byte[]`). | Decided |
| D5 | Timer | Event based only in v1. A configurable timer is a future feature. | Decided |
| D6 | Template option | All sync code ships under `IncludeNotes`. | Decided |
| D7 | Where Notes keeps its index and files | Index in `blazwiz_sync` / `notes_index`, files in `blazwiz_notes` / `files`. | Decided |
| D8 | Remember offline mode? | Yes. The login page's advanced options section starts expanded with the box ticked when it's on, and collapsed when it's off and no other advanced option is set. | Decided |
| D9 | Signing in with no network | Future piece of work. | Decided |
| D10 | Debounce after a change | 2s by default. Set with `UseChangeDebounce(...)`. | Decided |
| D11 | Connectivity timings | Reuse a check for 30s, 5s S3 timeout, rechecks at 30s, 1m, 2m, then every 5m, by default. Set with `UseConnectivityTimings(...)`. | Decided |
| D12 | Local stand-in for S3 | None. Without AWS there is simply no cloud sync. The UI tests cover the no-sync experience, and `Core.IntTests` cover real syncing against S3. | Decided |
| D13 | Open note changed remotely while it has unsaved edits | Ask the user: keep mine / take theirs. A rare edge case (same user, two devices, same file), so a simple prompt is enough. | Decided |
| D14 | Making sure a remote delete doesn't remove a newer version | Conditional deletes: `DeleteObject` with `If-Match: <base ETag>`, supported on general purpose buckets since September 2025. A 412 re-plans the file, the same as uploads (D21). | Decided |
| D15 | Ask the browser for persistent storage (`navigator.storage.persist()`) | Yes, after the first sign in. Show the result and usage on the Settings Sync card. Applies to all site storage, so future stores like OPFS are covered too. | Decided |
| D16 | Several tabs | Web Locks, one sync per profile at a time. A rare edge case, since the app is a PWA meant to be installed, so locking is all that's needed. Other tabs aren't told about changes in v1. | Decided |
| D17 | Remote file deleted, local copy unchanged | Delete locally without asking. The sync log records it ("Deleted remotely, removing local copy", `DeleteLocal`). | Decided |
| D18 | No-change passes in the log | Consecutive ones collapse into a single row with a count. | Decided |
| D19 | Log retention | 30 days, and at most 5,000 events per user by default. Set with `UseEventLogRetention(...)` (see [Storage](#storage)). | Decided |
| D20 | Where the sync log lives in the UI | Its own **Sync** page in the main navigation. It's a core feature of the framework. | Decided |
| D21 | Making sure an upload doesn't overwrite a newer version | Conditional uploads: `If-Match: <base ETag>` for changed files, `If-None-Match: *` for new ones. A 412 re-plans the file. A rare edge case, but cheap to include now. | Decided |

## Risks

- **R1 AWSSDK.S3 in WASM** (D2). Things the spike checks:
  - synchronous response reads (handled by `BufferingHttpHandler`, as for Cognito)
  - default flexible checksums (`x-amz-checksum-crc32`, and the `aws-chunked` / trailing checksum encoding on
    `PutObject`, which browsers may not allow). The `RequestChecksumCalculation` / `ResponseChecksumValidation`
    settings can turn these off if needed.
  - CORS on the custom headers (`x-amz-meta-*`, `If-Match`, `If-None-Match`)
  - response streams in `GetObject`
  - download size of the SDK
- **R2 Eviction**: until a file uploads, the local copy is the only one. Ask for persistent storage (D15), and show the
  pending count in the status so users can see unsynced changes.
- **R3 Clock skew**: newest wins (D3) depends on device clocks being roughly right. That's acceptable for one user's
  own devices, and it only matters when both sides changed.
- **R4 Overwritten edits**: newest wins overwrites the older version with no copy kept. Enabling S3 bucket versioning
  would give a way back for the remote side, but it's a stack change and isn't planned for v1.

## Phases

0. **Spike**: identity pool credentials + `AWSSDK.S3` List / Get / Put (with `If-Match`, `If-None-Match` and
   `x-amz-meta-modified-at`) / HEAD (ETag and metadata) / Delete (with `If-Match`) from the deployed portal. Settles D2 and the CORS
   changes.
1. **Core engine**: models, interfaces, `SyncPlanner` (table driven tests for every row), `SyncEngine`,
   `GlobSyncPathMatcher` (including folder markers), `Sha256ContentHasher`, `NewestWinsConflictResolver`, in-memory
   test doubles for the stores.
2. **Core stores**: Core as a Razor class library, generic IndexedDB JS module + `IndexedDbClient`,
   `IndexedDbSyncFileStore`, `IndexedDbSyncIndexStore`, `TrackingSyncFileStore`, `S3SyncRemoteStore`. The
   `AddBlazwizSync` builder, with the index store and file store configured separately per profile (keyed services),
   and the debounce, connectivity timing and log retention options. Builder tests cover defaults, custom stores and
   mixed combinations.
3. **Connectivity, scheduling and log**: `ConnectivityService` (check order, caching, backoff recheck, offline mode),
   `S3ReachabilityProbe`, `BrowserNetworkStatus`, `SyncService`, `SyncScheduler` (debounce, single flight, focus and
   back-online triggers, Web Lock), `IndexedDbSyncEventLog` (write, query, prune, collapsing no-change passes), and
   events written from the engine, scheduler and connectivity service.
4. **Portal wiring**: `CognitoS3TargetProvider`, `AuthSyncUserContext`, start / stop on sign in / out,
   `LocalStorageOfflineModeStore`, bottom right status pill + offline mode switch, login "Advanced options" with
   **Start in offline mode** (expanded when set), skip token refresh in offline mode, persistent storage request
   (`IStorageDurabilityService`), Settings Sync card with the Storage line, Sync page in
   the main navigation (`SyncViewModel`, `SyncEventPresenter`).
5. **Notes**: `NoteService` on the file store with keys as paths, rename / move as write + delete, remove the old
   stores, migration, `FilesChanged` refresh, editor prompt (D13), welcome note after first sync.
6. **Docs + template**: README (Notes, Configuration, Roadmap), CloudFormation CORS changes from phase 0,
   `test-auth.ps1` check if needed, template excludes and conditionals (D6), `test-template.ps1` passes with
   `IncludeNotes` on and off.

## Testing

All four suites must pass before this is done.

- **Core.UnitTests**: one file per class.
  - `SyncPlanner` covers every table row.
  - `SyncEngine` with mocked stores covers ordering, per file failure, 412 re-plan on both uploads and deletes, and index updates after each action.
  - `NewestWinsConflictResolver` covers local newer, remote newer, missing metadata falling back to `LastModified`,
    and delete vs edit.
  - `GlobSyncPathMatcher` covers `*`, `**` and folder markers.
  - `SyncScheduler` covers debounce, coalescing, stop, no passes while offline, sync on back online / focus, and
    taking the per-profile lock around each pass (through a mocked lock service).
  - `TrackingSyncFileStore` marks dirty / tombstones.
  - The engine writes the right `SyncEventKind` for every decision table row, all with the same `passId`.
  - `IndexedDbSyncEventLog` query building, retention pruning, and collapsing no-change passes (over a mocked
    `IIndexedDbClient`).
  - `ConnectivityService` covers the check order (offline mode makes no calls, browser offline skips credentials,
    no credentials skips the probe), caching, backoff and failure reporting.
- **Core.IntTests** (Reqnroll, `@aws`): two simulated devices (two in-memory local stores) syncing through the real
  bucket. Covers create, edit, delete, rename, nested folders, empty folders, a binary file, edit / edit (newest
  wins), delete / edit, and recovery from an interrupted pass. Also `S3ReachabilityProbe` succeeding with valid
  credentials and reporting `Unreachable` for a bad endpoint.
- **Portal.Application.UnitTests**:
  - `NoteService` over a mocked file store: paths, rename / move, folder rename, welcome note timing
  - migration
  - `CognitoS3TargetProvider` (placeholder auth gives null)
  - `SettingsViewModel` Sync card, including the Storage line (persistent / best effort, usage, install hint)
  - persistent storage requested once after the first sign in, and again only if refused and since installed
  - `SyncViewModel`: search, filter chips, grouping by pass and day, paging, live updates, clearing the log
  - `SyncEventPresenter`: every `SyncEventKind` has an icon, label and group
  - `MainLayoutViewModel` status pill and offline mode switch
  - `LoginViewModel` advanced options: expanded and ticked when offline mode is saved, collapsed otherwise
  - `LocalStorageOfflineModeStore`
  - the auth service skipping token refresh in offline mode
- **Portal.UITests**: these run with the placeholder sign-in and no AWS, so there is no cloud sync. They cover what
  the user sees without it. Real syncing is covered by `Core.IntTests` against the real bucket (D12).
  - The status pill shows "Offline: no credentials", and **Sync now** is disabled.
  - Editing a note works offline, and the pending count goes up.
  - The offline mode switch changes the reason to offline mode, and survives a reload.
  - After signing out in offline mode, the login page shows advanced options expanded with **Start in offline mode**
    ticked. Unticking it and signing in leaves offline mode off.
  - Sync page: it's in the main navigation, shows the "Offline: no credentials" and offline mode events with their
    icons, search finds them, the Connectivity filter shows them and Uploads hides them, and the log is still there
    after a reload.

## Later

- Searching inside IndexedDB (or a full text index) for large sync logs.
- Telling other open tabs about synced changes (`BroadcastChannel`), so their file lists refresh. Not needed while
  the app is used as an installed PWA.
- Timer based sync with the interval set on the Settings page (D5).
- Signing in with no network (D9).
- Large files: streaming (`IJSStreamReference` / `DotNetStreamReference`) and multipart upload.
- `OpfsSyncFileStore` (see the OPFS notes).
- Delta listing (only list keys changed since the last pass) if listings get large.
- Optional client side encryption per profile, using the existing `ICryptoService`.
