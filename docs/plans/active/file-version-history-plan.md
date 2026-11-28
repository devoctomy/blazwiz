---
title: "File version history: list a synced file's past versions, view one, restore it"
type: plan
status: draft
added: 2026-11-28
updated: 2026-11-28
areas: [Sync, UI.Sync, Aws, template]
effort: medium
functional-change: yes
blocked-by: none
---

# File version history (built, waiting for the deployed test)

## What the user gets

In the note editor, a **History** dropdown at the top right of the page header (next to Close and Save) lists the
note's past versions, newest first, each shown as its ETag and upload time (`etag (date time)`):

- **Current** is always first, and is what the editor shows when the note opens.
- Nothing is fetched until the dropdown is expanded. Each time it is expanded, the versions' metadata is fetched (only
  the metadata). A version's content is downloaded when it is picked.
- With unsaved changes, picking a version first warns and asks whether to save them: **Save** (save, then show the
  version), **Don't save** (drop the changes, then show it) or **Cancel** (stay on the edited current version).
- Picking a past version shows it in the editor and preview, read only, with a bar above it: "Viewing the version
  from 26 Nov, 14:02" and two buttons, **Restore this version** and **Back to current**.
- Picking another version shows that one instead. Picking **Current**, pressing **Back to current**, closing the
  note or going to another page changes nothing.
- **Restore this version** asks to confirm, then saves that version's content as the note's new current version.
  Nothing is lost: the version it replaces becomes a past version, listed like any other, so a restore can itself be
  undone by restoring again.

The framework does the work (listing, reading and restoring versions of any synced file); the note editor is the
template's example of using it, as the rest of the notes feature is.

## What is there already

- The data bucket is versioned (`VersioningConfiguration: Enabled` in `src/Devoctomy.Blazwiz.Aws/cloudformation/stack.yaml`),
  so every upload already keeps the version it replaces. How long versions are kept is the bucket's business: the
  feature assumes versioning is on and shows whatever versions S3 has.
- The authenticated role may not purge versions (`NoVersionPurge` denies `s3:DeleteObjectVersion`), but it cannot read
  them either: it has `s3:ListBucket`, `s3:GetObject`, `s3:PutObject`, `s3:DeleteObject` and no version actions.
- Files are encrypted end to end and bound to their profile and path (`EncryptingSyncFileStore`). A version lives at
  the same key, so it decrypts with the same binding. A passphrase change keeps the earlier file keys, retired
  (`IE2eProtectedProfileManager`), so versions from before it still decrypt.

## Design

### Restoring is an ordinary save

Restore never copies an S3 version over the current one. It reads the old version, decrypts it, and writes the
plaintext through the profile's ordinary file store (`ISyncProfileRegistry.GetFileStore`), exactly as if the user had
typed it and pressed Save. So:

- It works offline once the version is loaded: the write is local, and the next pass uploads it.
- It is encrypted with the **active** key, so a version from before a passphrase change comes back under the new one.
- The upload is conditional (`If-Match`) as always, so a change made meanwhile on another device goes through the
  usual conflict handling, not overwritten unseen.
- The local store, the index and the pending count stay right, because nothing goes around them.

### Sync package

1. `ISyncRemoteStore` gets two members:
   - `ListVersionsAsync(key)`: every version of exactly that key, metadata only (S3 `ListObjectVersions` with the
     key as prefix, keeping only entries whose key matches exactly, paging with the key and version id markers),
     newest first. Delete markers are not versions and are not listed.
   - `GetVersionAsync(key, versionId)`: downloads one version's content (`GetObject` with `VersionId`), or null if
     S3 no longer has it.
   - A new model, `RemoteObjectVersion`: `Key`, `VersionId`, `ETag`, `LastModified`, `Size`, `IsLatest`.
