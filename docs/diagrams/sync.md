# Sync

A sync profile is a set of files kept on the device and synced with a folder of the user's prefix in the S3 data
bucket. Every change is recorded locally first, so the app works the same offline. A pass compares three things per
file (the local file, the S3 object, and the index record from the last sync) and decides what to do. Every profile
is end to end encrypted with its own keys, behind its own passphrase.

The examples use the template's `files` profile (`template/MyApp.Portal/Sync/FilesSyncProfile.cs`): files under
`files/` in the user's prefix, kept in IndexedDB, OPFS or a folder the user picks. [sync-framework.md](sync-framework.md)
covers how an app sets profiles up. Storage names use the template's app name, `myapp`: the storage clients add the
prefix (`IAppStorageKeys`), so the code names them `sync`, `files` and so on.

Main classes (all in `src/Devoctomy.Blazwiz.Sync/` unless noted):

- `SyncSessionHandler` (`src/Devoctomy.Blazwiz.UI.Sync/Services/`): a session lifecycle handler that starts sync when the session starts and stops it at sign out
- `SyncScheduler`: when passes run (start, change debounce, back online, back to the foreground, a profile added, Sync now)
- `ConnectivityService` (`Connectivity/`): whether a pass may touch the network
- `SyncService`: runs passes per profile, gates, status, pending count
- `SyncEngine` + `SyncPlanner`: one pass, the decision table
- `SyncProfileRegistry`: every profile's stores, including the profiles the user creates while the app runs
- `TrackingSyncFileStore`: marks the index when the app writes a file
- `EncryptingSyncFileStore` (`Encryption/`): plaintext in the app, ciphertext on disk and in S3, with the profile's own keys
- `SyncProtectedProfileService` (`Encryption/`) and `E2eProtectedProfileManager` (`src/Devoctomy.Blazwiz.Encryption/`): each profile's keys and passphrase, the profiles the user creates, unlocking and locking
- `E2eIdleLock` and `E2eLockChannel` (`Encryption/`): locking a profile after it has been idle, and in every tab when it is locked in one
- `SyncKeyRotationService` (`Encryption/`): changing a profile's passphrase, which rotates its keys and encrypts its files again (diagram 8), with `S3SyncScratchStore` (`S3/`) for the upload it times first
- `SwitchableSyncFileStore` and `SyncStorageResolver` (`Storage/`): the storage chosen on this device (IndexedDB, OPFS or a folder), and `SyncStorageMigrationService` to move between them
- `S3SyncRemoteStore` (`S3/`), with `CognitoS3TargetProvider` (`src/Devoctomy.Blazwiz.UI.Sync/Services/`) for the client
- `WebLockSyncLockService`: one pass per profile across all tabs
- `S3UserKeysStore` (`S3/`): the keys documents and the index, in the data bucket under `keys/`, written conditionally (`LocalStorageUserKeysStore`, in `Encryption/`, without a data bucket)

The wiring is `AddBlazwizSyncModule` (`src/Devoctomy.Blazwiz.UI.Sync/Extensions/SyncModuleServiceCollectionExtensions.cs`),
which adds the module's UI and Cognito parts and calls `AddBlazwizSync` (`src/Devoctomy.Blazwiz.Sync/Extensions/SyncServiceCollectionExtensions.cs`).
The template's `Program.cs` passes it the settings in `sync.json` and the `SyncProfileDefinition` classes in the app.

## Where things are kept

