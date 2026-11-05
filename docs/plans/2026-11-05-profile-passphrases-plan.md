# Plan: a separate passphrase for each encrypted profile

**Date:** 2026-11-05
**Status:** decisions made (D1 to D8); Phase 1 done

## Goal

Let an app give each encrypted profile its own keys, protected by its own passphrase, instead of one set of keys for
the whole account. Each profile is then a separate secure area: unlocking one does not unlock another, each can be
locked again on its own, and losing access to one does not affect the rest. A user can have several, and can create
and delete them.

## How it works today

End to end encryption is per account (see `Devoctomy.Blazwiz.Encryption`, `Sync/Encryption` and `UI.Sync`):

- One keys document per user, `keys.json`, in the Secrets Manager secret `<user pool sub>/keys`. It holds the Argon2id
  settings, the master key wrapped by the passphrase key, and the file keys wrapped by the master key. The IAM policy in
  the base stack allows only that secret name (`${aws:PrincipalTag/userSub}/keys-??????`).
- One file key (the "active" key) encrypts every file of every `UseEncryption()` profile. Nothing creates a second key
  or retires one (`FileKeyEntry.RetiredStatus` is never set).
- `IE2eKeyRing` is a singleton holding the keys in `IDerivedKeyStore` under fixed names (`E2eKeyNames`). The store is
  cleared when the login page loads after sign out. `IE2eKeyManager` has one `Status` for the account.
- `UseEncryption()` marks a profile as one that is encrypted when the user turns encryption on in Settings. Turning it
  on or off sweeps every such profile (`SyncEncryptionSweeper`). `E2eSyncGate` holds back every encrypted profile
  while this device cannot encrypt, so plaintext never replaces ciphertext.
- Each device keeps the master key wrapped by a key derived from the passkey (WebAuthn PRF), so it unlocks without the
  passphrase.
- Files are stored as ciphertext below `EncryptingSyncFileStore` (in IndexedDB, OPFS or the folder, and in S3), so the
  sync engine, storage moves and conflict handling only ever see ciphertext.
- The authenticated data of a file is its header only, not its path (L1 in the 2026-10-03 code review), so an
  encrypted file can be moved to another path and still decrypt.
- Profiles are fixed when the app starts: each is registered in DI under its name.

## Requirements

Derived from the goal. Each is numbered so the work below can refer to it.

- **R1. Keys per profile.** A profile can have its own master key and file keys, which encrypt only its files.
- **R2. A passphrase per profile.** Each profile's master key is wrapped by a key derived from that profile's own
  passphrase. The passphrase is never stored or sent anywhere.
- **R3. Always encrypted.** A profile with its own passphrase is encrypted from the moment it is created. There is no
  plaintext mode, no turning it off, and no sweep to encrypt or decrypt it.
- **R4. Locked by default.** A profile starts locked in every session and is unlocked explicitly. While locked, its
  files cannot be read or written, and app code is told so (an error code, not a crash).
- **R5. Lock on demand and automatically.** Each profile can be locked again at any time, all can be locked at once,
  and a profile locks itself after a period without use (configurable for each profile). Signing out locks all.
  Locking wipes the profile's keys from memory.
- **R6. Unlocked in this tab only.** Unlocking in one tab does not unlock other tabs (keys are never sent between
  tabs). Locking in one tab locks the profile in every tab of the app.
- **R7. Created and deleted by the user.** The user can create a profile and delete it. How many there are is not
  known when the app is built.
- **R8. Known only by an id.** A protected profile is identified only by a GUID, shown to the user as a string. No name
  or description is stored anywhere; remembering what an id represents is the user's responsibility (D8).
- **R9. Separate failure.** Forgetting one profile's passphrase loses only that profile. The app warns clearly that
  there is no recovery (D6).
- **R10. Change the passphrase.** Changing a profile's passphrase re-wraps its master key; files are not re-encrypted.
  The last write wins (see `docs/backlog/protected-profile-concurrent-writes.md`).
- **R11. Deleting destroys the keys.** Deleting a profile deletes its files (S3 and every local storage) and its keys
  document, so any copy of its ciphertext left behind (backups, old S3 versions) cannot be decrypted.
- **R12. Sync while locked.** A locked profile keeps syncing its ciphertext, so changes from other devices arrive and
  pending changes are uploaded, without unlocking it.
