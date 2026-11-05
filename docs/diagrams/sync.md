# Sync

Notes are plain markdown files kept on the device and synced with the user's prefix in the S3 data bucket. Every
change is recorded locally first, so the app works the same offline. A pass compares three things per file (the local
file, the S3 object, and the index record from the last sync) and decides what to do.

Main classes (all under `blazwiz.Core/Sync/` unless noted):

- `SyncScheduler`: when passes run (start, change debounce, back online, back to foreground, Sync now)
- `ConnectivityService` (`Connectivity/`): whether a pass may touch the network
- `SyncService`: runs passes per profile, gates, status, pending count
- `SyncEngine` + `SyncPlanner`: one pass, the decision table
- `TrackingSyncFileStore`: marks the index when the app writes a file
- `EncryptingSyncFileStore` (`Encryption/`): plaintext in the app, ciphertext on disk and in S3, with the profile's
  own keys
- `SyncProtectedProfileService` (`Encryption/`) and `E2eProtectedProfileManager` (Encryption package): each profile's
  keys and passphrase, the profiles the user creates, unlocking and locking
- `S3SyncRemoteStore` (`S3/`), with `CognitoS3TargetProvider` (`blazwiz.Portal.Application/Services/`) for the client
- `WebLockSyncLockService`: one pass per profile across all tabs

Wiring is in `blazwiz.Core/Extensions/SyncServiceCollectionExtensions.cs` and the `notes` profile is configured in
`blazwiz.Portal.Application/Extensions/ServiceCollectionExtensions.cs`.

## Where things are kept

| What | Where | Key / name | Notes |
|---|---|---|---|
| Note files | IndexedDB **or** OPFS **or** a folder the user picked (per device, Settings) | `blazwiz_notes` / `files`, OPFS `blazwiz_notes/`, or the folder | Always ciphertext, with the profile's own keys |
| Folder handle (for the picked folder) | IndexedDB | `blazwiz_folders` / `handles` | Browser asks again for permission after a restart |
| Which storage is in use, per user | IndexedDB | `blazwiz_sync` / `storage` | Default IndexedDB |
| Sync index: per file `BaseETag`, `BaseHash`, `Dirty`, `Deleted`, `Revision`, `KeyId` | IndexedDB | `blazwiz_sync` / `notes_index` | Keyed by profile + owner + file key |
| Sync event log | IndexedDB | `blazwiz_sync` / `events` | 30 days, 5000 events per user |
| "Sync turned off" flag | localStorage | `blazwiz_sync_disabled` | Survives reloads and sign outs |
| Scheduler state per profile (`Running`, `RunAgain`, `VerifyPending`, debounce timer) | Memory | `SyncScheduler.states` | Reset on start |
| Connectivity status (online, reason, last checked) | Memory | `ConnectivityService.status` | Cached 30 seconds |
| Sync status (state, last error, pending count, last synced) | Memory | `SyncService.status` | Shown in the status pill |
| AWS credentials and S3 client | Memory | see [authentication.md](authentication.md) | |
| E2E master and file keys, per profile | Memory | `DerivedKeyStore` via `E2eKeyRings` (one ring per profile) | Only while the profile is unlocked |
| Each profile's keys document | Secrets Manager (localStorage without Cognito) | `{sub}/keys/{profile id}` | Wrapped by its passphrase (Argon2id) |
| Index of the user's profiles | Secrets Manager (localStorage without Cognito) | `{sub}/keys/index` | Ids only; this device keeps a copy |
| This device's copy of each keys document | localStorage | `e2e_keys_{profile id}_{user}` | For unlocking offline, and with the passkey where allowed |
| Remote copy | S3 | `{identityId}/notes/**/*.md` | `x-amz-meta-modified-at` holds the edit time |

### The file store stack

The app and the sync engine see different stacks over the same files:

| Used by | Stack (outer to inner) |
|---|---|
| App code (`NoteService`), keyed `notes` | `EncryptingSyncFileStore` → `TrackingSyncFileStore` → `SwitchableSyncFileStore` → IndexedDB / OPFS / folder |
| Sync engine (`SyncProfile.FileStore`), keyed `notes:untracked` | `SwitchableSyncFileStore` → IndexedDB / OPFS / folder |

So the app reads and writes plaintext and every write marks the index, while the engine moves ciphertext between disk
and S3 without marking anything dirty.

## 1. Starting sync after sign in

