# Changelog

Every `Devoctomy.Blazwiz.*` package and the `Devoctomy.Blazwiz.Templates` template share one version (see
`docs/versioning.md`). Each release lists what changed for apps; breaking changes say what to do, and are repeated
in `UPGRADING.md` from 1.0.

Before 1.0 any release may break, and nothing is kept for earlier builds: no data migrations, no compatibility code.

## Unreleased

### Fixed
- The minimum IAM policy in the template's `cloudformation/README.md` lacked `cloudformation:GetTemplateSummary`, which
  deploying the app's own stack (`aws cloudformation deploy`) needs. With that policy, a deploy updated the base stack,
  then failed with AccessDenied on the app's stack. Add the action to policies made from the README.

### Changed
- The template's minimum IAM policy for deploying is now a file, `cloudformation/deploy-policy.json`, in place of a block
  in `cloudformation/README.md`, which shows how to create the policy from it.

## 0.5.0

### Added
- `IClipboardService` (Browser): copies text, and for secrets clears the clipboard again after a time unless something
  else was copied since (when the browser lets the page see that). A later copy replaces a clear still waiting; if the
  page does not have focus when the time comes, it is cleared when it has focus again.
- Toasts: `IToastService.Show(message, tone, duration)` shows a short message over the page that goes by itself (4
  seconds by default, at most 3 at once). The shell shows them (`ToastHost`, in the overlays region).
- `AppSettings.LogoUrl`: the logo in the sidebar and on the sign-in pages, any image the browser shows. Defaults to
  `images/icon.svg`, as before.
- A storage for the device: `ISyncStorageResolver.Get/SetDeviceDefaultKindAsync`. A profile with no storage of its own
  recorded yet (e.g. one the user creates) uses it, if it can. Moving the files to another storage in Settings also makes
  it the device's storage. With no profiles at all yet, Settings offers the storage choice anyway, and choosing just
  records it for the profiles created later.
- A protected profile's own idle timeout: `ISyncProtectedProfileService.SetIdleTimeoutAsync` (and
  `IE2eProtectedProfileManager.SetIdleTimeoutAsync`). It is kept in the index of the user's profiles
  (`E2eProfileIndexEntry.IdleTimeoutSeconds`), so it applies on every device, over the app's `ProfileDefaults`, and takes
  effect at once (`IE2eIdleLock.Refresh`).

### Changed
- Unlocking a protected profile with the passkey is now "Unlock with biometrics", turned on from the unlock panel.
  Unlocking with the passphrase there offers "Unlock with biometrics next time on this device", when the profile's
  settings allow it and the signed in user is remembered here with a passkey ("Remember me"). From then on the
  panel shows "Unlock with biometrics".
  - **The passkey is asked for** (fingerprint, face or PIN) whenever its key is not in memory. Before, the key was
    only there after a sign in with the passkey, so after a reload the profile needed its passphrase until the next
    such sign in.
  - **Settings no longer has** "Use passkey on this device" or "Unlock with passkey". `E2eProfilesViewModel` loses
    `EnableDeviceUnlockAsync` and `UnlockWithDeviceAsync`.
  - **New API:**
    - `IPasskeyKeysService` (UI): whether the user has a passkey here, and making sure a key derived from it is in
      memory, asking for the passkey if not.
    - `ISyncProtectedProfileService.HasDeviceUnlockAsync` and `DisableDeviceUnlockAsync`.
    - `IE2eProtectedProfileManager.HasDeviceUnlockAsync`.
    - `E2eProfileRow.CanSetUpDeviceUnlock`.
    - `E2eProfilePresenter` and `E2eUnlockPanelViewModel` take an `IPasskeyKeysService` (and the panel an
      `IToastService`), created by DI.
  - **Text:** `Sync.E2e.UnlockWithPasskey` now reads "Unlock with biometrics"; `Sync.E2e.UsePasskey` is gone; new
    `Sync.E2e.RememberBiometrics` and `Sync.E2e.Error.BiometricsNotSetUp`.