- **R13. Offline unlock.** A profile can be unlocked offline on a device that has synced it before, from a cached copy
  of its keys document.
- **R14. Device unlock is opt in.** Unlocking with the passkey instead of the passphrase is allowed only when the app
  allows it, and the user turns it on for that profile on that device. Off by default.
- **R15. Files are bound to their profile and path.** A file's ciphertext cannot be moved to another path or another
  profile and still decrypt (fixes L1 for these profiles).
- **R16. Every profile has its own passphrase.** Profiles set up in code and profiles users create alike have their own
  passphrase and keys. There is no passphrase for the whole account.
- **R17. Stronger defaults.** The passphrase rules (minimum length) and Argon2id costs can be set for each profile,
  and are stored in each keys document so they can be raised later.

## Design

### Key scopes

Introduce a **key scope**: one keys document, one passphrase, one set of keys in memory, one lock state. Today's
account encryption becomes one scope (`account`); each profile with its own passphrase is another (its GUID).
The existing pieces become scoped rather than duplicated:

| Today | Scoped |
|---|---|
| `IE2eKeyRing` (one set, fixed names in `IDerivedKeyStore`) | keys held per scope id (`<scope>/master`, `<scope>/file/<id>`), with `Lock(scope)` and `LockAll()` |
| `IE2eKeyManager` (one `Status`) | key management takes a scope; a status per scope. The account scope keeps today's states (`Disabled`, `NeedsSetup`, ...); a passphrase scope has `Locked` / `Unlocked` only |
| `IUserKeysStore` (secret `<sub>/keys`) | keyed by scope: `<sub>/keys` for the account, `<sub>/keys/<scope>` for the others (D2) |
| `IE2eLocalKeyStore` (`e2e_keys_<user>`) | keyed by scope, for the cached document (R13) and the optional device-wrapped key (R14) |
| `EncryptingSyncFileStore` (uses the active key) | uses the active key of its profile's scope |

`UseEncryption()` stays as the account scope. A protected profile always has its own scope; its idle timeout (R5),
device unlock (R14) and passphrase rules (R17) are its own settings, with framework defaults.

### Profiles the user creates (R7)

Each protected area is its own sync profile, with its own key scope (the profile id is the scope id), its own local
stores and index, its own remote folder and its own sync state. Nothing is shared between two of them except the
user's sign in. A profile is never split into several scopes, and two profiles never share a scope.

Profiles are registered in DI when the app starts. Protected profiles are not: the user creates them while the app
runs, and nothing about them is declared in the app's code.

- **Everything a profile needs comes from the profile itself.** Its remote folder follows from its GUID
  (`profiles/<id>/**`). Its storage on each device is the user's choice on that device, as today. Its Argon2id costs
  are in its keys document. Its own settings (e.g. how long until it locks itself) are in the index (see below). The
  framework's defaults apply until the user changes them.
- **A registry of profiles.** Holds the profiles configured at startup and the ones the user creates, and raises an
  event when one is added or removed. `SyncService`, `SyncScheduler`, `SyncStorageResolver`, `E2eSyncGate`, the Sync
  page and Settings work over the registry instead of the fixed list in `SyncOptions`. App code gets a protected
  profile's file store from the registry by id rather than as a keyed service.
- **Found again on every device.** At sign in, the registry adds every protected profile the user has (D3), so a
  profile created on one device appears on the others.
- **Separate local storage per profile.** Each protected profile has its own object stores, named after its id: its
  index (`sync` / `<id>_index`) and its files (`files` / `<id>`). `indexeddb.js` adds a missing object store to an
  existing database at any time (it opens the next version), so no database of its own is needed. In OPFS its files
  are under a directory named after its id, and in the picked folder under `profiles/<id>/`, as in S3.

### The index (R7, R8, D3)

Each user has one index secret, `<sub>/keys/index`: a JSON document listing their protected profiles, each by its
GUID with its own settings. It is read at sign in to find the user's profiles on a new device, and updated when a
profile is created, deleted or its settings change; the last write wins (see
`docs/backlog/protected-profile-concurrent-writes.md`). It holds no keys and no names.

The app never lists secrets (`secretsmanager:ListSecrets`): every secret it reads has a name it already knows, so the
per-user IAM policy is enough and nothing about other users can be seen.

Paths inside a profile are visible in S3 and on the device, as for all synced files today (D4).

### Locking (R4, R5, R6, R12)