```mermaid
sequenceDiagram
    autonumber
    participant Layout as MainLayoutViewModel
    participant Chan as SyncStorageChannel
    participant Mig as SyncStorageMigrationService
    participant Prof as SyncProtectedProfileService
    participant Folder as SyncFolderService
    participant Sched as SyncScheduler
    participant Conn as ConnectivityService
    box rgb(240,240,255) In memory
        participant Mem as Memory (.NET fields)
    end
    box rgb(255,245,230) Browser storage
        participant LS as localStorage
        participant IDB as IndexedDB
    end
    box rgb(235,250,235) AWS
        participant SM as Secrets Manager
    end

    Layout->>Conn: InitializeAsync()
    Conn->>LS: GET blazwiz_sync_disabled
    Conn->>Mem: syncEnabled
    Layout-)Layout: LoadAwsCredentialsAsync() (authentication.md, diagram 3)
    Layout->>Chan: StartAsync() (BroadcastChannel "blazwiz-sync-storage")
    Layout->>Mig: RecoverAsync()
    Mig->>IDB: GET blazwiz_sync/storage (finish or undo a storage move cut short)
    Layout->>Prof: LoadAsync()
    Prof->>SM: GetSecretValue "{sub}/keys/index" (this device's copy when offline)
    Prof->>Mem: Add the profiles the user created to the registry
    Prof->>LS: Forget the keys of profiles deleted on another device, and delete their files here
    Note over Prof: Every profile starts locked, and syncs its ciphertext while locked
    Layout->>Folder: NeedsReconnectAsync()
    Folder->>IDB: GET blazwiz_folders/handles + permission state
    alt Folder storage and permission lost
        Layout->>Layout: Show "Reconnect folder" banner, scheduler not started
    else Access fine
        Layout->>Sched: StartAsync()
        Sched->>Mem: states[profile] = {VerifyPending = true}
        Sched->>Sched: Subscribe to connectivity changes and BecameVisible
        Sched->>Sched: RunAllAsync() (diagram 3)
    end
```

## 2. The app saves a note

```mermaid
sequenceDiagram
    autonumber
    participant Notes as NoteService
    participant EncFS as EncryptingSyncFileStore
    participant Ring as E2eKeyRing
    participant Track as TrackingSyncFileStore
    participant Sched as SyncScheduler
    box rgb(240,240,255) In memory
        participant Mem as Memory (.NET fields)
    end
    box rgb(255,245,230) Browser storage
        participant Files as File store (IndexedDB / OPFS / folder)
        participant IDB as IndexedDB blazwiz_sync
    end

    Notes->>EncFS: WriteAsync("notes/foo.md", plaintext)
    EncFS->>Ring: ActiveKeyId? (the notes profile's ring)
    alt Unlocked
        Ring->>Mem: TryGetFileKey(activeKeyId) from DerivedKeyStore
        EncFS->>EncFS: Encrypt with the file key, binding the profile id and path; reset the idle timer
    else Locked
        EncFS-->>Notes: E2eLockedException, nothing saved
    end
    EncFS->>Track: WriteAsync(key, bytes)
    Track->>Files: Write file
    Track->>IDB: GET notes_index record (profile, owner, key)
    Track->>IDB: PUT record {Revision++, Dirty = true, Deleted = false, KeyId, Encrypted}
    Track->>Sched: RequestSync("notes")
    Sched->>Mem: Cancel previous debounce, start a 2 s debounce
    Note over Sched: Each save restarts the timer, so a burst of typing gives one pass
    Sched->>Sched: After 2 s: RunProfileAsync("notes") (diagram 3)
```

Deleting works the same way, but leaves a tombstone: `Dirty = false`, `Deleted = true`, `DeletedAt = now`.

## 3. A sync pass

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
        participant IDB as IndexedDB blazwiz_sync
    end
    box rgb(235,250,235) AWS
        participant S3 as S3 data bucket
    end

    Sched->>Mem: states[notes].Running?
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
            Conn->>LS: 2. Sync turned off? (reason SyncDisabled, no network calls)
            Conn->>Conn: 3. navigator.onLine (reason NoNetwork)
            Conn->>Mem: 4. AWS credentials / S3 client (reason NoCredentials or Unreachable)
            Conn->>S3: 5. ListObjectsV2 MaxKeys 1, 5 s timeout (reason Unreachable)
            Conn->>Mem: status = {IsOnline, Reason, LastCheckedAt}
        end
        alt Offline
            Sched->>Svc: RefreshPendingCountAsync()
            Svc->>IDB: GET all notes_index records, count Dirty or Deleted
            Svc->>Mem: status.PendingCount
        else Online
            Sched->>Lock: navigator.locks.request("blazwiz-sync-notes")
            Lock-->>Sched: Held (other tabs wait)
            Sched->>Mem: verify = VerifyPending, VerifyPending = false
            Sched->>Svc: SyncAsync("notes", verify)
            Svc->>Mem: status.State = Syncing
            Note over Svc: Locked or not, the engine only moves ciphertext
            Svc->>Eng: SyncAsync(profile, verify)
            Eng->>Files: List local files
            Eng->>S3: ListObjectsV2 prefix "{identityId}/notes/"
            Eng->>IDB: GET all notes_index records
            opt verify (first pass after sign in)
                Eng->>Files: Read every file, SHA-256 hash
                Note over Eng: Catches a write whose index update was lost
            end
            Eng->>Plan: Plan(local, remote, index, hashes)
            Plan-->>Eng: One action per key (table below)
            loop Each action that is not None
                Eng->>Eng: Execute (diagram 4)
            end
            Eng->>IDB: PUT events (blazwiz_sync/events), or one "no change" record
            Eng-->>Svc: SyncResult
            Svc->>IDB: Count pending, prune old events
            Svc->>Mem: status = {Idle or Error, LastSyncedAt, PendingCount}
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
| none | none | none or live | `None` / `DropRecord` |
| none | none | tombstone | `DropRecord` |
| none | exists | tombstone, ETag = base | `DeleteRemote` |
| none | exists | tombstone, ETag ≠ base | Conflict `DeleteEdit` |
| none | exists | none or no base | `DownloadNew` |
| none | exists | has base | `Download` (local copy went missing) |
| exists | none | no base | `UploadNew` |
| exists | exists | no base | `CompareContent` |
| exists | none | has base, not changed locally | `DeleteLocal` |
| exists | none | has base, changed locally | Conflict `EditDelete` |
| exists | exists | not changed either side | `None` |
| exists | exists | changed locally only | `Upload` |
| exists | exists | changed remotely only | `Download` |
| exists | exists | changed both sides | Conflict `EditEdit` |

