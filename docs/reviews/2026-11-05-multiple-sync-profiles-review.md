# Review: multiple sync profiles

**Date:** 2026-11-05
**Branch:** `multiprofiles` (no changes from `main` yet)
**Question:** do we only support one sync profile?

**How this review was done:** by reading the source (`Sync`, `UI.Sync`, the template's `Program.cs` and the notes
extension, and the sync tests), then trying a second profile in an app generated from the template (see "Trial app").
MP1 and MP4 were confirmed by tests in that app. The other findings come from reading the code only.

## Answer

**No, not entirely.** The sync engine and its configuration were built for several profiles, and most of the
pipeline treats them separately. What actually uses only one is the **template** (and so every generated app), the
**integration tests**, and parts of the **status and storage UI**, which were written as if there were one. Adding a
second profile today would build and sync, but would show misleading status and storage choices, and has no checks
against two profiles that clash.

### Already per profile

| Area | Where | Notes |
|---|---|---|
| Configuration | `SyncBuilder.AddProfile`, `SyncOptions.Profiles` | Any number; duplicate names are refused. |
| DI | `SyncServiceCollectionExtensions.AddProfile` | Keyed index, file and remote stores and matcher per profile; one `SyncProfile` (and `EncryptedSyncProfile`) per profile. |
| Engine | `SyncEngine.SyncAsync(SyncProfile, ...)` | Takes one profile per pass. |
| Service | `SyncService` | Runs all or one by name; `FilesChanged` carries the profile; pending count sums every profile. |
| Scheduler | `SyncScheduler` | State, debounce and verify per profile; lock `sync-<profile>`. |
| Gate and E2E | `E2eSyncGate`, `SyncEncryptionService`, `SyncEncryptionSweeper` | Only encrypted profiles are held back or swept (tested with `notes` + `photos`). |
| Storage | `SyncStorageResolver`, `SyncStorageMigrationService` | State per profile; each profile moved under its own lock. |
| Device stores | IndexedDB, OPFS, folder | Default object store names include the profile name; the folder store only sees files matching the profile's pattern. |
| Sync log page | `SyncViewModel`, `SyncView.razor` | Profile filter shown when there is more than one. |

### Tests with more than one profile

Only registration and gating: `SyncServiceCollectionExtensionsTests` (two profiles, encrypted and not),
`E2eSyncGateTests`, `SyncEncryptionServiceTests` and `SyncEncryptionSweeperTests` (a second `photos` profile is
ignored). Nothing tests two profiles syncing, the status across them, a storage move of two profiles, or the
settings UI with two. `Sync.IntTests` uses one profile (`SimulatedDevice.Profile = "notes"`).

## Trial app

An app was created from the template, using the packages as a user would, to check that a second profile can be
set up. It is in `temp/MultiProfile`, which git ignores. It has not been deployed.

- **How:** the framework and template were packed from the working tree as `0.3.0-multiprofile.1` into
  `temp/feed`. The template was installed into an isolated hive (`temp/hive`, so the machine's templates are not
  touched), and `dotnet new blazwiz -n MultiProfile` was run with the default options (sync, notes, showcase,
  tests). The app's `Directory.Packages.props` and `nuget.config` point at the feed, as `test-template.ps1` does.
  The app restored `Devoctomy.Blazwiz.Sync/0.3.0-multiprofile.1`, so it builds against the packages, not the source.
- **Change:** `Program.cs` adds a `photos` profile after `AddNotesProfile()`: its own index store
  (`sync`/`photos_index`), IndexedDB, OPFS and folder storages (default IndexedDB), remote `photos/**/*`,
  encrypted.