- The unlock panel syncs the profile before unlocking it (online, and with sync on), so what it shows once open is up
  to date. A failed pass does not stop the unlock. `ISyncScheduler.SyncProfileAsync(profile)` runs a pass of one
  profile and waits for it (and for a pass already running). `E2eUnlockPanelViewModel` takes an `ISyncScheduler`
  (created by DI, so apps need no change).
- Protected profiles' labels are no longer kept on the device in plain text. A label can say what a profile holds,
  e.g. a vault's name. They are kept encrypted (AES-GCM) with a new key derived from the passkey at sign in,
  `E2eKeyNames.LabelKey`, which the sync module adds to the PRF keys. Without it (signed in with a password, or after
  a reload until the next passkey sign in) labels are not read or kept, so profiles show by their ids. A label set then
  lasts until the page is closed. `LocalStorageE2eProfileLabelStore` takes an `ICryptoService` and an
  `IDerivedKeyStore` (created by DI, so apps need no change). Labels kept in plain text before are no longer read.
  Apps created from the template with the notes example have a UI test that expects a notebook's name after a reload
  (`NotebooksTests.GivenANotebook_WhenReloaded_ThenItIsLockedAndShownByNameHereAndInSettings`). The tests sign in with
  a password, so the notebook now shows its id: change the test as the template's
  (`GivenANotebookAfterAPasswordSignIn_WhenReloaded_ThenItIsLockedAndShownById_AndItsNameWasNotKept`).
