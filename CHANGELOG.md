# Changelog

Every `Devoctomy.Blazwiz.*` package and the `Devoctomy.Blazwiz.Templates` template share one version (see
`docs/versioning.md`). Each release lists what changed for apps; breaking changes say what to do, and are repeated
in `UPGRADING.md` from 1.0.

Before 1.0 any release may break, and nothing is kept for earlier builds: no data migrations, no compatibility code.

## Unreleased

### Added
- **Sign out everywhere.** `IAuthenticationService.SignOutEverywhereAsync(reason)` revokes every session the user
  has (Cognito `GlobalSignOut`), runs every `ISignOutEverywhereHandler` (`AddSignOutEverywhereHandler<T>()`), then
  signs out here. When Cognito cannot be reached, or in offline mode, it returns false and nothing changes. The Profile
  page offers it, with a confirmation, when a handler is registered.
- **User events**, in two new packages:
  - **`Devoctomy.Blazwiz.Events`:** a channel the user's open devices share (`IUserEventChannel`), with a local
    channel between one browser's tabs for development and tests.
  - **`Devoctomy.Blazwiz.Events.AppSync`:** the channel over AWS AppSync Events, reaching every device, with no Amplify.
  When a device signs out everywhere, the others end their sessions at once, and the sign-in page says which device
  did it. `AddBlazwizAppSyncEvents(configuration, allowLocalChannel)` reads the new `Events` configuration section.
  Outside development, without it, the app runs without events and logs a warning.
- **The resume check.** Whenever the page becomes visible again or the browser comes back online, the session is
  checked with Cognito (`CheckSessionAsync`, `GetUser`). A device that was closed, offline or asleep when the user
  signed out everywhere signs itself out as soon as it wakes. `IPageResumeService` (Browser) raises the event, and
  `ISessionResumeMonitor` (Auth) runs the check.
- **The device's name** (`IDeviceNameService`, Browser), kept in local storage (`device_name`). It defaults to
  `<Platform>-<Browser>-<6 digits>` (e.g. `Android-Edge-624834`), and can be renamed under Settings, This device. The
  section is shown when user events are on.
- **The base stack:**
  - A new `Events` parameter (`disabled` by default; apps created with user events pass `enabled`) creates an AppSync
    Events API, `<AppName>-<Environment>-events`, with a `users` namespace whose handlers allow each user only their
    own channel. It also adds the outputs `EventsHttpEndpoint` and `EventsRealtimeEndpoint`. An app made before user
    events existed passes nothing, so upgrading it creates no API.
  - `deploy.ps1` takes `-Events`, writes the endpoints to `appsettings.Production.json` and adds them to the
    Content-Security-Policy.
  - `test-auth.ps1` checks the channel's isolation and that signing out everywhere revokes the session
    (`-SkipSignOutEverywhere` leaves that out).
  - `deploy-policy.json` gains an `AppSyncEvents` statement.
- **The template's new `--events` option** (on by default) adds user events. Apps created with `--events false` are
  told at creation what that leaves out, and deploy with `-Events disabled`.

### Changed
- `IAuthenticationService.SessionEnded` is now `EventHandler<SessionEndedEventArgs>`, with the reason
  (`SessionEndedReason`) and the device that ended the session. `IAuthenticationService` also gains
  `SignOutEverywhereAsync`, `EndSessionAsync` and `CheckSessionAsync`. Handlers of `SessionEnded` take the new
  arguments, and implementations of `IAuthenticationService` add the new members.
- `LocalAuthenticationService`, `CognitoAuthenticationService`, `MainLayoutViewModel`, `LoginViewModel` and
  `ProfileViewModel` take new constructor parameters. This only matters to code that constructs them itself, such as
  tests; dependency injection supplies them.