- **Tests added** (`MultiProfile.Portal.Application.UnitTests/Extensions/MultipleSyncProfilesTests.cs`):
  1. Both profiles resolve from the app's real registrations (`ValidateScopes`, `ValidateOnBuild`), each with its
     own file store, index store, OPFS store and matcher. Both are encrypted and switchable. **Passes.**
  2. A profile with `**/*` is accepted beside notes, and both match `notes/a.md`. **Passes, so MP4 is confirmed.**
  3. `SyncService` with `notes` failing then `photos` succeeding, one call per profile as the scheduler does,
     ends Idle with no error and a `LastSyncedAt`. **Passes, so MP1 is confirmed.**
- **Runs:** `dotnet build MultiProfile.slnx` (0 warnings, 0 errors). `dotnet test` on the application unit tests:
  276 passed, 0 failed. `dotnet test` on the app's UI tests (Playwright, the app started by the tests): 46 passed,
  0 failed. The UI tests do not sign in or sync, so they show only that the app starts and its pages render with
  two profiles. They do not cover the Settings storage choice (MP2) or the log filter (MP6).
- **Not checked:** real sync to S3, the storage move with two profiles (MP2) and the shared folder (MP3). These
  need a signed in user in a browser, so they wait for a deployment.

So a second profile can be set up today in a few lines, and it builds, registers and starts. The gaps are the
findings below.

## Findings

### MP1 (Medium). The sync status shows only the last profile's pass

- [ ] Fixed

`SyncScheduler.RunAllAsync` (`SyncScheduler.cs:158`) calls `SyncService.SyncAsync(profile)` once per profile.
Each call overwrites the whole status in its `finally` (`SyncService.cs:102-106`): `State`, `LastErrorCode` and
`ConflictsResolved`. If `notes` fails and `photos` then succeeds, the pill and Settings show Idle with no error, and
the failure is lost. `LastSyncedAt` moves on even though one profile has not synced. With one profile this cannot
happen, so nobody has seen it.

**Fix:** keep the result per profile in `SyncService` (last error, conflicts, last synced) and derive the overall
status from all of them: Error if any profile's last pass failed, `LastSyncedAt` the oldest of the profiles that
can sync. Unit test: two profiles, first fails, second succeeds, status is Error.

### MP2 (Medium). The storage choice is one per device, but read from the first profile only

- [ ] Fixed

Settings shows one storage choice for all switchable profiles. `SyncSettingsViewModel.LoadStorageKindAsync`
reads `StorageKind` from `switchableProfiles[0]` only (`SyncSettingsViewModel.cs:463`), and after a move sets it to
the target (`:387`). But profiles can differ:

- `MigrateAsync` only moves profiles that offer the target storage. If `notes` offers OPFS and `photos` does not,
  "move to OPFS" moves `notes` only, and Settings says everything is in OPFS.
- `MigrateAsync` moves profiles one after another (`SyncStorageMigrationService.cs:62`). If the second fails, the
  first stays moved and the second does not: a split state. After the error Settings reloads the first profile's
  storage and shows that.
- The choices offered (`IsOpfsSupported`, `IsFolderSupported`) are browser support, not what each profile allows.

**Fix (decision needed):** either

1. **One storage per device (recommended):** keep the single choice, require every switchable profile to offer the
   same storages (check in `AddBlazwizSync`), and make a move all-or-nothing (or let `RecoverAsync` finish the
   rest), or
2. **One storage per profile:** a storage row per profile in Settings, and `MigrateAsync(profile, target)`.

Option 1 matches how the folder works today (MP3) and keeps Settings simple.

### MP3 (Low). All profiles share one picked folder

- [ ] Decided

`SyncFolderService` keeps one folder per user, shared by every profile (its own doc comment says so). That is fine
while profiles' patterns do not overlap, because each folder store only sees its profile's files. It fits option 1
in MP2. It does not fit option 2: picking a folder for one profile changes it for all. Write the choice down in the
`AddLocalFolder` doc comment whichever way MP2 goes.

### MP4 (Medium). Nothing stops two profiles from clashing

- [ ] Fixed

`SyncBuilder.AddProfile` (`SyncBuilder.cs:99`) checks only the name. Two profiles can be configured to:

