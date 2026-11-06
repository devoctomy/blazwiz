# Changelog

Every `Devoctomy.Blazwiz.*` package and the `Devoctomy.Blazwiz.Templates` template share one version (see
`docs/versioning.md`). Each release lists what changed for apps; breaking changes say what to do, and are repeated
in `UPGRADING.md` from 1.0.

Before 1.0 any release may break, and nothing is kept for earlier builds: no data migrations, no compatibility code.

## Unreleased

### Fixed
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