| What | Where | Key / name | Notes |
|---|---|---|---|
| The profile's files | IndexedDB **or** OPFS **or** a folder the user picked (per device, Settings) | `myapp_files` / `files`, OPFS `myapp_files/`, or the folder | Always ciphertext, with the profile's own keys |
| Files of a profile the user created | The same choice | `myapp_files` / `{profile id}`, OPFS `myapp_{profile id}/` | Remote `profiles/{profile id}/**` |
| OPFS and folder file details | IndexedDB | `myapp_sync` / `files_opfs`, `files_folder` | |
| Folder handle (for the picked folder) | IndexedDB | `myapp_folders` / `handles` | The browser asks for permission again after a restart |
| Which storage is in use, per profile and user | IndexedDB | `myapp_sync` / `storage` | Default IndexedDB |
| Sync index: per file `BaseETag`, `BaseHash`, `Dirty`, `Deleted`, `Revision`, `KeyId` | IndexedDB | `myapp_sync` / `files_index` | Keyed by profile + owner + file key |
| Sync event log | IndexedDB | `myapp_sync` / `events` | 30 days, 5000 events per user (`sync.json`) |
| "Sync turned off" flag | localStorage | `myapp_sync_disabled` | Survives reloads and sign outs |
| Whether persistent storage was asked for | localStorage | `myapp_storage_persistence` | |
| Scheduler state per profile (`Running`, `RunAgain`, `VerifyPending`, debounce timer) | Memory | `SyncScheduler.states` | Reset on start |
| Connectivity status (online, reason, last checked) | Memory | `ConnectivityService.status` | Reused for 30 seconds |
| Sync status (state, last error, pending count, last synced), worked out from each profile's last pass | Memory | `SyncService.status`, `SyncService.outcomes` | Shown in the status pill |
| AWS credentials and S3 client | Memory | see [authentication.md](authentication.md) | |
| A profile's master and file keys | Memory | `IDerivedKeyStore` `e2e:{profile id}:master`, `e2e:{profile id}:file:{key id}`, through its `E2eKeyRing` (`E2eKeyRings`, one ring per profile) | Only while the profile is unlocked |
| Each profile's keys document | S3 data bucket (localStorage `myapp_e2e_browser_keys_{profile id}_{user}` without a data bucket) | `{identityId}/keys/{profile id}` | Master key wrapped by the passphrase (Argon2id), file keys wrapped by the master key (one active, earlier ones retired), and `rotation_in_progress` while a passphrase change is unfinished |
| The throwaway file timed before a passphrase change | S3 data bucket | `{identityId}/scratch/{random}` | Random bytes, deleted straight away. No profile may match `scratch/` |
| Index of the user's profiles (ids, idle timeouts) | S3 data bucket (localStorage `myapp_e2e_browser_index_{user}` without a data bucket) | `{identityId}/keys/index` | This device keeps a copy in `myapp_e2e_index_{user}` |
| This device's copy of each keys document, and the master key wrapped by the passkey where allowed | localStorage | `myapp_e2e_keys_{profile id}_{user}` | For unlocking offline, and with the passkey ([prf-remember-me.md](prf-remember-me.md), diagram 4) |
| Labels of the profiles the user created | localStorage | `myapp_e2e_labels_{user}` | Encrypted with the passkey's `e2e-labels` key |
| Remote copy | S3 | `{identityId}/files/**` | `x-amz-meta-modified-at` holds the edit time |
| Cross-tab | Web Locks, BroadcastChannel | lock `myapp_sync-files`, channels `myapp_sync-storage` and `myapp_e2e-lock` | One pass per profile; storage moves and locks reach every tab |

### The file store stack

The app and the sync engine see different stacks over the same files (`SyncProfileRegistry`):

| Used by | Stack (outer to inner) |
|---|---|
| App code, `[FromKeyedServices("files")] ISyncFileStore` | `EncryptingSyncFileStore` → `TrackingSyncFileStore` → `SwitchableSyncFileStore` → IndexedDB / OPFS / folder |
| Sync engine (`SyncProfile.FileStore`), keyed `files:untracked` | `SwitchableSyncFileStore` → IndexedDB / OPFS / folder |

So the app reads and writes plaintext and every write marks the index, while the engine moves ciphertext between disk
and S3 without marking anything dirty. A profile that is locked still syncs: the engine never needs its keys.

## 1. Starting sync after sign in

`SyncSessionHandler` runs after the other handlers (`Order` 100), when the shell starts the session
([session-management.md](session-management.md), diagram 1).

