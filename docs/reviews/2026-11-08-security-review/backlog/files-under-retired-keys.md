---
title: Encrypt again the files that arrive under a retired key after a passphrase change
type: backlog
status: needs a decision
added: 2026-11-10
review: ../security-review.md
finding: SR3 (follow-up)
severity: low
urgency: medium
before-1.0: recommended
reason: after a passphrase change, a file written by a device that still held the old keys stays under a key the old passphrase can unwrap
areas: [Sync, UI.Sync]
effort: small
functional-change: yes
blocked-by: choosing whether it blocks the profile or runs as part of sync
---

# Encrypt again the files that arrive under a retired key after a passphrase change

Left over from the key rotation plan (SR3 of `../security-review.md`, completed and removed on 2026-11-11), which rotates a profile's keys when its
passphrase changes and encrypts its files again. This is the one case that plan did not settle.

## The gap

A passphrase change ends by signing every device out. Until that moment, the user's other devices are still signed in
and still hold the old keys, so they can write files under the old file key. So can a device that edited files offline
before the change and uploads them after signing in again: passkey unlock falls back to this device's copy of the keys
document when the store cannot be reached, and writes under the old key.

Those files reach the bucket after the change has finished. They stay readable, because the keys document keeps
retired keys. But they are not under the new key: someone with the old passphrase and an old copy of the keys document
can unwrap the old file key and read them. Nothing encrypts them again until the next passphrase change.

A file like this is easy to spot: its index record (`SyncIndexRecord.KeyId`) names a key whose status in the keys
document is `retired`.

## What is already there

`SyncKeyRotationService` (in `Devoctomy.Blazwiz.Sync`) already finds the files that are not under the active key
(`FindFilesUnderEarlierKeysAsync`), encrypts them again through the profile's own file store and uploads them. It is
what finishes a passphrase change that was cut short (`FinishAsync`). The same code can do this.

## Options

1. **As part of sync.** After a pass of an unlocked profile, encrypt again any file whose index record names a retired
   key, and let the next pass upload it. Nothing is shown to the user and nothing blocks. A locked profile cannot do
   it (it has no keys), so it waits until the profile is next unlocked.
2. **At each unlock, blocking the profile.** Like finishing a change cut short: the unlock panel shows progress until
   the files are encrypted again and uploaded. Certain, but it holds the user up for something they did not ask for,
   and it needs a connection.
3. **At each unlock, in the background.** Unlock opens the profile at once and the files are encrypted again behind
   it. No sign out everywhere follows either way: the keys have not changed, so there is nothing for other devices to
   forget.

The proposal in the key rotation plan was option 1 or 2, the maintainer's call. Option 1 is the least intrusive and
reuses sync's own triggers.

## Also left for later by the key rotation plan

Each now has its own backlog plan. Neither is part of this work.

- Housekeeping that removes retired keys no file uses any more: `remove-unused-retired-keys.md`. Retired keys are
  kept for now (the maintainer's decision of 2026-11-10).
- A suggestion to change the passphrase, and so rotate the keys, after a set time (SR37): `suggest-key-rotation.md`.

## To decide and check

- Which option.
- Whether a device that is offline when it unlocks with the passkey should be stopped from writing under a key it
  cannot confirm is still active, or whether writing under a retired key and fixing it later is acceptable.
- Tests: a file uploaded under a retired key after a change has finished ends up under the active key, on the device
  that wrote it and on another; a locked profile is left alone; a file edited meanwhile on another device is not lost
  (the edit time is kept, so the newest edit still wins a conflict).