2. A new service, `ISyncFileHistoryService` / `SyncFileHistoryService`, registered by `AddBlazwizSync`:
   - `IsAvailable`: false without a data bucket (local development), so the UI can hide itself.
   - `GetVersionsAsync(profile, key)`: the remote versions' metadata as `SyncFileVersion` (version id, ETag, uploaded
     at, size, whether it is the one this device last synced). No content is downloaded. Throws
     `SyncNetworkException` offline, as other remote calls do.
   - `ReadVersionAsync(version)`: the version's plaintext (`SyncFileVersionContent`), or null if the bucket no longer
     has it. Throws `E2eLockedException` while the profile is locked.
   - `RestoreAsync(versionContent)`: writes the content already read through the tracked, encrypting file store, which
     asks the scheduler for a pass. It takes the content read, so it works offline once the version is shown.
3. Decrypting a downloaded version uses the same code as `EncryptingSyncFileStore.ReadAsync` (key lookup, binding,
   the empty and plaintext checks, the key missing handlers), not a copy of it: that step is
   `EncryptingSyncFileStore.DecryptAsync` (`ISyncFileDecryptor`), and the registry hands out each profile's
   (`ISyncProfileRegistry.GetFileDecryptor`, the same object as `GetFileStore`).
4. Decrypted versions are kept only in memory, by the view model showing them, and never written to browser storage.

### UI.Sync package

A reusable `FileHistoryPicker` (the dropdown and its dialogs) and `FileHistoryBar` (the bar above a past version),
both shown over one `FileHistoryViewModel` with all the logic, owned by the page's view model and given a profile and a
key (`SetFile`), so any app built on blazwiz can put history on any of its synced files. Nothing in it knows about
notes:

- The dropdown. Its versions are fetched each time it is expanded, and never otherwise: not when the page opens, not
  after a sync. **Current** comes first, then the versions, each as `etag (date time)`. Picking one downloads and
  decrypts it.
- The page's unsaved changes, through `IFileHistoryHost` (whether there are any, save them, drop them). With unsaved
  changes, picking a version opens the picker's own Save / Don't save / Cancel dialog first.
- While the app is offline (`IConnectivityService` has a reason: offline mode, sync turned off, no network, no
  credentials, S3 unreachable), the dropdown is disabled and says why when hovered. A version already shown can still
  be restored, as restoring is a local save. If listing fails anyway, the dropdown shows why.
- A version that can't be decrypted on this device (`E2eLockedException`: a key it doesn't hold;
  `CryptographicException` or `InvalidDataException`: content that isn't the file's) is never shown or restored: the
  bar says it can't be opened here, and the current version stays on the page. Nothing crashes.
- It raises the selected version's content (or "current") to the page; the page decides how to show it.
- The confirm dialog for restoring, and a call to `RestoreAsync`.
- Text in `SyncUiText`. Hidden when `IsAvailable` is false.

### Template (notes example)

- `NoteEditor` puts `FileHistoryPicker` in its `PageHeader` and `FileHistoryBar` above the editor.
  `NoteEditorViewModel` owns the `FileHistoryViewModel`, makes the editor (the same textarea, so its shortcuts stay
  attached), the toolbar and Save read only while a past version is shown, and after going back or restoring reads
  the note again as current.
- It is the `IFileHistoryHost`: `IsDirty`, its save, and dropping the edits, so the picker's unsaved changes dialog
  saves the note the usual way.
- When a pass downloads a change to the open note while a past version is shown, the version stays on screen and
  **Current** refreshes behind it. The list is not fetched again until the dropdown is next expanded.

### AWS base stack

- `AuthenticatedRole`: allow `s3:ListBucketVersions` on the bucket with the same `s3:prefix` condition as
  `ListOwnPrefix`, and `s3:GetObjectVersion` on the user's own prefix. `NoVersionPurge` stays as it is.
