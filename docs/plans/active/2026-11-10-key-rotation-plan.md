---
title: Rotate a profile's keys when its passphrase changes
type: active
status: not started
added: 2026-11-08
updated: 2026-11-10
review: ../../reviews/2026-11-08-security-review/security-review.md
finding: SR3
severity: medium
urgency: high
before-1.0: yes
reason: users will expect a passphrase change to revoke anyone who held the old passphrase; today it revokes nobody
areas: [Encryption, Sync, UI.Sync, UI]
effort: large
functional-change: yes
blocked-by: none (keys documents in S3 with conditional writes, and sign out everywhere through user events, are done)
decision: design questions answered by the maintainer on 2026-11-10 (see Decisions)
related:
  - ../../reviews/2026-11-08-security-review/security-review.md (SR3; SR37, nonce rotation, belongs with this change)
  - ../../diagrams/user-events.md (sign out everywhere and the resume check, which rotation ends with)
---

# Rotate a profile's keys when its passphrase changes

Moved from the security review's backlog (`backlog/passphrase-change-key-rotation.md`) on 2026-11-10, once its
prerequisites were in place.

## The gap

`E2eProtectedProfileManager.ChangePassphraseAsync` unwraps the master key with the old passphrase and wraps it again
with the new one. The master key, the file keys and the active key id stay the same. Every earlier version of the keys
document also stays in the data bucket: it is versioned, and the role's `NoVersionPurge` statement stops anyone
purging a version until the lifecycle rule expires it. So a passphrase change revokes nobody. Suppose someone has a
copy of a keys document (a stolen device's localStorage copy, a backup, an earlier version in the bucket) **and** the
old passphrase, or once held the master key. They can still unwrap the same master key, so the same file keys, so
every file, including those written after the change. Users will reasonably assume that changing the passphrase after
a suspected compromise locks the attacker out.

After rotation, the old versions in the bucket still unwrap the old master key and the old file keys. That is
expected: the attacker had those already. What rotation stops is access to the **new** keys, and so to every file
written or re-encrypted after the change. The role cannot read earlier object versions (it has no
`s3:GetObjectVersion`), so a re-encrypted file's earlier ciphertext is out of reach of a user's credentials.

## What is already in place

| Need | Where |
|---|---|
| Keys document writes that cannot lose a concurrent write | `S3UserKeysStore` (`If-Match` / `If-None-Match`); `ChangePassphraseAsync` already retries on `E2eDocumentChangedException` |
| Several file keys per profile, one active, the key id in each file's header | `UserKeysDocument.Keys`, `FileKeyEntry.ActiveStatus` / `RetiredStatus`, `IE2eKeyRing.Load`, `IEncryptedFileFormat.TryGetKeyId` |
| The key id of each file in the sync index | `SyncIndexRecord.KeyId` |
| Progress with file counts | `E2eProgress(step, completed, done, total)` |
| Sign out everywhere with a reason | `IAuthenticationService.SignOutEverywhereAsync(SignOutReason.KeysRotated)`, the `keys-rotated` user event, `SessionEndedReason.KeysRotated`, and `Login.SignedOut.KeysRotated` / `KeysRotatedUnknownDevice` |
| Devices that miss the event | `SessionResumeMonitor` runs `CheckSessionAsync` when the app resumes; `GlobalSignOut` has revoked the tokens |

## A gap the backlog plan missed: device unlock reads only the cached document

`UnlockWithDeviceAsync` unwraps the device's `E2eLocalKeys.WrappedMasterKey` and loads the file keys from
`E2eLocalKeys.CachedDocument`. It never reads the store, and nothing compares `CachedDocumentETag`. Signing out locks
the profiles but keeps the local copy. So after a rotation, another device with device unlock signs in again with its
passkey and unlocks **with the old master key and the old document**, with no error. It reads old files, writes new
ones under the retired key, and fails with `CannotDecrypt` on every file written under the new key. The backlog plan
assumed this device would fall back to the passphrase. It falls back only when the KEK unwrap fails, not when the
document has changed.

Rotation therefore also needs device unlock to check the store when online (step 6).

## Decisions (maintainer, 2026-11-10)

- **Retired keys are kept.** They are not dropped when no file uses them. Housekeeping to remove redundant keys can
  come later, as its own piece of work.
- **The rotation runs to the end. There is no skip button.** The user is warned before it starts, with an estimate of
  how long it will take, sees a progress bar, and waits. Every device is signed out only when it has finished.
- **An interrupted rotation must be finished before the profile is used again.** The next unlock shows the same
  progress screen, with a warning that this matters and must not be interrupted.
- **A full sync runs first.** If the device is offline or the sync fails, the passphrase change is refused.
- **No rotation without a passphrase change.** The security rests on the passphrase being secret, and a suspected
  compromise is a reason to replace it, not to keep it.
- **No automatic rotation before 1.0** (on a schedule or after N writes; SR37's bound stays documented only).

## Design

What the user sees, in order: confirm → sync → estimate and warning → rotate (progress bar) → signed out everywhere.

1. **Confirm and sync** (`UI.Sync`, `Sync`). The user enters the current and new passphrases. The app runs a full
   sync of the profile, so this device holds every file. Offline, or if the sync fails: stop, with a message. Nothing
   has changed yet.
2. **Estimate and warn** (`Sync`, `UI.Sync`). Time one throwaway file: encrypt it and upload it, then delete it. It
   must not be real data and must not be seen by sync on any device (a key outside the synced files, under the user's
   prefix). Extrapolate from the number and size of the profile's files. Show the estimate with the warning: every
   file is encrypted again and uploaded, it cannot be skipped, keep the app open and connected until it finishes, and
   every device is signed out at the end. The user confirms or cancels. Nothing has changed yet.
3. **New keys** (`Encryption`). Under the current passphrase, in the existing read and conditional-write retry loop:
   create a new master key and a new active file key. Wrap every existing file key again under the new master key, so
   old files still decrypt, and mark the old active key `retired`. Wrap the new master key with the new passphrase.
   Mark the document as **rotation in progress** (a new field in `UserKeysDocument`). Write it with `If-Match`. On
   `E2eDocumentChangedException`, start again from the new version with new random keys. Load the new keys into this
   device's key ring and save the cached copy. The old master key unwraps nothing new from then on.
4. **This device's unlock copy.** If the KEK is held (signed in with the passkey), wrap the new master key with it, as
   `EnableDeviceUnlockAsync` does. Otherwise clear `WrappedMasterKey`, so no copy of the old master key stays on this
   device.
5. **Re-encrypt and upload** (`Sync`). Read every file of the profile through the encrypting store, which decrypts
   with the retired key, and write it back, which encrypts with the new active key. Each write goes through the
   tracked store, so it marks the index and syncs like any edit. Skip files whose header already names the active key
   (this is what makes an interrupted rotation resumable). Push them. Report progress with `E2eProgress` and a new
   `E2eStep` value; the progress bar covers both re-encrypting and uploading.
6. **Device unlock checks the document** (`Encryption`). When online, `UnlockWithDeviceAsync` reads the store's
   document. If its ETag differs from `CachedDocumentETag` and the device's master key does not unwrap its file keys
   (`LoadFileKeys` throws `InvalidDataException`), clear `WrappedMasterKey` and throw
   `E2ePassphraseRequiredException`. If the master key still fits (nothing writes such a change once every passphrase
   change rotates, but the format allows it), update the cached copy and unlock. Offline, unlock from the cached copy
   as now. Edits made that way are under the retired key, which stays in the document, so they remain readable. The UI
   says why the passphrase is asked for again.
7. **Sign out everywhere.** When every file is re-encrypted and pushed, call
   `SignOutEverywhereAsync(SignOutReason.KeysRotated)`, then clear the rotation-in-progress mark. Every device, this
   one included, ends its session, wipes its unwrapped keys and unlocks again with the new passphrase. If
   `GlobalSignOut` fails, the rotation is not finished: the mark stays, and the screen keeps offering the sign out
   until it succeeds, since other devices still hold the old keys in memory.
8. **Finishing an interrupted rotation** (`Encryption`, `Sync`, `UI.Sync`). A crash, a closed tab or a lost connection
   after step 3 leaves the mark in the keys document. Whenever a profile is unlocked (on any device, with the new
   passphrase) and its document carries the mark, the app shows the rotation screen instead of the profile: a warning
   that the passphrase change was interrupted, that it must finish and must not be interrupted, then sync, steps 5
   and 7. The profile cannot be used until it is done.
9. **Unknown key id** (`Sync`). When `EncryptingSyncFileStore` meets a file whose key id the key ring does not hold,
   run the resume check (`CheckSessionAsync`) before throwing. A device whose connection dropped silently then signs
   out the first time it sees a file written under the new key. This needs a hook that `Sync` can call without
   depending on `Auth`; decide where it lives (an `ISyncKeyMissingHandler` registered by `UI.Sync` is one option).
10. **Text** (`SyncUiText`). `Sync.E2e.Dialog.ChangePassphrase.Text` now says "Its files and other devices are not
    affected", which is true today and must change with the behaviour. New text for: what a passphrase change does
    ("Your files are encrypted again with new keys. Anyone who had your old passphrase and a copy of your keys can no
    longer read them. Every device, this one included, is signed out and must sign in again."), the estimate and
    warning, the progress step, refusing when offline or when the sync fails, a failed sign out, the interrupted
    rotation warning, and step 6's "your keys have changed" prompt.

## Other devices

Each device caches the keys document and holds the unwrapped keys in memory. A device unlocked before the change does
not know the new key id. Step 7 ends every session, so there is no separate key-version mechanism:

- **Open and connected devices** receive the `keys-rotated` event and sign out at once.
- **Closed, offline or backgrounded devices** miss the event. `GlobalSignOut` has revoked their tokens, so the resume
  check refuses them and they sign out before they sync.
- **Devices with device unlock** unlock again only with the new passphrase, because of step 6.
- **Open devices whose connection dropped silently** keep their session until the next token renewal, or until step 9
  catches them.

**To settle while building:** until step 7, other devices are still signed in and can write files under the old key.
So can a device that made edits offline before the rotation and pushes them after signing in again. Those files
arrive under a retired key after the rotation has finished. They stay readable (retired keys are kept), but they are
not under the new key. Proposal: at each unlock, re-encrypt any file whose index record names a retired key, through
the same code as step 5. Whether that blocks the profile like step 8 or runs as part of sync is the maintainer's call
when the first part is working.

## Checklist

- [ ] `Encryption`: a rotate operation on `IE2eKeysDocumentService` (new master key, new active file key, old keys
      wrapped again and retired, the rotation-in-progress mark), used by
      `E2eProtectedProfileManager.ChangePassphraseAsync`; unit tests, including a retry after
      `E2eDocumentChangedException`, every old file key still unwrapping, and exactly one active key.
- [ ] `Encryption`: this device's unlock copy wrapped again or cleared (step 4); unit tests.
- [ ] `Encryption`: device unlock checks the store's document when online (step 6); unit tests for unchanged, changed
      with the same master key, changed with a new master key, and offline.
- [ ] `Sync`: the sync before rotating, and refusing when offline or when it fails (step 1); unit tests.
- [ ] `Sync`: the timing test with a throwaway file that sync never sees, deleted afterwards, and the estimate
      (step 2); unit tests with `FakeTimeProvider`.
- [ ] `Sync`: re-encrypt and push a profile's files, with progress, skipping files already under the active key
      (step 5); unit tests, and an integration scenario in `Sync.IntTests` (rotate, re-encrypt, sync, read on a second
      device).
- [ ] Finishing an interrupted rotation (step 8): unit tests for an interruption after step 3, part way through
      step 5, and after step 5 with the sign out still to do; finishing on a different device.
- [ ] `Sync`: unknown key id runs the resume check (step 9); unit tests.
- [ ] `UI.Sync`: the confirmation, estimate and warning, progress, sign out and its retry, and the interrupted
      rotation screen in `E2eProfilesViewModel` and the unlock panel; new `SyncUiText` entries; view model tests.
- [ ] Template UI tests: change a passphrase, see the warning and the progress, be signed out, sign in again with the
      new passphrase, and read the old files; reload part way through and be made to finish.
- [ ] Settle the files that arrive later under a retired key ("Other devices").
- [ ] `PublicAPI.Unshipped.txt` for each changed package.
- [ ] Docs: `src/Devoctomy.Blazwiz.Encryption/README.md`, `src/Devoctomy.Blazwiz.Sync/README.md` (and its SR37 nonce
      note), `src/Devoctomy.Blazwiz.UI.Sync/README.md`, `docs/diagrams/user-events.md` and `docs/diagrams/sync.md`,
      `CHANGELOG.md`.
- [ ] Upgrade test (AGENTS.md): the keys document gains the rotation-in-progress field; before 1.0 no migration is
      kept. Rotation changes what apps see in Settings and in sign out, so run
      `./templatepack/test-template.ps1 -UpgradeFrom latest`.
- [ ] Mark SR3 fixed in the security review, and remove this plan.

## Later, not in this plan

- Housekeeping that removes retired keys no file uses.
- Rotation on a schedule or after N writes (SR37).
