---
title: Rotate a profile's keys when its passphrase changes
type: backlog
status: not started
added: 2026-11-08
review: ../security-review.md
finding: SR3
severity: medium
urgency: high
before-1.0: yes
reason: users will expect a passphrase change to revoke anyone who held the old passphrase; today it revokes nobody
areas: [Encryption, UI.Sync, Sync]
effort: large
functional-change: yes
blocked-by: none
related:
  - SR37 (nonce rotation belongs with this change)
  - ../../../plans/backlog/user-events-appsync.md (tells other devices the keys changed)
---

# Rotate a profile's keys when its passphrase changes

From SR3 of `../security-review.md`: a functional change, so not done with the review's fixes.

## The gap

`E2eProtectedProfileManager.ChangePassphraseAsync` unwraps the master key with the old passphrase and wraps it again
with the new one. The master key, the file keys and the active key id do not change, and Secrets Manager keeps the
previous document as `AWSPREVIOUS`. So a passphrase change revokes nobody: whoever captured a keys document (a stolen
device's localStorage copy, a backup, the previous version in the account) **and** learned the old passphrase, or who
ever held the master key, can still unwrap the same master key, hence the same file keys, hence every file, including
those written after the change. Users will reasonably assume that changing the passphrase after a suspected compromise
locks the attacker out.

## Design

1. **New keys.** On a passphrase change, create a new master key and a new active file key. Wrap every existing file
   key under the new master key (so old files still decrypt) and mark the old active key `retired`
   (`FileKeyEntry.RetiredStatus` exists for this). Wrap the new master key with the new passphrase. Write the
   document. The old document's master key no longer unwraps anything new.
2. **Re-encrypt the files.** Read every file of the profile through the encrypting store (which decrypts with the
   retired key) and write it back (which encrypts with the new active key). Each write goes through the tracked store,
   so it marks the index and syncs like any edit. Report progress with `E2eProgress` (its `Done`/`Total` file steps
   were made for this). Vault secrets are small JSON files, so this takes seconds; the template's `files` profile may
   hold larger files, so the step runs in the background with the profile unlocked and a progress bar, and a reload
   part way through just leaves some files under the retired key (still readable; a later "re-encrypt everything"
   action, or the next passphrase change, finishes them).
3. **Drop the retired key** once no file uses it: list the index records' `KeyId`s; when none names the retired key,
   remove it from the document. Until then it stays, so nothing becomes unreadable.
4. **Device unlock copies.** `E2eLocalKeys.WrappedMasterKey` on each device wraps the old master key; it no longer
   matches and falls back to the passphrase (as `UnlockWithDeviceAsync` already does), after which the device wraps
   the new one when the user turns device unlock on again. Say so in the UI.
5. **Text.** In Settings, say what a passphrase change does: "Your files are encrypted again with new keys. Anyone who
   had your old passphrase and a copy of your keys can no longer read them." Until this is built, say the opposite.

## Other devices

Each device caches the keys document locally and holds the unwrapped keys in memory, so a device unlocked before the
change does not know the new key id and fails to decrypt files written after it. Two things are needed: a durable
check, where an unknown key id in a file header (or the next unlock) makes the device re-read the keys document and
compare its version before asking for the passphrase again; and, optionally, a `keys-changed` event on the user event
channel (`docs/plans/backlog/user-events-appsync.md`) so open devices do it at once. The check is required; the event
only removes the lag. The keys document write itself is still unguarded against two devices writing at once, which matters more once a
lost write can orphan files under a key nobody holds; `docs/plans/backlog/protected-profile-concurrent-writes.md`
moves the documents to S3 with `If-Match` writes and should land first.

## Open questions

- Should "re-encrypt everything" also be offered on its own (without a passphrase change), as a response to a
  suspected compromise of the keys rather than the passphrase?
- Rotation on a schedule, or after N writes (the random-nonce bound, SR37)? Probably not before 1.0.