- `DataBucket` CORS: unchanged. `ListObjectVersions` and a `GetObject` of a version are GETs, already allowed, and the
  portal reads nothing from the `x-amz-version-id` header (version ids come from the listing's body), so it is not
  exposed.
- Versioning and lifecycle settings are left as they are.

## Limits (to say in the docs)

- **History is per path.** S3 has no rename, so renaming or moving a note writes a new key: its history starts again
  there, and the old path's versions stay under the old key. Following a file across renames is not in this plan.
- **Versions are uploads, not saves.** Several saves between two passes upload once, so they make one version.
- **A passphrase change uploads every file again**, so each file gains a version identical in content to the one
  before it.
- **Deleted files** cannot be brought back from here (a deleted note has no editor to open). That would be a separate
  "recently deleted" feature.

## Tests

- [x] Unit: `S3SyncRemoteStore` list (paging, exact key only, every version returned, delete markers left out, 403,
  network errors) and get version (found, gone).
- [x] Unit: `SyncFileHistoryService`: available only with a bucket, listing without downloading, the version this
  device synced marked, read and decrypt, gone, restore writes through the profile's file store. (Offline and locked
  are the remote store's and decryptor's errors, tested there and in the view model.)
- [x] Unit: `EncryptingSyncFileStore.DecryptAsync`, and `EncryptingSyncFileStore` still passing its tests through it.
- [x] Unit: `FileHistoryViewModel` and the `NoteEditorViewModel` changes: nothing fetched before the dropdown
  is expanded, each expansion fetches metadata only, select downloads one version, select another, back to current,
  cancel restore, restore, and with unsaved changes: save then view, don't save then view, cancel. Disabled offline
  (and closed when the app goes offline); each kind of decryption failure leaves the current version shown.
- [x] Integration: a past version under a key nobody has can't be read (`E2eLockedException`), and the file is not
  affected.
- [x] Fakes: `FakeSyncRemoteStore` (unit tests) and `SharedCloud` / `SharedCloudRemoteStore` (integration tests) keep
  versions.
- [x] Integration (Reqnroll, `ProtectedProfiles.feature`, where the encrypted two-device setup is): edit a file three
  times across passes, list and read its versions, restore the first, and a second device syncs the restored content;
  a restore while another device changed the file meanwhile does not overwrite that change.
- [x] Deploy tests (`UserData.feature`): a signed in user can list and read versions under their own prefix and
  cannot delete one, and is refused another user's versions and the whole bucket's. (Run by the maintainer.)
- [ ] UI tests: the UI tests run without a data bucket, so the picker is hidden there. No UI test was added for it.
- [ ] `test-template.ps1`, and `-UpgradeFrom latest` for the stack change (an app deployed from the latest release
  upgraded to the new base stack can list versions). Not run locally: left to the CI workflow and the maintainer's
  deployed test.
- [x] Coverage of every changed line in `src` and the template's `Application` project measured from the Cobertura
  reports; the count of uncovered Razor markup lines added reported before merge.

## Documentation

- [x] `docs/blazwiz/sync-and-encryption.md`: a "File history" section (the service, the picker, the limits above).
- [x] `src/Devoctomy.Blazwiz.Sync/README.md`, `src/Devoctomy.Blazwiz.UI.Sync/README.md`.
- [x] `src/Devoctomy.Blazwiz.Aws/cloudformation/README.md`: the new permissions.
- [x] `template/README.md`: the note editor's history.
- [x] `docs/diagrams/sync.md`: `SyncFileHistoryService` in the list of parts.
- [x] `CHANGELOG.md` (Unreleased).

## Steps

- [x] 1. Stack: permissions.
- [x] 2. Sync: `RemoteObjectVersion`, the two remote store members, the fakes.
- [x] 3. Sync: the shared decrypt step; `ISyncFileHistoryService`.
- [x] 4. UI.Sync: `FileHistoryPicker`, `FileHistoryBar` and their view model, text.
- [x] 5. Template: the note editor.
- [x] 6. Integration and deploy tests.
- [ ] 7. Documentation, public API lists, full build and test run, coverage report. (All but `test-template.ps1`;
  then the maintainer's deployed test.)
