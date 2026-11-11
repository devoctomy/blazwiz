---
title: Remove retired file keys that no file uses
type: backlog
status: needs a decision
added: 2026-11-11
review: ../security-review.md
finding: SR3 (follow-up)
severity: low
urgency: low
before-1.0: no
reason: every passphrase change adds a key to the keys document and none is ever removed
areas: [Encryption, Sync]
effort: medium
functional-change: yes
blocked-by: none
---

# Remove retired file keys that no file uses

Left for later by the key rotation plan (completed and removed on 2026-11-11). The maintainer decided on 2026-11-10 that retired keys are kept, and that removing redundant ones is its own
piece of work.

## The gap

Each passphrase change makes a new active file key, wraps every existing key again under the new master key and marks
the old active key `retired` (`FileKeyEntry.RetiredStatus`). Nothing removes a retired key, even when every file has
been encrypted again under a newer one. So the keys document grows by one key per passphrase change, and every
retired key stays unwrappable by anyone holding the current passphrase or master key.

## What is already there

- `SyncIndexRecord.KeyId` names the key of each file, and `IEncryptedFileFormat.TryGetKeyId` reads it from a file's
  header.
- `SyncKeyRotationService.FindFilesUnderEarlierKeysAsync` finds the files that are not under the active key.
- Keys document writes are conditional (`If-Match`) and retried on `E2eDocumentChangedException`.

## To decide and check

- **When it is safe.** A key is unused only if no file anywhere uses it, not only the files this device knows of. Files
  that arrive late under a retired key (`files-under-retired-keys.md`) and edits made offline on another device are the
  risk: remove the key and those files can never be read again. This probably needs that plan done first, a full sync
  just before, and perhaps a grace period after the key was retired.
- **When it runs.** At the end of a passphrase change, as part of sync, or on its own.
- **What happens if it is wrong.** A file under a removed key is lost for good. Decide whether to keep the removed keys
  somewhere (for example the previous keys document versions, which the bucket keeps until the lifecycle rule expires
  them) or refuse to remove a key while any record could still name it.
- Tests: a retired key with no files is removed; one still named by a file is kept; a concurrent write of the keys
  document is retried; a file that arrives later under a removed key is reported, not silently dropped.
- Docs: the Encryption README (the keys document), `docs/diagrams` where they show the keys document, `CHANGELOG.md`.