```mermaid
sequenceDiagram
    autonumber
    participant H as SyncSessionHandler
    participant Conn as ConnectivityService
    participant Chan as SyncStorageChannel
    participant Mig as SyncStorageMigrationService
    participant Prof as SyncProtectedProfileService
    participant Mgr as E2eProtectedProfileManager
    participant Folder as SyncFolderAccessMonitor
    participant Sched as SyncScheduler
    box rgb(240,240,255) In memory
        participant Mem as Memory (.NET fields)
    end
    box rgb(255,245,230) Browser storage
        participant LS as localStorage
        participant IDB as IndexedDB
    end
    box rgb(235,250,235) AWS
        participant KS as S3 data bucket (keys/)
    end

    Note over H: OnShellLoadingAsync
    H->>Conn: InitializeAsync()
    Conn->>LS: GET myapp_sync_disabled
    Conn->>Mem: syncEnabled

    Note over H: OnSessionStartedAsync (in the background)
    H->>Chan: StartAsync() (BroadcastChannel myapp_sync-storage)
    H->>Mig: RecoverAsync()
    Mig->>IDB: GET myapp_sync/storage (finish or undo a storage move cut short)
    H->>Prof: LoadAsync()
    Prof->>Prof: Start the idle lock and the lock channel (myapp_e2e-lock)
    Prof->>Mgr: GetProfilesAsync()
    Mgr->>KS: GetObject "{identityId}/keys/index"
    alt Reached
        Mgr->>LS: SET myapp_e2e_index_{user} (this device's copy)
    else Offline
        Mgr->>LS: GET myapp_e2e_index_{user}
    end
    Prof->>Mem: Add the profiles the user created to the registry
    Prof->>LS: Forget profiles deleted on another device: their keys, label and files here
    Note over Prof: Every profile starts locked, and syncs its ciphertext while locked
    H->>Folder: Start(), CheckAsync()
    Folder->>IDB: GET myapp_folders/handles + permission state
    alt Folder storage and permission lost
        Folder->>Folder: Show the "Reconnect folder" banner, scheduler not started
        Note over Folder: Reconnecting starts the scheduler
    else Access fine
        H->>Sched: StartAsync()
        Sched->>Mem: states[profile] = {VerifyPending = true} for every profile
        Sched->>Sched: Subscribe to connectivity changes, BecameVisible, ProfileAdded and ProfileRemoved
        Sched->>Sched: RunAllAsync() (diagram 4)
    end
```

## 2. Setting up, unlocking and locking a profile