- **The keys of protected profiles moved from Secrets Manager to the data bucket, and are written conditionally.** Each
  profile's keys document is now `<identity id>/keys/<profile id>` and the index of them `<identity id>/keys/index`,
  beside the user's files (`S3UserKeysStore`, in Sync). Every write is conditional (`If-None-Match: *` to create,
  `If-Match` to replace), so two devices changing a profile's passphrase, or adding, deleting or changing profiles, at
  the same time can no longer silently undo one another: `E2eProtectedProfileManager` reads the document again and
  reapplies its change (up to `MaxWriteAttempts`, then `E2eDocumentChangedException`, code `E2e.DocumentChanged`,
  shown with the new `Error.E2e.DocumentChanged` text). Users see no difference.
  - `IUserKeysStore` and `IE2eProfileIndexStore` return a `StoredDocument` (value and ETag), take the ETag when
    writing and return the new one; `TryCreateAsync` returns the ETag, or null when the keys exist. Implementations
    of them change accordingly. `E2eLocalKeys` keeps the version of its copy (`CachedDocumentETag`).
  - `AddBlazwizSync` now registers the keys stores: in the data bucket when `IS3SyncTargetProvider.IsConfigured`,
    otherwise in local storage (local development; keyed on the data bucket, no longer on the user pool). It needs
    `ILocalStorageService` (`AddBlazwizBrowser`).
  - No sync profile's remote pattern may reach `keys/`: `AddProfile` refuses it, as it does `profiles/`.
  - Removed: `SecretsManagerUserKeysStore`, `ISecretsManagerClientFactory` and `SecretsManagerClientFactory` (UI.Sync),
    and the `AWSSDK.SecretsManager` dependency.
  - **The base stack:** the role loses the `per-user-keys` policy (Secrets Manager), the `userSub` principal tag and
    `sts:TagSession`, and is now denied `s3:DeleteObjectVersion`, `s3:PutBucketVersioning` and
    `s3:PutLifecycleConfiguration` (`NoVersionPurge`), so a deleted or replaced keys document can be restored by an
    operator for 30 days. The Content-Security-Policy no longer allows Secrets Manager. `remove.ps1` no longer deletes
    users' secrets, and `deploy-policy.json` loses its Secrets Manager statements. `test-auth.ps1` checks the
    conditional writes, that the user cannot delete a version, and that another user's keys are refused. Redeploy with
    `deploy.ps1`.
  - Keys documents already in Secrets Manager are not moved: set the profiles up again, or copy each secret once into
    `<identity id>/keys/` with the AWS CLI. The stack deletes nothing in Secrets Manager, so they stay restorable until
    you delete them.

## 0.7.2

### Changed
- blazwiz is licensed under the Apache License 2.0 instead of MIT, from this version: the packages declare
  `Apache-2.0` and carry the `NOTICE` file. Versions already published stay under the licence they were published with.
  Apps built on blazwiz keep whatever licence their authors choose; the licence adds an explicit patent grant and asks
  that the NOTICE file's attribution travels with redistributed copies of blazwiz itself.

## 0.7.1

### Fixed
- `deploy.ps1` could not apply the stack policy to an environment without audit logging (every environment but prod,
  by default): the policy names the audit log bucket, and CloudFormation refuses a policy naming a resource the stack
  does not have (`Unknown logical id 'LogicalResourceId/AuditLogBucket'`). The stack kept the policy from before 0.7.0,
  so the CloudFront distribution and the app client were not protected. The policy is now applied with only the
  protected resources the stack has. Deploy to pick it up.

## 0.7.0

### Added
- A small indicator in the bottom corner while a new version of the app downloads in the background
  (`AppUpdateIndicator`, in both layouts), with the percentage of files cached when the service worker reports it. On a
  slow connection the download took minutes with nothing to show for it until the update banner appeared. The
  template's `service-worker.published.js` now caches files a few at a time, posts `INSTALL_PROGRESS` to the open
  pages after each, and keeps the files an earlier, failed attempt at the same version had cached. Apps copy it to get
  the percentage; without it the indicator shows but no percentage.

### Security
From the security review of 2026-11-08 (`docs/reviews/2026-11-08-security-review/security-review.md`), every finding
whose fix needs no functional change:
- Idle auto-lock now applies to profiles set up in code, such as the template's `files`: `E2eIdleLock` looked them up
  by name with their key scope id, so only profiles users create were ever locked for being idle (SR2).
- The passkey-derived keys (`rememberme`, `e2e-kek`, `e2e-labels`) are wiped on sign out by a new Auth session
  lifecycle handler, `DerivedKeySessionHandler`, not only by the framework's login page, so an app with its own login
  page no longer keeps them in memory (SR9).