- The encrypting store refuses reads and writes for a locked scope with an `E2eLockedException` and a coded error, so
  app code and the UI can ask the user to unlock.
- Because these scopes are never plaintext (R3), plaintext can never replace their ciphertext. They are left out of
  `E2eSyncGate` and of the account sweeps, and sync while locked (R12).
- An idle timer per scope, reset by reads and writes through the store, locks it. The timer uses `TimeProvider`.
- Lock events go over a `BroadcastChannel` (as storage moves already do with `SyncStorageChannel`), so every tab locks.
  Unlock events are not sent (R6).
- Sign out locks every scope, from a session lifecycle handler (`OnSigningOutAsync`). Today the keys are wiped only
  when the login page next loads (`LoginViewModel` clears `IDerivedKeyStore`; M5 in the 2026-10-03 code review), which
  is not soon enough for R5.

### File format (R15)

Add the scope id and the file's key (path) to the authenticated data for files in passphrase scopes, and record the
scope id in the header so a file can be matched to its keys document without its path. This is a new format version.
Before 1.0 there is nothing to migrate.

### Memory (R5)

Keys are byte arrays and are zeroed when a scope locks, as today. Two limits to record in the docs: the passphrase
arrives as a .NET string from an input binding (and passes through the DOM), which cannot be wiped; and the .NET
runtime may copy arrays before they are zeroed. This is the same limit as account encryption today.

### AWS (R11, D2, D3)

A secret per profile, and the index secret, need the base stack's IAM policy to allow
`${aws:PrincipalTag/userSub}/keys/*` as well as the account secret. No new actions, and no `ListSecrets`. Deleting a
profile deletes its secret with `ForceDeleteWithoutRecovery`. Each secret costs about USD 0.40 a month.

### UI

What the user sees, in the framework (`UI.Sync`, D5):

- **Settings, End to end encryption** (the account scope) stays where it is, and lists the profiles it protects by
  their display names, e.g. "Protects: Notes". This needs a display name for each configured profile from resources
  (MP6 in the 2026-11-05 multiple sync profiles review). When the app also has protected profiles, the section says
  they have their own passphrases and that turning account encryption on or off does not touch them.
- **Settings lists the user's protected profiles** (for now), by id. When the user signs in on a new device, the
  framework reads the index and lists the profiles that need their passphrase on this device. Each can be unlocked
  there, and the list has New, Change passphrase, Delete and Lock all.
  - **New**: passphrase and confirmation, the Argon2id progress panel (`Argon2idPanel`), and an "I understand this
    cannot be recovered" checkbox, as account encryption has today (R9). Shows the new profile's id.
  - **Delete**: confirmed by typing the id, and says the data is destroyed on every device (R11).
- **Unlocking where the content is.** Opening a locked profile shows an unlock panel in place of its content:
  passphrase, a wrong passphrase error, and "Unlock with passkey on this device" only when allowed (R14). A framework
  component, so any page can show it when the encrypting store reports the profile locked.
- **A lock indicator in the shell**, beside the sync pill: hidden when nothing is unlocked, otherwise e.g.
  "2 unlocked", with Lock all in its menu. Auto-lock is silent; a page showing a profile that locks switches to the
  unlock panel.
- **The encryption banner** (`E2eBanner`) stays about the account scope only.
- **The Sync page** labels a protected profile's log entries with its id.
- **The template's demo** shows only notes.

## Decisions

| Id | Question | Decision |
|---|---|---|
| D1 | How are user-created profiles modelled? | Each is its own sync profile, created and deleted at runtime, with nothing shared with any other profile |
| D2 | Where is each profile's keys document kept? | Its own secret, `<sub>/keys/<id>` (needs the IAM change) |
| D3 | How does the app find a user's protected profiles? | An index secret, `<sub>/keys/index`, a JSON document of the user's profiles. No listing of secrets |
| D4 | Are paths inside a profile encrypted? | No. Paths stay as they are for all synced files |
| D5 | Does the framework provide UI? | Yes. For now Settings lists the user's protected profiles, including those needing their passphrase on a new device. The template's demo shows only notes |
| D6 | Is there any recovery? | No. Recorded as a request: `docs/requests/2026-11-05-protected-profile-recovery.md` |
| D7 | Replacing a profile's file key? | Not added. Changing the passphrase stays (R10) |
| D9 | Where do the settings of profiles users create come from? | Defaults the developer sets in code |
| D10 | What id does a profile set up in code have? | One set in code; the developer chooses a fixed id or a newly generated one. The notes example has a fixed id, so it can be tested. Profiles users create get new GUIDs |
| D8 | How is a protected profile named? | Only by its GUID, shown as a string. The user remembers what it represents. The notes sample has one configured profile, so it cannot be anything else |