A profile set up in code (such as `files`) has no keys until the user sets its passphrase: the banner
(`E2eBanner`) asks for one, in Settings. A profile the user creates in Settings gets its keys as it is created. Until
a profile is unlocked, its files cannot be read or written, and `E2eUnlockPanel` (around the app's pages) asks for the
passphrase. The UI tests in `E2eEncryptionTests` cover setting, unlocking, locking and reloading.

```mermaid
sequenceDiagram
    autonumber
    actor User
    participant UI as Settings (E2eProfilesViewModel) / E2eUnlockPanelViewModel
    participant Prof as SyncProtectedProfileService
    participant Mgr as E2eProtectedProfileManager
    participant Doc as E2eKeysDocumentService
    participant Ring as E2eKeyRing (the profile's)
    box rgb(255,245,230) Browser storage
        participant LS as localStorage
    end
    box rgb(235,250,235) AWS
        participant KS as S3 data bucket (keys/)
    end

    rect rgb(245,245,245)
    Note over User,KS: Setting the passphrase (or creating a profile)
    User->>UI: Passphrase (at least 12 characters by default)
    UI->>Prof: SetUpAsync(scope, passphrase) / CreateAsync(passphrase)
    Prof->>Mgr: CreateKeysAsync / CreateAsync (a new GUID for a new profile)
    Mgr->>Doc: CreateAsync(passphrase, Argon2id costs)
    Doc->>Doc: New master key and file key, the master key wrapped by Argon2id(passphrase)
    Mgr->>KS: PutObject "{identityId}/keys/{profile id}", If-None-Match: * (never replaces existing keys)
    Mgr->>KS: GetObject, then PutObject "{identityId}/keys/index" with If-Match (adds the profile, and on 412 reads again and retries)
    Mgr->>LS: SET myapp_e2e_keys_{profile id}_{user} (copy of the document)
    Mgr->>Ring: Load(master key, file key)
    Note over Mgr: Unlocked. A created profile is added to the registry and syncs straight away
    end

    rect rgb(245,245,245)
    Note over User,KS: Unlocking with the passphrase
    User->>UI: Passphrase, Unlock
    UI->>UI: Sync the profile first (SyncProfileAsync, skipped offline)
    UI->>Mgr: UnlockAsync(scope, passphrase)
    alt Online
        Mgr->>KS: GetObject "{identityId}/keys/{profile id}" (with its ETag)
    else Offline (HttpRequestException)
        Mgr->>LS: GET myapp_e2e_keys_{profile id}_{user} (this device's copy)
    end
    Mgr->>Doc: UnwrapMasterKeyAsync (Argon2id, wrong passphrase fails the AES-GCM check)
    Mgr->>Ring: LoadFileKeys (each file key unwrapped by the master key)
    Mgr->>Mgr: Zero the master key copy
    opt The document was fetched from the bucket
        Mgr->>LS: SET myapp_e2e_keys_{profile id}_{user} (new copy, with its ETag)
    end
    Note over Mgr: LockStateChanged, pages reload
    end

    rect rgb(245,245,245)
    Note over User,KS: Locking: by the user, after the idle timeout, from another tab, or at sign out
    alt User locks it in Settings, or all of them with the lock indicator
        UI->>Mgr: Lock(scope) / LockAll()
    else No read or write for IdleTimeout (15 min by default)
        Note over Mgr: E2eIdleLock calls Lock(scope)
    else Locked in another tab
        Note over Prof: E2eLockChannel message, Prof calls Lock(scope)
    else Sign out
        Prof->>Mgr: LockAll() (SyncSessionHandler.OnSigningOutAsync calls Unload)
    end
    Mgr->>Ring: Clear() (keys zeroed and removed from DerivedKeyStore)
    Prof-)Prof: Post the lock on myapp_e2e-lock (unlocking is never sent)
    end
```

## 3. The app saves a file

```mermaid
sequenceDiagram
    autonumber
    participant App as App code
    participant EncFS as EncryptingSyncFileStore
    participant Ring as E2eKeyRing
    participant Track as TrackingSyncFileStore
    participant Sched as SyncScheduler
    box rgb(240,240,255) In memory
        participant Mem as Memory (.NET fields)
    end
    box rgb(255,245,230) Browser storage
        participant Files as File store (IndexedDB / OPFS / folder)
        participant IDB as IndexedDB myapp_sync
    end

    App->>EncFS: WriteAsync("files/report.json", plaintext)
    EncFS->>Ring: ActiveKeyId? (the profile's ring)
    alt Unlocked
        Ring->>Mem: TryGetFileKey(activeKeyId) from DerivedKeyStore
        EncFS->>EncFS: Encrypt with the file key, binding the profile id and the path
    else Locked
        EncFS-->>App: E2eLockedException (CannotEncrypt), nothing saved
    end
    EncFS->>Track: WriteAsync(key, ciphertext)
    Track->>Files: Write file
    Track->>IDB: GET files_index record (profile, owner, key)
    Track->>IDB: PUT record {Revision++, Dirty = true, Deleted = false, KeyId, Encrypted}
    Track->>Sched: RequestSync("files")
    Sched->>Mem: Cancel the previous debounce, start a 2 s debounce
    EncFS->>EncFS: Restart the profile's idle timer
    Note over Sched: Each save restarts the timer, so a burst of typing gives one pass
    Sched->>Sched: After 2 s: RunProfileAsync("files") (diagram 4)
```

Deleting works the same way, but leaves a tombstone: `Dirty = false`, `Deleted = true`, `DeletedAt = now`.

## 4. A sync pass

```mermaid
sequenceDiagram
    autonumber
    participant Sched as SyncScheduler
    participant Conn as ConnectivityService
    participant Lock as Web Locks
    participant Svc as SyncService
    participant Eng as SyncEngine
    participant Plan as SyncPlanner
    box rgb(240,240,255) In memory
        participant Mem as Memory (.NET fields)
    end
    box rgb(255,245,230) Browser storage
        participant LS as localStorage
        participant Files as File store (untracked)
        participant IDB as IndexedDB myapp_sync
    end
    box rgb(235,250,235) AWS
        participant S3 as S3 data bucket
    end

    Sched->>Mem: states[files].Running?
    alt Already running
        Sched->>Mem: RunAgain = true (single flight)
    else Not running
        Sched->>Mem: Running = true
        Sched->>Conn: CheckAsync()
        alt Checked less than 30 s ago and nothing switched
            Conn->>Mem: Return cached status
        else Cache stale
            Note over Conn: Checks in order, stopping at the first that fails
            Conn->>Mem: 1. Offline mode on? (reason OfflineMode, no network calls)
            Conn->>Mem: 2. Sync turned off? (reason SyncDisabled, no network calls)
            Conn->>Conn: 3. navigator.onLine (reason NoNetwork)
            Conn->>Mem: 4. AWS credentials and S3 client (reason NoCredentials, or Unreachable)
            Conn->>S3: 5. ListObjectsV2 MaxKeys 1, 5 s timeout (reason Unreachable)
            Conn->>Mem: status = {IsOnline, Reason, LastCheckedAt}
        end
        alt Offline
            Sched->>Svc: RefreshPendingCountAsync()
            Svc->>IDB: GET every profile's index records, count Dirty or Deleted
            Svc->>Mem: status.PendingCount
        else Online
            Sched->>Lock: navigator.locks.request("myapp_sync-files")
            Lock-->>Sched: Held (other tabs wait)
            Sched->>Mem: verify = VerifyPending, VerifyPending = false
            Sched->>Svc: SyncAsync("files", verify)
            Svc->>Mem: status.State = Syncing
            Svc->>Svc: Gates (ISyncGate) may hold the profile back
            Note over Svc: Locked or not, the engine only moves ciphertext
            Svc->>Eng: SyncAsync(profile, verify)
            Eng->>Files: List local files
            Eng->>S3: ListObjectsV2 prefix "{identityId}/files/"
            Eng->>IDB: GET all files_index records
            opt verify (first pass after sign in, or of a new profile)
                Eng->>Files: Read every file, SHA-256 hash
                Note over Eng: Catches a write whose index update was lost
            end
            Eng->>Plan: Plan(local, remote, index, hashes)
            Plan-->>Eng: One action per key (table below)
            loop Each action that is not None
                Eng->>Eng: Execute (diagram 5)
            end
            Eng->>IDB: PUT events (myapp_sync/events), or one "no change" record
            Eng-->>Svc: SyncResult
            Svc->>Mem: Record this profile's outcome, FilesChanged for the keys it changed
            Svc->>IDB: Count pending, prune old events
            Svc->>Mem: status = {Idle or Error from every profile's last pass, LastSyncedAt, PendingCount}
            Sched->>Lock: Release
        end
        Sched->>Mem: Running = false (loop again if RunAgain)
    end
```

### Planner decision table

`changed locally` = `Dirty`, `Deleted`, or (on a verify pass) the file's hash differs from `BaseHash`.
`changed remotely` = the S3 ETag differs from `BaseETag`.

| Local file | S3 object | Index record | Action |
|---|---|---|---|
| none | none | none | `None` |
| none | none | live or tombstone | `DropRecord` |
| none | exists | tombstone, has base, ETag = base | `DeleteRemote` |
| none | exists | tombstone, ETag ≠ base or no base | Conflict `DeleteEdit` |
| none | exists | none or no base | `DownloadNew` |
| none | exists | has base | `Download` (local copy went missing) |
| exists | none | none or no base | `UploadNew` |
| exists | exists | none or no base | `CompareContent` |
| exists | none | has base, not changed locally | `DeleteLocal` |
| exists | none | has base, changed locally | Conflict `EditDelete` |
| exists | exists | not changed either side | `None` |
| exists | exists | changed locally only | `Upload` |
| exists | exists | changed remotely only | `Download` |
| exists | exists | changed both sides | Conflict `EditEdit` |

Conflicts go to `NewestWinsConflictResolver`: the newer edit wins, comparing the local modified time (or `DeletedAt`)
with the remote `x-amz-meta-modified-at`.

## 5. Executing actions: upload, download, retry on a race

Every S3 write is conditional, so a change made by another device between the listing and the write is never
overwritten.

```mermaid
sequenceDiagram
    autonumber
    participant Svc as SyncService
    participant Eng as SyncEngine
    participant Plan as SyncPlanner
    box rgb(240,240,255) In memory
        participant Mem as Memory (.NET fields)
    end
    box rgb(255,245,230) Browser storage
        participant Files as File store (untracked)
        participant IDB as IndexedDB myapp_sync
    end
    box rgb(235,250,235) AWS
        participant S3 as S3 data bucket
    end

    rect rgb(245,245,245)
    Note over Eng,S3: Upload / UploadNew
    Eng->>Files: Read file (ciphertext)
    Eng->>S3: PutObject (If-Match: BaseETag, or If-None-Match: * when new) + modified-at metadata
    S3-->>Eng: New ETag
    Eng->>IDB: GET record (has it changed during the pass?)
    Eng->>IDB: PUT record {BaseETag, BaseHash, LastSyncedAt, clear Dirty only if Revision unchanged}
    end

    rect rgb(245,245,245)
    Note over Eng,S3: Download / DownloadNew
    Eng->>S3: GetObject
    S3-->>Eng: Content, ETag, modified-at
    Eng->>IDB: GET record
    alt Revision changed since the snapshot (edited locally during the pass)
        Eng->>Eng: Skip, the next pass sees both changes
    else Not edited locally
        Eng->>Files: Write file (no index marking, untracked store)
        Eng->>IDB: PUT record {BaseETag, BaseHash, KeyId from the file header}
        Eng->>Mem: Remember the changed key (FilesChanged, pages reload)
    end
    end

    rect rgb(245,245,245)
    Note over Eng,S3: Someone else changed the object since the listing
    Eng->>S3: PutObject / DeleteObject with If-Match
    S3-->>Eng: 412 Precondition Failed (SyncPreconditionFailedException)
    Eng->>Eng: Log "Changed in S3 during sync, checking again"
    Eng->>Files: Read file again
    Eng->>S3: HeadObject
    Eng->>IDB: GET record
    Eng->>Plan: PlanItem(...) for this key only
    Plan-->>Eng: New action (often a conflict)
    Eng->>Eng: Execute it once
    end

    rect rgb(245,245,245)
    Note over Eng,S3: S3 refuses the credentials (403)
    S3-->>Eng: SyncAccessDeniedException
    Eng-->>Svc: Pass fails
    Svc->>Mem: targetProvider.Invalidate(), AWS credentials and S3 client dropped
    Svc->>Eng: The profile's pass once more, with new credentials
    end
```

Any other per-file failure is counted and logged, and the pass carries on with the next file. A network failure stops
the pass and tells `ConnectivityService`, which marks the app offline.

## 6. Going offline and coming back

```mermaid
sequenceDiagram
    autonumber
    participant Browser as Browser events
    participant Conn as ConnectivityService
    participant Sched as SyncScheduler
    participant Svc as SyncService
    box rgb(240,240,255) In memory
        participant Mem as Memory (.NET fields)
    end
    box rgb(255,245,230) Browser storage
        participant IDB as IndexedDB myapp_sync
    end
    box rgb(235,250,235) AWS
        participant S3 as S3 data bucket
    end

    Svc->>S3: Any call during a pass
    S3--xSvc: Network error (SyncNetworkException)
    Svc->>Conn: ReportNetworkFailure()
    Conn->>Mem: status = {IsOnline = false, Reason = Unreachable}
    Conn->>IDB: PUT event WentOffline
    Conn->>Conn: Schedule a recheck (30 s, 1 min, 2 min, then every 5 min)

    Note over Mem,IDB: Edits keep going to the file store and index, the pill shows the pending count

    alt Recheck timer fires, or browser "online" event
        Browser-)Conn: online
        Conn->>S3: ListObjectsV2 MaxKeys 1
        S3-->>Conn: OK
        Conn->>Mem: status = {IsOnline = true}, stop rechecks
        Conn->>IDB: PUT event WentOnline
        Conn-)Sched: StatusChanged (back online)
        Sched->>Sched: RunAllAsync()
    else App comes back to the foreground
        Browser-)Sched: BecameVisible
        Sched->>Sched: RunAllAsync()
    end
```

## 7. Sync turned off and offline mode

Both stop all network calls. They differ in what they mean and how long they last.

```mermaid
sequenceDiagram
    autonumber
    actor User
    participant Pill as SyncPillViewModel / Settings / Sync page
    participant Conn as ConnectivityService
    participant OMS as OfflineModeService
    box rgb(240,240,255) In memory
        participant Mem as Memory (.NET fields)
    end
    box rgb(255,245,230) Browser storage
        participant LS as localStorage
        participant IDB as IndexedDB myapp_sync
    end

    rect rgb(245,245,245)
    Note over User,IDB: Sync off (remembered across reloads and sign outs)
    User->>Pill: Toggle sync off (or "Start with sync off" at sign in)
    Pill->>Conn: SetSyncEnabledAsync(false)
    Conn->>LS: SET myapp_sync_disabled (LocalStorageSyncEnabledStore)
    Conn->>Mem: syncEnabled = false
    Conn->>IDB: PUT event "Sync turned off"
    Conn->>Mem: status = {IsOnline = false, Reason = SyncDisabled}, no recheck timer
    end

    rect rgb(245,245,245)
    Note over User,IDB: Offline mode (memory only, ends the online session, see session-management.md)
    User->>OMS: Turn offline mode on (in Settings, via GoOfflineAsync)
    OMS->>Mem: reason = Chosen
    OMS-)Conn: Changed(true)
    Conn->>IDB: PUT event "Offline mode turned on"
    Conn->>Mem: status = {IsOnline = false, Reason = OfflineMode}
    end
```

## 8. Changing a passphrase

Changing a profile's passphrase rotates its keys and encrypts every file again, so anyone who had the old passphrase
and a copy of the keys document can read nothing written afterwards. It runs to the end: there is no skip. The earlier
file keys are kept in the keys document, retired, so a file not yet encrypted again still decrypts.

```mermaid
sequenceDiagram
    autonumber
    actor User
    participant UI as Settings (E2eProfilesViewModel)
    participant Rot as SyncKeyRotationService
    participant Sync as SyncService (diagram 4)
    participant Mgr as E2eProtectedProfileManager
    participant Doc as E2eKeysDocumentService
    participant Files as The profile's file store (EncryptingSyncFileStore)
    box rgb(235,250,235) AWS
        participant KS as S3 data bucket (keys/)
        participant S3 as S3 data bucket (files, scratch/)
    end

    rect rgb(245,245,245)
    Note over User,S3: Nothing changes yet
    User->>UI: Current and new passphrase, Change passphrase
    UI->>Rot: PrepareAsync(scope)
    Rot->>Sync: SyncAsync(profile), under the profile's lock
    alt Offline, or the pass failed
        Rot-->>UI: SyncKeyRotationException (Sync.Rotation.Offline / SyncFailed)
        UI-->>User: The change is refused
    end
    Rot->>S3: PutObject then DeleteObject "{identityId}/scratch/{random}" (random bytes, timed)
    Rot-->>UI: SyncKeyRotationEstimate (files, bytes, duration)
    UI-->>User: The estimate and the warning. Cancel, or go on
    end

    rect rgb(245,245,245)
    Note over User,S3: The change
    User->>UI: Change passphrase
    UI->>Rot: RotateAsync(scope, current, new, progress)
    Rot->>Mgr: ChangePassphraseAsync (through SyncProtectedProfileService)
    Mgr->>KS: GetObject "{identityId}/keys/{profile id}" (with its ETag)
    Mgr->>Doc: RotateAsync: new master key and active file key, earlier file keys wrapped again and retired, rotation_in_progress = true
    Mgr->>KS: PutObject with If-Match (on 412 reads again and starts over with new keys)
    Mgr->>Mgr: Load the new keys, and replace or remove this device's passkey copy of the master key
    loop Every file not under the active key
        Rot->>Files: ReadAsync (decrypts with the retired key), WriteAsync (encrypts with the active key, marks the index)
    end
    Rot->>Sync: SyncAsync(profile, progress): one pass uploads them, reporting each
    Rot-->>UI: Done: the profile is ready to complete
    end

    Note over UI: Sign out everywhere (user-events.md, section 6), which clears rotation_in_progress
```

The progress bar adds the two counts together: files encrypted again, then files uploaded
(`E2eStep.ReencryptFiles`, `E2eStep.UploadFiles`). If another device's edit wins a conflict during the upload, that file
comes back under the retired key, and the files are gone through again, up to `SyncKeyRotationService.MaxRounds`
times.

**A change cut short.** A closed tab or a lost connection after the keys are written leaves `rotation_in_progress` in
the keys document. `E2eProtectedProfileManager.IsRotationInProgress` is then true wherever the profile is unlocked,
with the new passphrase, and `E2eUnlockPanel` shows a warning and Finish passphrase change in place of the profile's
pages. `SyncKeyRotationService.FinishAsync` syncs, encrypts again only the files whose header does not name the
active key, uploads them, and the sign out everywhere follows as above.

**Without a data bucket** (local development and the UI tests) nothing is synced or uploaded: the files on the device
are encrypted again, and the placeholder sign-in signs out.

The UI tests in `NotesTests` cover a passphrase change and finishing one cut short. `ProtectedProfiles.feature` covers
two devices: the files in the bucket end up under the new key, and a change started on one device is finished on the
other.

## The template's notes example

The template's notes example (`--notes`) is an app of the framework, not part of it: its `notes` profile
(`template/MyApp.Portal.Application/Sync/NotesSyncProfile.cs`) is the default notebook, with its files in IndexedDB
`myapp_notes` / `files` (or OPFS `myapp_notes/`) and in S3 under `{identityId}/notes/**/*.md`. Each notebook the user
creates is a profile the user created (`NotebookService` calls `SyncProtectedProfileService.CreateAsync`), so it
syncs under `profiles/{profile id}/` like any other.
