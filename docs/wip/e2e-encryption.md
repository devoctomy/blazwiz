# E2E encryption for synced files

Work in progress plan for branch `e2e2ncryption`. Covers the original spec plus the answers from the questions round.

## Goal

Demonstrate correct end to end encryption of synced file contents in a blazwiz project. File contents are encrypted on the device, synced as ciphertext and only decrypted inside the app. Saving decrypted copies out of the app is a later feature.

## Decisions

- **Encrypted on the device.** Local stores (IndexedDB, OPFS, LocalFolder) hold ciphertext. Files in a LocalFolder are unreadable outside the app, which is intended.
- **Only file contents are encrypted.** Keys/paths (S3 keys, index) and folder markers (`.../`) stay plaintext. This leaks metadata, which a later version will address.
- **Hardware PRF is required.** Encryption can only be enabled, and encrypted files only read, in a session signed in with the WebAuthn passkey (PRF). Without it, sync and file access are blocked, with a warning explaining that hardware PRF is essential to keep their data secure. There is no fallback that asks for the passphrase each session.
- **No passphrase recovery.** The UI says so clearly before the user confirms enabling or changing the passphrase.
- **keys.json lives in Secrets Manager.** All reads, writes and deletes go through `IUserKeysStore` (implemented only by `SecretsManagerUserKeysStore`). It is never stored with the synced files.
- **Offline use.** A local copy of keys.json is cached on the device and refreshed every time we're online. It is all wrapped, so it is not wrapped again.
- **Argon2id uses BouncyCastle** (`Argon2BytesGenerator`, already referenced), parallelism 1. Release: 512 MiB, 2 iterations. Debug: about 19 MiB, 2 iterations (it only has to show derivation works). Build settings only apply to new keys.json files; deriving always reads the `kdf` values from keys.json.
- **Template.** Everything here is part of Notes, behind `IncludeNotes`.
- **Core first.** Anything not specific to this app goes in Core: key management, keys.json model, argon2, AEAD, file format, encrypting store, migrations. Portal.Application keeps only what needs app services (Secrets Manager, local storage, UI view models).

## Keys

| Key | Source | Stored | Wraps / encrypts |
| --- | --- | --- | --- |
| Passphrase key | Argon2id(passphrase, kdf from keys.json) | Never. Only exists while enabling, changing passphrase, disabling or setting up a device. | Master key, in keys.json |
| Master key | 32 random bytes | keys.json (wrapped by passphrase key); this device's local storage (wrapped by `e2e-kek`) | File keys, in keys.json |
| File key | 32 random bytes | keys.json by id, wrapped by master key, key id as associated data | File contents |
| `e2e-kek` | Derived from PRF (`.AddKey("e2e-kek")`) | Never, in memory only | Master key, local copy only |

Raw master key and file keys only ever live in `IDerivedKeyStore`, under `e2e-master` and `e2e-file:{keyId}`. One active file key for all files; the format allows more so it can be rotated later (`active` for new files, `retired` decrypt only).

## keys.json

As in the spec: `version`, `kdf` (`alg`, `salt`, `memory_kib`, `iterations`, `parallelism`, `key_len`), `master_key` (`alg`, `nonce`, `wrapped`), `keys` by id (`alg`, `nonce`, `wrapped`, `created`, `status`). All binary values base64, snake_case names. Every nonce is 12 new random bytes each time something is wrapped. A failed GCM tag check when unwrapping `master_key` means the passphrase is wrong. Key ids look like `k_` plus a random base32 suffix.

## Encrypted file format

```
magic     "E2EE" (ASCII, so encrypted files are easy to spot)
version   1 byte (1)
keyIdLen  1 byte
keyId     UTF-8
nonce     12 bytes
ciphertext + 16 byte GCM tag
```

The whole header is the associated data, so it is readable but can't be altered. The file says which key it needs; the index columns are only for quick lookup.

## Components

### Core