## Work

Each phase leaves everything passing (all four suites and `test-template.ps1`).

### Phase 1: key scopes, with the account as the only scope

- [x] Key ring holds keys per scope, with lock per scope and lock all.
- [x] Keys store and local key store keyed by scope; account scope keeps `<sub>/keys`.
- [x] Key manager takes a scope; status per scope.
  - Done differently: the account's key manager stays the account's; protected profiles have their own manager
    (Phase 2), with a locked or unlocked status per profile.
- [x] No behaviour change for apps; existing tests still pass, new tests for two scopes in the key ring and stores.
  - Each device asks for the account passphrase once after updating, as its local key entry is renamed.

### Phase 2: passphrase scopes

- [x] Each profile's settings (idle timeout, device unlock allowed, passphrase rules, Argon2id costs): set in code for
      profiles set up in code, and defaults set in code for profiles users create (R5, R14, R17).
- [x] A registry of profiles added and removed at runtime, with no profile declared in code; the sync service,
      scheduler, storage resolver and gate work over the registry (R7). Protected profiles found again at sign in.
- [x] Local stores per runtime profile: IndexedDB databases, OPFS and folder directories named after the profile id.
  - Done differently: each profile's own object stores, named after its id, in the existing databases.
- [x] The index secret: read at sign in, updated on create, delete and settings changes (D3).
- [x] Create a profile: GUID, new keys document (conditional create), index entry, local stores (R1, R2, R7, R8).
- [x] Unlock with the passphrase, online and offline (R4, R13); optional device unlock (R14).
- [x] Encrypting store: refuse when locked, coded errors, idle timer (R4, R5).
- [x] Leave passphrase scopes out of `E2eSyncGate` and the account sweeps; they sync while locked (R3, R12, R16).
- [x] Format version with scope and path in the authenticated data (R15).
- [x] Change passphrase (R10).
- [x] Delete a profile: files on every storage, cached keys, its secret and its index entry (R11).
  - Files in the picked folder are deleted only when it is the storage in use.
- [x] Lock in every tab over a broadcast channel (R6); lock all from a session lifecycle handler on sign out.
- [x] Profiles set up in code (like notes) are protected profiles: each has an id set in code, its own passphrase and
      keys, and the user sets its passphrase the first time (R16, D10).
- [ ] Remove the account-wide passphrase: `E2eKeyManager`, the sweeps, the gate, and its Settings section and banner
      (R16).

### Phase 3: AWS

- [ ] Base stack IAM policy allows `<sub>/keys/*` (D2, D3), with the stack's own tests.
- [ ] `test-auth.ps1` checks a user can create, read and delete a profile secret and the index, and cannot touch
      another user's.

### Phase 4: UI (D5, see "UI" above)

- [ ] Profile display names from resources (MP6), and the account section listing the profiles it protects.
- [ ] Unlock panel component, in place of a locked scope's content (R4, R14).
- [ ] Settings list of protected profiles by id, including those needing their passphrase on this device (D5).
- [ ] Create, change passphrase and delete dialogs, with the no recovery acknowledgement (R9, R11).
- [ ] Lock indicator beside the sync pill, with Lock all (R5).
- [ ] Sync page labels protected profiles by id (D8).

### Phase 5: proving it

- [ ] Integration scenarios: two scopes on two devices; a locked scope receives and sends changes; a file moved
      between scopes or paths fails to decrypt; deleting a scope leaves nothing decryptable; passphrase change on one
      device while another is unlocked.
- [ ] The template's demo shows only notes (D5).
- [ ] Docs: package READMEs, `CHANGELOG.md`, the sync diagrams (`docs/diagrams/sync.md`, `sync-framework.md`).
- [ ] Upgrade test: `./templatepack/test-template.ps1 -UpgradeFrom latest` (public API and the base stack change).

## Out of scope

- Sharing a profile with another user.
- Encrypting paths (D4).
- Replacing a profile's file key (D7).
- Recovery (D6): see `docs/requests/2026-11-05-protected-profile-recovery.md`.