- **Overlap remotely**, e.g. `documents/**/*` and `documents/**/*.md`. Both list, upload and delete the same S3
  keys, each with its own index, so a delete in one is a remote delete in the other, and conflicts are resolved
  twice. With the shared folder (MP3) they also write the same local files.
- **Share a device store**, e.g. both `.AddIndexedDb(database: "files", store: "files")`, both
  `UseIndexedDbIndexStore("sync", "x")`, or the same OPFS root. The default names include the profile name, but
  explicit ones are not checked. (Stores are keyed by owner, not profile, so the two would see each other's
  files.)

**Fix:** check in `AddBlazwizSync` that no two profiles use the same index store, IndexedDB file store or OPFS root,
and refuse remote patterns where one's fixed prefix (up to the first wildcard) is a prefix of another's. Proving two
globs never match the same key is not worth it; a prefix check catches the realistic cases. Unit tests for each.

### MP5 (Low). The template and docs only ever show one profile

- [ ] Fixed

`Program.cs` adds either the notes profile or the `files` profile, never both, and `UI.Sync/README.md` shows one.
Nothing shows an app how a second profile is used: the keyed `ISyncFileStore`, a second set of store names, and
what Settings and the log do. Generated apps are the only real test of the packages together, so the gaps above
have never run in an app.

**Fix:** when `IncludeNotes` is on, keep the generic `files` profile as well (or add a small second profile to the
showcase), so the template, its UI tests and `test-template.ps1` run with two profiles. Add a short "More than one
profile" section to `Sync/README.md`.

### MP6 (Low). Profile names are shown as identifiers

- [x] Fixed

The log page's profile filter shows the raw name (`SyncView.razor:71`), e.g. `notes`, and so would any per-profile
storage row (MP2 option 2). That breaks the rule that visible text comes from resources, and
`PseudoLocalizationTests` would not catch it because the template has one profile, so the filter is hidden.

**Fix:** give profiles a display text resource (like `NavigationItem.TextResource`), e.g.
`AddProfile("notes", p => p.UseDisplayText(...))`, falling back to the name.

### MP7 (Low). No end to end test of two profiles

- [ ] Fixed

Add integration scenarios to `Sync.IntTests` with two profiles on one simulated device: each syncs only its own
keys, a delete in one does not touch the other, and an encrypted profile is held back while the other syncs.
`SimulatedDevice` needs to take the profile name and pattern instead of the `"notes"` constant.

## Suggested order

1. MP4 (validation): cheap, and stops the worst outcome (one profile deleting another's files).
2. MP1 (status): small change in `SyncService`, with unit tests.
3. MP2 and MP3: decide one storage per device or per profile, then fix.
4. MP5, MP7: two profiles in the template and integration tests, which also exercises MP1 and MP2 in the UI tests.
5. MP6.

## Follow-up: profiles as classes and from configuration (done 2026-11-05)

Profiles can now be classes deriving from `SyncProfileDefinition`, found in an assembly by
`AddProfilesFromAssemblyContaining<T>()` (like AutoMapper profiles), and sync settings and profiles can come from
configuration: `UseEmbeddedConfiguration` (a JSON file embedded in the app) and `UseConfiguration` (any section). All
of them call the builder's own methods, so every profile is checked the same way, which is where MP4's clash checks
belong. The template's profiles are classes (`NotesSyncProfile`, or `FilesSyncProfile` without notes), each a whole
file the template options keep or leave out; its other settings are in an embedded `sync.json`. A second profile in an
app is now one more class, which makes MP5 easier.

## Upgrade notes

MP1, MP4 and MP6 change public API (`SyncStatus`, builder methods) or behaviour apps see, so each needs its
PublicAPI entries and a `CHANGELOG.md` line. MP4 can refuse configurations that built before; that is a deliberate
change in what apps see, so it needs a `-UpgradeFrom latest` run. No stored data changes, so no `IDataMigration`.