- Sync profiles that would clash are refused when they are added, with an error naming both:
  - remote patterns that could match the same files (one's fixed folders, before the first wildcard, the same as or
    inside the other's), e.g. `documents/**/*` and `documents/**/*.md`;
  - two profiles keeping data in the same IndexedDB object store (index, files or file metadata) or OPFS directory.
  A profile set up in code may not cover `profiles/`, where the profiles users create are kept, so `**/*` is refused
  too. Custom stores (`UseIndexStore<T>`, `UseFileStore<T>`) are not checked, and the picked local folder is shared by
  every profile, as before. An app whose profiles overlapped stops at startup: give each profile a folder of its own.
- Text from resources is looked up once for each culture and name, then kept (`OverridingStringLocalizer`). Each lookup
  went through every override and resource manager, which in WebAssembly made a page that shows a lot of text, such as
  a long list, many times slower to render. Overrides should be set before text is used, as `AddBlazwizText` does; if
  their number changes afterwards, what was kept is forgotten.

### Fixed
- In a phone's browser (not installed), the bottom of the sidebar, with Log out, was hidden behind the browser's
  toolbar: the shell used `100vh`, which mobile browsers measure as if their toolbars were hidden. It now uses the
  visible height (`100dvh`, with `100vh` where that is not supported).
- On a phone the update banner ("A new version of the app is available") was squeezed into half the screen, a word to
  a line, and covered the sync status pill. It now takes the screen's width and sits above the pill.
- Deleting a protected profile that failed after its keys were deleted (e.g. removing one of its files) left it
  listed on this device, locked and impossible to unlock, until the next sign in. Once its keys are gone it is now
  removed whatever happens to its files; the error is still thrown.
- With more than one sync profile, the sync status showed only the last profile synced: a profile that failed was shown
  as Idle with no error once another synced after it, and "last synced" moved on although it had not synced. The status
  now covers every profile's last pass: Error if any failed, "last synced" when the least recently synced profile last
  synced, and the conflicts all of them resolved. A profile held back (e.g. locked) or removed no longer counts.
- A dialog opened over another (e.g. a picker over an editor) now dims the one beneath.
- The theme preview in Settings and range sliders use the app's accent instead of the framework's purple.
- An app with no sync profiles yet (all of them created by the user at runtime, none defined in code) now checks
  connectivity when sync starts, on focus and when back online. Before, the sync status pill said "Checking" forever and
  the Sync page logged no connectivity events until the first profile was created.
- The Settings storage choice is loaded again when a profile is added or removed. Before, in an app with no profiles in
  code, it stayed hidden after the user created their first protected profile until the page was opened again.
  `SyncSettingsViewModel` takes an `ISyncProfileRegistry` (it is created by DI, so apps need no change).

## 0.4.0

### Added
- Profiles can have a display name from resources: `UseDisplayText(key, resourceType)`, shown by Settings and the
  Sync page's profile filter (`ISyncProfileDisplayNames`).
- `E2eUnlockPanel`: wrap a page's content of a profile in it, and while the profile is locked it shows a passphrase
  form (and the passkey, where allowed) in its place. The template's Files and note editor pages use it.
- A lock indicator beside the sync status pill: how many profiles are unlocked, with Lock all.
- `ISyncProtectedProfileService.Profiles`, `IsLoaded` and `ProfilesChanged`: the user's profiles, and whether each has
  a passphrase yet.
- Labels for protected profiles, kept only on the device and never synced: `ISyncProtectedProfileService.SetLabelAsync`,
  stored by `IE2eProfileLabelStore` (local storage). Settings, the unlock panel and the Sync page's profile filter show
  a profile by its label in place of its id. A label is removed when its profile is deleted, on this device or another.
  The template labels each notebook with its name whenever it is unlocked, so a notebook shows its id on a device until
  it has been unlocked there.
- The template's notebook page has a Lock button: it locks the notebook and goes back to the Notebooks page.
- Sync profiles can be classes deriving from `SyncProfileDefinition`, added with `AddProfile<TProfile>()` or found in
  an assembly with `AddProfilesFromAssemblyContaining<T>()`.
- Protected profiles, each with its own passphrase and keys (`IE2eProtectedProfileManager`): create, list, unlock
  (offline from this device's copy too), lock, lock all, change the passphrase and delete. Each is identified only by
  a GUID, with its keys in its own secret (`<sub>/keys/<id>`) and the user's profiles listed in an index secret
  (`<sub>/keys/index`). Each syncs as a sync profile of its own (`profiles/<id>/` in the user's prefix, its own
  local storage), added when the session starts, created and deleted with `ISyncProtectedProfileService`, and locked
  and removed when the user signs out. A locked one keeps syncing its encrypted files.
- Sync profiles can be added and removed while the app runs (`ISyncProfileRegistry`); configured profiles are still
  reached as keyed services.
- Sync settings and profiles can be set in configuration as well as in code: `UseEmbeddedConfiguration` reads the
  `Sync` section of a JSON file embedded in the app, and `UseConfiguration` reads any configuration section. Unknown
  keys and invalid values stop the app at startup with an error naming the setting.

### Changed
- Every sync profile is end to end encrypted with its own keys and passphrase, named by its id: `UseKeyId(<guid>)`
  (or `Id` in configuration) replaces `UseEncryption()` and `Encrypted`, and is required. A profile is locked until its
  passphrase is entered, and does not sync while locked. `UseProfileSettings` sets when it locks itself after being
  idle, whether it can be unlocked with the passkey, its minimum passphrase length and Argon2id costs;
  `UseProfileDefaults` sets them for profiles users create. Unlocking or locking a profile does so in every tab.
- End to end encryption keys belong to a key scope (`E2eKeyScope`). `IUserKeysStore` and `IE2eLocalKeyStore` take the
  scope; apps that implement them must add it. `IE2eKeyRings` gives each scope its key ring.
- Encrypted files are bound to their profile and path, so a file cannot be moved to another path or profile and still
  be read. Files encrypted by earlier builds cannot be read.
- Without a Cognito user pool (local development), the keys are kept in the browser, so encryption works locally.
- The sync module reads the Argon2id settings for new keys from the optional `Argon2id` configuration section. The
  template's `appsettings.Development.json` lightens them for local development and the UI tests.
- Settings lists the profiles set up in code, by display name, and the protected profiles the user created, by id:
  set a first passphrase, create, unlock (with the passkey too, where allowed), lock, lock all, change the passphrase
  and delete (confirmed by typing the id). Encryption can no longer be turned off. The banner shows only while a
  profile set up in code has no passphrase yet.
- Deleting a protected profile finds its files in S3 before deleting its keys, and stops with nothing deleted if S3
  cannot be reached.
- The template's notes example has notebooks: the notes profile is the default notebook, and users create more,
  each a protected profile with its own passphrase, its name kept encrypted inside it. A Notebooks page lists them
  (the sidebar's Files item is now Notebooks); the Files page and editor show the notebook named in the URL.
- A protected profile deleted on another device is forgotten on this one when the profiles are next loaded: its keys
  (`IE2eProtectedProfileManager.ForgetAsync`), its files and its sync index here are deleted too.
- The AWS base stack lets each user manage their secrets under `<sub>/keys/` (each profile's keys and the index) in
  place of the single `<sub>/keys` secret; redeploy with `deploy.ps1`. `test-auth.ps1` checks them.
- The template's sync profiles are classes (`NotesSyncProfile`, or `FilesSyncProfile` without the notes example),
  found by `AddProfilesFromAssemblyContaining` in `Program.cs`; `AddNotesProfile` is gone. Its other sync settings are
  in `MyApp.Portal/sync.json`, embedded in the app.

### Removed
- The account-wide passphrase and keys: `IE2eKeyManager`, `E2eWriteMode`, `E2eSyncGate`, `ISyncEncryptionSweeper`,
  `ISyncEncryptionService`, `E2eStatus`, `E2eSettingsViewModel`, `E2eAlreadyEnabledException` (`E2e.AlreadyEnabled`)
  and the account key scope. Plain files are no longer synced or read by encrypted profiles.
- The template's per-note lock icons (`NoteEntry.EncryptionKeyId`): locked notes are not shown at all.

### Fixed
- In development builds, the update banner's Update button did nothing: the template's development service worker
  now takes over when asked, as the published one does.

## 0.3.0

### Changed
- The template's options are lower case, with no single-letter forms: `--title`, `--description`, `--tests`
  (was `--IncludeTests`), `--showcase` (was `--IncludeShowcase`), `--sync`, `--notes` and `--no-restore`.
- Betas are published to nuget.org only, without a tag or GitHub pre-release. Releases from main still get both.

## 0.2.0

### Added
- Each release has a GitHub release and tag, with its changelog section and packages; betas of branches are GitHub
  pre-releases.
- A README for each package and the template on nuget.org: what it does, how to register it and its settings.

## 0.1.0

The first release as packages. Apps created from the template build on the `Devoctomy.Blazwiz.*` packages and upgrade
by changing `BlazwizVersion` in their `Directory.Packages.props`.

### Added
- The packages: `Core`, `Cryptography`, `Encryption`, `Browser`, `Auth`, `Auth.Cognito`, `Sync`, `UI`, `UI.Sync` and
  `Testing`, each registered with its own `AddBlazwiz*` method.
- Extension points: shell regions, Settings sections, sidebar items, sign-in options, session lifecycle handlers,
  page assemblies, and replacing or turning off the framework's pages (`BlazwizPages`).
- Text from resources that apps can reword and translate (`AddBlazwizText`), coded errors, plural forms, and a
  pseudo-localisation mode for finding hard-coded text (`Text:PseudoLocalization`).
- Choosing the language in Settings (`Text:SupportedCultures`), applied at startup.
- Changing the password (Profile page) and resetting a forgotten one (`/forgot-password`), with Cognito or the
  placeholder sign in.
- Data migrations (`IDataMigration`, `AddDataMigration`), run at sign in, for stored data from 1.0 on.
- The framework's version in Settings > About.
- `Devoctomy.Blazwiz.Aws`: the AWS base stack and its deploy, test and remove scripts, released with the framework.
  Apps run it through their own `cloudformation` scripts, and keep their own resources in `cloudformation/app.yaml`,
  deployed as a second stack that imports the base stack's outputs.
- `Devoctomy.Blazwiz.Testing`: `FrameworkText`, to test an app against the framework's real text and to check its
  text overrides still match it.
- Template options `--Sync` and `--Notes`. Syncing without end to end encryption is not offered
  (`docs/backlog/sync-without-encryption.md`).

### Changed
- In the template the app is called `MyApp` (`myapp`, `MYAPP_`), and everything it keeps in the browser is named
  after it.