- The WebAuthn bridge hands the PRF output to .NET as bytes (wiped on both sides) instead of a hex string, reports the
  real user presence and verification flags from the authenticator data (they were always false), and gives new
  passkeys a random user handle instead of one holding the email address. `IWebAuthnService.RegisterAsync` lost its
  unused `userId` parameter (SR10, SR32, part of SR1).
- Remembered passwords and profile labels are encrypted with `IAeadCipher` and bound to the user they belong to
  (`IPasswordCryptoService.EncryptAsync`/`DecryptAsync` take the email), in place of the stream-based `ICryptoService`,
  which is removed with `BouncyCastleAesGcmCryptoService`. Passwords remembered and labels kept by earlier builds are
  dropped; signing in with the password offers the passkey again (SR24).
- A profile's master key wrapped for device unlock is bound to the profile, so one moved to another profile's entry
  does not unwrap. Device unlock set up by earlier builds falls back to the passphrase (SR23).
- A keys document's Argon2id settings are checked against upper bounds (`E2eKeysDocumentService.MaxMemoryKib`,
  `MaxIterations`, `MaxParallelism`) and the cipher's key size (`IKeyWrapService.KeySize`) when parsed, so a tampered
  document cannot exhaust the browser's memory (SR30).
- An empty file in an encrypted profile is reported as not encrypted instead of read as empty content; folder markers
  still pass through (SR19).
- Loading the profile index no longer replaces a code-defined profile's whole settings with the defaults for created
  profiles; only an idle timeout chosen for the profile is taken from the index (SR18).
- The conflict resolver ignores a remote edit time more than `RemoteObjectInfo.MaxClockSkew` (5 minutes) after the
  object's upload time, so S3-side metadata cannot make a version win every conflict (SR22).
- `GlobSyncPathMatcher` never matches keys with empty, `.` or `..` segments or backslashes, which the glob library would
  normalise and the stores would not (SR36).
- The template's note editor refuses note keys from the URL with such segments or control characters (SR33).
- `ChangePasswordViewModel` and `PasskeyKeysService` match the saved user with one rule, `SavedUser.IsFor` (SR42).
- `Login.Passkey.Question` is plain text rendered with encoding, not markup (SR41).
- The base stack: the data bucket is versioned (old versions kept 30 days), the user pool has deletion protection in
  `prod` (`UserPoolDeletionProtection`), a CloudTrail trail records data bucket access in `prod` (`AuditLogging`), email
  changes need verification and the app client's attributes are limited, the public client no longer allows
  `USER_PASSWORD_AUTH` (`test-auth.ps1` signs in with the admin flow as the deployer), local development origins are
  only in the data bucket's CORS where asked (`LocalDevOrigins`, none for `prod`), and the users' keys secrets are
  tagged `app=<AppName>` so `remove.ps1` deletes them with the environment. The scripts keep passwords off the command
  line, `test-auth.ps1` also checks that another user's prefix cannot be read or listed and no longer writes the real
  index, and the deployer policy gained the actions these need (SR4, SR11, SR12, SR13, SR14, SR15, SR16, SR17 in part,
  SR48, SR49). Deploy again after upgrading.
- GitHub Actions are pinned to commits (Dependabot keeps them current), the branch name reaches the release scripts
  through the environment, and checkouts do not keep the job token (SR7, SR25, SR26).
- `templatepack/test-template.ps1` refuses a `-WorkRoot` that already exists instead of deleting it on failure (SR8).
- Every .NET security analyzer rule is on (`AnalysisModeSecurity` `All`) (SR27).
- `.gitignore` covers more secret-bearing files; a root `nuget.config` restores from nuget.org only; `LICENSE`,
  `THIRD-PARTY-NOTICES.md` and the vendored libraries' licence files are added (SR28, SR44, SR45).
- The template removes quotes, angle brackets, ampersands and backslashes from `--title` and `--description`, which
  would have broken the generated app's JSON, HTML and tests (SR29).

And the review's smaller functional changes:
- Passkeys must verify the user (fingerprint, face or PIN): the WebAuthn ceremonies require it, and keys are derived
  from the PRF output only when the authenticator reports it (`WebAuthnRegisterResult.UserVerified`). A passkey that
  cannot verify the user is no longer offered for "remember me"; one set up without verification needs setting up
  again (SR1).