- `IUserKeysStore` moves here from Portal.Application, with a new `Task<bool> TryCreateAsync(string value, CancellationToken ct)` that only creates and returns `false` if keys.json already exists. `SecretsManagerUserKeysStore` stays in Portal.Application (it needs `AWSSDK.SecretsManager`, Cognito auth and credentials).
- `IPassphraseKeyDerivationService` / BouncyCastle Argon2id implementation, plus `Argon2idOptions` holding the build defaults (Debug/Release via `#if DEBUG`).
- `IAeadCipher` / BouncyCastle AES-256-GCM implementation with associated data support (the existing `ICryptoService` has no AAD and takes base64 keys).
- `IKeyWrapService`: wrap/unwrap a key into `{ alg, nonce, wrapped }`, with optional associated data.
- `UserKeysDocument` model and `IUserKeysDocumentSerializer`.
- `IEncryptedFileFormat`: write/parse the header, `IsEncrypted(bytes)`, encrypt/decrypt with a key.
- `IE2eLocalKeyStore` (interface only, like `IOfflineModeStore`): this device's master key wrapped by `e2e-kek`, plus the cached keys.json, per user.
- `IE2eEncryptionService`: the state machine and operations below. Holds the active key id, puts raw keys in `IDerivedKeyStore`, raises a state changed event.
- `EncryptingSyncFileStore`: sits between `TrackingSyncFileStore` and the untracked store. Reads decrypt when the header is present (throws a specific locked exception if the key isn't held). Writes encrypt when encryption is unlocked, and are refused when the account has encryption but this device is locked. Folder markers pass straight through.
- `ISyncEncryptionMigrator`: resumable encrypt and decrypt sweeps per profile (below).
- `SyncProfileBuilder.UseEncryption()`: opt a profile in. Notes opts in.
- `SyncIndexRecord`: new `Encrypted` (bool) and `KeyId` (string?) columns. The sync engine sets them from the content header whenever it records a sync (upload or download), and the tracking store sets them on local writes.
- `SyncFileInfo`: new `EncryptionKeyId`, filled from the index by `TrackingSyncFileStore.ListAsync`, for the locks on the Files page.
- Sync gate: the sync service skips a profile while encryption is blocked (no PRF) or the device needs setting up.

The sync engine keeps using the untracked store, so it moves ciphertext bytes as they are and never needs keys.

### Portal.Application / Portal

- `LocalStorageE2eLocalKeyStore` over `ILocalStorageService`, keyed per user.
- `SecretsManagerUserKeysStore.TryCreateAsync` (`CreateSecret` only, `false` on `ResourceExistsException`).
- `e2e-kek` added next to the remember-me key in `ServiceCollectionExtensions`.
- Settings → Sync panel: new **E2E encryption** section.
- Settings: new **Argon2id** panel.
- Files page: lock icons.
- Layout warning banner for the blocked and needs-setup states.
- Unlock on sign in (normal startup) once PRF keys are derived, before sync runs.

## States

| State | Meaning | UI |
| --- | --- | --- |
| `Unavailable` | No `e2e-kek` (signed in without PRF), account has no encryption | E2E section explains a passkey sign-in is needed to enable |
| `Blocked` | No `e2e-kek`, account has encryption | Warning banner; sync and file access blocked |
| `Disabled` | PRF available, no keys.json | Enable button |
| `NeedsSetup` | keys.json exists, no usable local wrapped master key | Banner + "Set up this device" (passphrase prompt) |
| `Unlocked` | Keys in memory | Change passphrase, Disable |
| `DisabledElsewhere` | Online, keys.json gone, but local key material present | Warning dialog, then clean up (below) |

## Flows

### Normal startup

PRF → `e2e-kek` → unwrap local master key → load keys.json (Secrets Manager when online, cached copy when offline; refresh cache when online) → unwrap file keys into `IDerivedKeyStore` → `Unlocked`. No passphrase, no argon2. If the local wrapped master key is missing or fails to unwrap → `NeedsSetup`. After unlocking, run the encrypt sweep, which picks up any plaintext files that arrived from a device that wasn't set up yet.

### Enable (step dialog)

A dialog lists each step and ticks it green as it completes, ending with a lock icon and **E2E Encryption Enabled**.

1. Full sync.
2. Turn on offline mode (remember the user's previous setting).
3. Load keys.json. If it exists → stop and switch to **Set up this device**.
4. Check the index: if any file is already marked encrypted → stop and report.
5. Passphrase entered twice (shown before the steps start, with the no recovery warning).
6. New 16 byte salt, derive the passphrase key with the build's Argon2id settings.
7. Create the master key and file key; build keys.json.
8. `TryCreateAsync`. If it returns `false`, another device got there first → stop and switch to setup. Do not continue until the save is confirmed.
9. Wrap the master key with `e2e-kek`, save locally; cache keys.json.
10. Raw keys into `IDerivedKeyStore`; discard the passphrase and passphrase key.
11. Encrypt sweep.
12. Restore offline mode to the user's previous setting (the scheduler resyncs on going back online).

### Set up this device

Prompt for the passphrase, derive with the `kdf` values from keys.json, unwrap the master key. Wrong passphrase → let them retry as often as they like. Wrap the master key with `e2e-kek`, save locally, then continue as normal startup.

### Change passphrase

Current passphrase + new one twice (with the no recovery warning). Load keys.json, check the current passphrase by unwrapping the master key, new salt, new passphrase key, re-wrap, `SetAsync`. Only report success once the save is confirmed; refresh the cache. File keys and files are untouched; other devices keep working.

### Disable (step dialog)

Confirm with the passphrase (unwraps the master key from keys.json). Offline mode on, decrypt sweep, delete keys.json, delete local wrapped master key and cached keys.json, clear the `e2e-*` entries from `IDerivedKeyStore`, restore offline mode. Files then sync in plaintext.

### Disabled on another device

Detected when online, keys.json is gone, but this device still has local key material. Warn the user first. Then, using the cached keys.json and local master key (still usable on this device), decrypt the local encrypted files that have changes not yet synced, so they aren't uploaded as ciphertext nobody can read. Then clear the local key material and let sync bring down the plaintext for everything else.

### Sweeps (resumable)

Per encrypted profile, over the index:

- **Encrypt:** for each file that isn't a folder marker, isn't deleted and isn't marked encrypted: read it; if it already has the header (crash after writing) just mark it; otherwise encrypt with the active key and write through the tracking store (so it is marked dirty and uploaded), then set `Encrypted` and `KeyId`.
- **Decrypt:** the reverse, clearing the columns as each completes.

Each file is marked as it completes, so an interrupted sweep carries on with the files not yet marked.

## UI

- **Settings → Sync → E2E encryption:** current state, Enable / Set up this device / Change passphrase / Disable. Passphrase fields only appear inside those controlled dialogs, never on the page. Clear "there is no passphrase recovery" warning before confirming.
- **Settings → Argon2id:** shows the build's settings (memory, iterations, parallelism, key length) and a **Benchmark** button that runs one derivation with a random passphrase and salt and shows how long it took.
- **Files page:** a lock to the left of encrypted files. Green: the key is held, all good. Red: E2E encryption isn't set up on this device.
- **Banner:** for `Blocked` (passkey sign in needed, hardware PRF is essential), `NeedsSetup` and `DisabledElsewhere`.

## Implementation notes

Decided while building it, on top of the plan above:

- **Reloads lock it again.** The PRF keys only live in memory, so a reload with a saved session comes back `Blocked`. The banner offers **Unlock with passkey**, which uses the passkey saved with the user on the login page (`IPasskeyUnlockService`) without signing in again, then unlocks with this device's keys.
- **Sync gate.** `ISyncGate` (Core) lets a feature hold back a profile's passes; `ISyncService` skips a held profile. `E2eSyncGate` holds the encrypted profiles unless the status is `Unsupported`, `Unavailable`, `Disabled` or `Unlocked`, so `Unknown` (not worked out yet, or offline with no local copy) waits too. Once back online an `Unknown` status is worked out again and sync runs if it allows.
- **Plaintext arrivals.** Besides after unlocking, the encrypt sweep also runs for a profile whenever a pass downloads files into it, so plaintext from a device that was not set up is encrypted as soon as it arrives.
- **Index columns.** `TrackingSyncFileStore` (local writes) and `SyncEngine` (synced files) both set `Encrypted` and `KeyId` from the stored content's header. The engine still works on the stored bytes, so it never needs the keys.
- **Store layering.** App code gets `EncryptingSyncFileStore` → `TrackingSyncFileStore` → the untracked store, so the index sees the stored (encrypted) bytes.
- **Other tabs** are told to reload when encryption is turned on or off (the storage channel's `Changed` message); they then unlock again.
- **Passphrases** must be at least 8 characters, and are cleared from the view model as soon as they are used.
- **`IUserKeysStore`** now lives in Core (`blazwiz.Core.Cryptography.Interfaces`), with `TryCreateAsync`.
- **`IDerivedKeyStore.RemoveKey`** was added, so turning encryption off only clears the `e2e-*` keys.
- **Generic building blocks** (`IAeadCipher`, `IKeyWrapService`, `IArgon2idKeyDerivationService`, `Argon2idOptions`) are in `blazwiz.Core/Cryptography` and always included in the template; the rest is part of Notes.

## Testing

- **Unit tests:** Argon2id (including the reference vector), AEAD (checked against .NET's `AesGcm`), key wrap, keys.json shape, file format (including a tampered header and an unknown key), wrapped file keys swapped between ids, wrong passphrase, every service flow, encrypt/decrypt sweeps including resuming, encrypting store, index columns, sync gate, view models.
- **UI tests:** what runs locally with the placeholder sign in: the E2E section's state (not available, no banner), the Argon2id panel showing the Release settings, no locks on plain notes. The benchmark is not clicked in the UI tests: at the Release settings it can take a long time in the browser.
- **Real flows** (enable, second device, change passphrase, disable, disabled elsewhere): tested by the user on the deployed site, same as S3 sync.
- All four suites pass before the work is called done.