Conflicts go to `NewestWinsConflictResolver`: the newer edit wins, comparing the local modified time (or `DeletedAt`)
with the remote `x-amz-meta-modified-at`.

## 4. Executing actions: upload, download, retry on a race

Every S3 write is conditional, so a change made by another device between the listing and the write is never
overwritten.

```mermaid
sequenceDiagram
    autonumber
    participant Eng as SyncEngine
    participant Plan as SyncPlanner
    box rgb(240,240,255) In memory
        participant Mem as Memory (.NET fields)
    end
    box rgb(255,245,230) Browser storage
        participant Files as File store (untracked)
        participant IDB as IndexedDB blazwiz_sync
    end
    box rgb(235,250,235) AWS
        participant S3 as S3 data bucket
    end

    rect rgb(245,245,245)
    Note over Eng,S3: Upload / UploadNew
    Eng->>Files: Read file (ciphertext if E2E is on)
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
        Eng->>Mem: Remember changed key (FilesChanged event, pages reload)
    end
    end

    rect rgb(245,245,245)
    Note over Eng,S3: Someone else changed the object since the listing
    Eng->>S3: PutObject / DeleteObject with If-Match
    S3-->>Eng: 412 Precondition Failed
    Eng->>IDB: Log "Changed in S3 during sync, checking again"
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
    Eng-->>Eng: Pass fails, SyncService calls targetProvider.Invalidate()
    Eng->>Mem: AWS credentials and S3 client dropped
    Eng->>S3: Whole profile retried once with new credentials
    end
```

Any other per-file failure is counted and logged, and the pass carries on with the next file. A network failure stops
the pass and tells `ConnectivityService`, which marks the app offline.

## 5. Going offline and coming back

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
        participant IDB as IndexedDB blazwiz_sync
    end
    box rgb(235,250,235) AWS
        participant S3 as S3 data bucket
    end

    Svc->>S3: Any call during a pass
    S3--xSvc: Network error (SyncNetworkException)
    Svc->>Conn: ReportNetworkFailure()
    Conn->>Mem: status = {IsOnline = false, Reason = Unreachable}
    Conn->>IDB: PUT event WentOffline
    Conn->>Conn: Schedule a recheck (backoff, attempt count in memory)

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

## 6. Sync turned off and offline mode

Both stop all network calls. They differ in what they mean and how long they last.

```mermaid
sequenceDiagram
    autonumber
    actor User
    participant Layout as MainLayoutViewModel
    participant Conn as ConnectivityService
    participant OMS as OfflineModeService
    box rgb(240,240,255) In memory
        participant Mem as Memory (.NET fields)
    end
    box rgb(255,245,230) Browser storage
        participant LS as localStorage
        participant IDB as IndexedDB blazwiz_sync
    end

    rect rgb(245,245,245)
    Note over User,IDB: Sync off (remembered across reloads and sign outs)
    User->>Layout: Toggle sync off
    Layout->>Conn: SetSyncEnabledAsync(false)
    Conn->>LS: SET blazwiz_sync_disabled = True
    Conn->>Mem: syncEnabled = false
    Conn->>IDB: PUT event "Sync turned off"
    Conn->>Mem: status = {IsOnline = false, Reason = SyncDisabled}, no recheck timer
    end

    rect rgb(245,245,245)
    Note over User,IDB: Offline mode (memory only, ends the online session, see session-management.md)
    User->>OMS: Turn offline mode on (via GoOfflineAsync)
    OMS->>Mem: reason = Chosen
    OMS-)Conn: Changed(true)
    Conn->>IDB: PUT event "Offline mode turned on"
    Conn->>Mem: status = {IsOnline = false, Reason = OfflineMode}
    end
```