- A profile's keys secret is deleted with a 7 day recovery window (`SecretsManagerUserKeysStore.RecoveryWindowDays`),
  and the base stack's role refuses forced deletion, so an operator can restore the keys of a profile deleted by a bug
  or a hijacked session. The app treats them as gone (SR5).
- A profile missing from the index of the user's profiles is only forgotten (and its local files deleted) once its
  keys document is gone too; while the keys exist the entry is put back
  (`IE2eProtectedProfileManager.TryRestoreToIndexAsync`). Two devices writing the index at once no longer lose a
  profile (SR20).
- Deleting a profile refuses while sync is set up but has no credentials right now, instead of deleting the keys and
  leaving its files in S3 undecryptable. `IS3SyncTargetProvider.IsConfigured` tells the two apart, and the S3 store
  reports `Sync.Remote.NotConfigured` when nothing is set up to sync with (SR21).
- `AddBlazwizCognito(configuration, allowPlaceholderSignIn)`: the placeholder sign in (any credentials) now stands in
  for Cognito only where the app allows it. The template and clypse pass the host environment's `IsDevelopment()`, so a
  deployed app whose settings are missing fails to start instead of letting anyone in (SR34).
- A pass downloads no remote file larger than `SyncOptions.MaxFileSize` (100 MiB by default, `UseMaxFileSize`); larger
  ones are reported as failed (`Sync.Remote.TooLarge`, with the sizes) and left in the cloud (SR39). `SyncEngine` takes
  the `SyncOptions`.

### Fixed
- The sync status could say "Offline: no credentials" for up to half a minute after sync was turned off (or on), when
  a connectivity check was already running at the time: its stale answer was kept. A check that finishes after the
  switch changed now runs again. Seen as a flaky `GivenNoCloud_WhenSyncSwitchedOff_...` UI test in CI.
- The minimum IAM policy in the template's `cloudformation/README.md` lacked `cloudformation:GetTemplateSummary`, which
  deploying the app's own stack (`aws cloudformation deploy`) needs. With that policy, a deploy updated the base stack,
  then failed with AccessDenied on the app's stack. Add the action to policies made from the README.
- Unlocking a profile with the passphrase and "unlock with biometrics from now on" ticked asks for the passkey first,
  then unlocks. Before, the profile unlocked and the biometric prompt appeared afterwards, as if unrelated. A wrong
  passphrase does not prompt again on the retry: the device key derived the first time is kept.

### Changed
- `deploy.ps1` keeps the stack's current value of any optional parameter it is not given (`-CustomDomainName`,
  `-CertificateArn`, `-LocalDevOrigins`, `-UserPoolDeletionProtection`, `-AuditLogging`) instead of resetting it to the
  default. Before, an upgrade run that left out `-CustomDomainName` removed the custom domain from the distribution and
  from the data bucket's allowed origins, and the portal lost access to S3 (`Offline: S3 unreachable`).
- The base stack's `stack-policy.json` also protects the audit log bucket, the CloudFront distribution and the app
  client from being replaced or removed, so `deploy.ps1` refuses a change set that would (losing the logs, changing the
  portal's address, or ending every session) unless `-AllowStatefulReplacement` is given, as it already did for the user
  pool, identity pool and data bucket. A workflow run with `-Force` is refused the same way.
- The template's minimum IAM policy for deploying is now a file, `cloudformation/deploy-policy.json`, in place of a block
  in `cloudformation/README.md`, which shows how to create the policy from it.
- The template's UI tests no longer build and start the portal with `MSBUILDDISABLENODEREUSE=1`. It worked around a
  build failure that no longer happens.
- The template's UI tests derive passphrase keys with the lowest Argon2id costs (64 KiB, 1 iteration) instead of the
  app's Development settings, so setting a passphrase no longer holds up the page long enough to time out on slow CI
  runners. They run about a minute faster.

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
  (`docs/plans/backlog/sync-without-encryption.md`).

### Changed
- In the template the app is called `MyApp` (`myapp`, `MYAPP_`), and everything it keeps in the browser is named
  after it.
