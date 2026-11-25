# Changelog

Every `Devoctomy.Blazwiz.*` package and the `Devoctomy.Blazwiz.Templates` template share one version (see
`docs/versioning.md`). Each release lists what changed for apps; breaking changes say what to do, and are repeated
in `UPGRADING.md` from 1.0.

Before 1.0 any release may break, and nothing is kept for earlier builds: no data migrations, no compatibility code.

## Unreleased

## 0.9.5

### Added
- `IPasskeyKeysService.ForgetKey(keyName)` wipes a key derived from the passkey from memory.
- `IRememberedDeviceService.Forgotten`, raised once a device has been forgotten. `RememberedDevicesViewModel` drops it
  from the list.
- `ISavedDeviceCredentialService` (`DropAsync`): drops the signed in user's stored credential for this device once it
  has been forgotten. `RememberedDevicesViewModel` takes it in place of `ILocalStorageService`.

### Changed
- **Renaming a remembered device warns first, and forgets it.** Cognito can't change the name a device was remembered
  under, so the This device section now says so before saving a new name for a remembered device: renaming it forgets
  it, and the next sign in there asks for a code, then remembers it under the new name. `DeviceSettingsViewModel` takes
  `IRememberedDeviceService`, `ISavedDeviceCredentialService` and `IErrorText`, and has `ForgetWarning`, `IsBusy`,
  `ConfirmForgetCommand` and `CancelForgetCommand`. New text: `Settings.Device.Forget.Text` and
  `Settings.Device.Forget.Confirm`.
- `E2eProfilesViewModel` takes `IPasskeyKeysService`, to ask for the passkey before a passphrase change.

### Security
- **The passkey's key encryption key (`e2e-kek`) no longer stays in memory.** Signing in with the passkey left it there
  for the whole session, so unlocking a protected profile with biometrics, even after locking it, never asked for the
  passkey. It is now wiped once the session has started (`E2eDeviceKeySessionHandler`), after every unlock with
  biometrics (`E2eDeviceUnlockService`, `E2eUnlockPanel`), when a profile locks (`E2eProtectedProfileManager.Lock`),
  and after a passphrase change, which asks for the passkey first when biometrics are on for the profile here so they
  stay on. Every unlock with biometrics now asks for the fingerprint, face or PIN. The `e2e-labels` key is kept, so
  profile labels still show while locked.

## 0.9.4

### Added
- **Two-step verification with an authenticator app (TOTP).** Users turn it on in their Profile
  (`MfaSettingsPanel`): an "Open in authenticator app" link for a phone, a QR code to scan from another screen, and the
  key with a Copy button. Signing in then asks for the app's code after the password. The base stack's new `Mfa`
  parameter (`deploy.ps1 -Mfa`) is `optional` by default; `required` makes every user set up an app at their next sign
  in, and `off` turns it off. `IMfaService` turns it on and off in code, with a placeholder for local development
  that accepts any six digits.
- **Remembered devices.** A device that signs in with a passkey is remembered (Cognito remembered devices; the base
  stack now sets `DeviceConfiguration`), so it is not asked for a code again. Its credential is kept with the
  remembered password, encrypted with the passkey. Settings lists the user's remembered devices with a Forget button
  on each (`RememberedDevicesSettingsSection`, `SettingsOrder.RememberedDevices`), and signing out everywhere forgets
  them all. `IRememberedDeviceService` lists and forgets them in code.
- `AuthenticatorSetupPanel` and `AuthenticatorSetupViewModel`, to show an authenticator app's secret in your own
  pages, and `IQrCodeService`, which draws QR codes (with QRCoder, a new dependency of `Devoctomy.Blazwiz.UI`).

### Changed
- **Breaking:** `IAuthenticationService` has new members: `LoginAsync(username, password, rememberedDevice)`,
  `CompleteMfaAsync`, `CompleteMfaSetupAsync` and `RememberDeviceAsync`. `LoginResult` has `MfaCodeRequired`,
  `MfaSetupSecret` and `RememberedDeviceRejected`. An app that implements `IAuthenticationService` itself must add
  them.
- **Breaking:** `CognitoAuthenticationService` takes an `ICognitoCurrentDevice`, and `LoginViewModel` an
  `IDeviceNameService` and an `AuthenticatorSetupViewModel`. Both are resolved through dependency injection, so this
  only matters to code that constructs them itself.
- Signing out everywhere forgets every remembered device before revoking the user's sessions.
- Deploy after upgrading: the stack's user pool changes in place (MFA and device tracking). No user, session or data
  is lost.

## 0.9.3

### Added
- **`ModalDialog` has a `FocusMode` parameter** that says what is focused when the dialog opens: `FirstInput` (the
  default), `FirstButton`, `Dialog`, `Autofocus` (the first element marked `autofocus`) or `None`. `ModalDialog`'s
  logic now lives in `ModalDialogViewModel`.
- **A guide for building apps on blazwiz**, in `docs/blazwiz`.

### Changed
- **A dialog with no inputs focuses the dialog itself, not its first footer button.** This applies with the default
  `FirstInput`, so pressing Enter in a confirmation no longer presses its first button. Set
  `FocusMode="ModalDialogFocusMode.FirstButton"` to keep the old behaviour. **Breaking:**
  `IBrowserInteropService.FocusFirstInputAsync` returns `Task<bool>` (whether it found an input) and no longer falls
  back to a footer button. `FocusFirstButtonAsync`, `FocusAutofocusAsync` and `FocusAsync` are new.

## 0.9.2

### Fixed
- **A deploy from Linux or macOS now clears CloudFront's cache.** `deploy.ps1` invalidated `/*`, but PowerShell on
  those systems replaced that argument with the machine's own root folders (`/bin`, `/etc`, ...) before the AWS CLI
  saw it, so the invalidation succeeded and cleared nothing. CloudFront kept serving the previous copies of files
  whose names do not change (stylesheets, for example) for up to a day, and an installed app's update then failed
  while downloading: the service worker rejects a file that does not match the new version's hash. The path is now
  passed in a file. Deploys from Windows were not affected. An environment deployed before this fix needs one
  invalidation of `/*` made by hand, or a day for the old copies to expire.

## 0.9.1

### Changed
- **The passphrase form's components hold no logic of their own.** `E2ePassphraseForm` and `Argon2idEditor` no longer
  listen to the view model they are given: they are shown again whenever the view showing them is, so the view model
  that owns an `E2ePassphraseFormViewModel` says when it changes. `E2eProfilesViewModel`, `E2eUnlockPanelViewModel`
  and the template's `NotebooksViewModel` raise `PropertyChanged` for their `PassphraseForm` whenever the form does,
  and `E2ePassphraseFormViewModel` raises it for `Argon2` whenever the Argon2id settings do. **Breaking:** a view model
  of your own that owns the form does the same (see the `Devoctomy.Blazwiz.UI.Sync` README), and the two components'
  `Dispose` and `OnParametersSet` members are gone.
- **Closing the profile dialog is decided in its view model.** New `E2eProfilesViewModel.CloseDialogAsync(closed)`
  closes the dialog and then calls `closed`, and does neither while the dialog's change is running; `E2eProfileDialog`
  uses it for its `OnClosed`.
- **The wait before an Argon2id derivation in the browser can be tested.** `BouncyCastleArgon2idKeyDerivationService`
  has a second constructor taking the new `IRuntimeEnvironment` (`IsBrowser`; implemented by `RuntimeEnvironment`) and
  a `TimeProvider`, both registered by `AddBlazwizCryptography` and `AddBlazwizE2eEncryption`. Its behaviour is the
  same.

### Removed
- `E2eProfilesViewModel`'s unused private `RunActionAsync`.

## 0.9.0

### Added
- **Any Settings section can be taken off the page.** `services.RemoveSettingsSection<TComponent>()` hides a section
  whoever added it (the framework, a module or the app), whether it is added before or after the call. New
  `SettingsSectionRemoval`. **Breaking:** `SettingsSectionRegistry`'s constructor takes the removals too; apps that
  register through `AddBlazwizUi` need no change.
- **Every dialog that sets a passphrase has an Advanced tab with its Argon2id settings.** New protected profile, Set
  passphrase and Change passphrase in Settings, and the template's new notebook dialog, are now two tabs: what the
  dialog asked for before, and Advanced (memory, iterations and parallelism, with a Benchmark button). The settings
  start at the ones that apply: the app's for a new passphrase, the profile's current ones when changing a passphrase.
  Anything Argon2id can run may be entered; there is no upper limit. Settings left as they start are used as they are;
  changed settings are only accepted once a benchmark has completed with exactly those values on the device, and the
  dialog's confirm button is disabled until then (`E2ePassphraseFormViewModel.CanConfirm`). The key
  length is fixed, because the key wraps the profile's master key. New components `E2ePassphraseForm` (the form, for
  any dialog of your own that sets a passphrase) and `Argon2idEditor`, new `E2ePassphraseFormViewModel` and
  `E2ePassphraseFormTab`, new `Argon2idBenchmarkViewModel` members `Reset`, `GetParameters`, `IsChanged`,
  `IsBenchmarked`, `NeedsBenchmark` and `IsKeyLengthFixed`, and new resources `Sync.E2e.Tab.Passphrase`,
  `Sync.E2e.Tab.Advanced`, `Sync.E2e.Advanced.Text` and `Sync.Argon2id.NeedsBenchmark`.
- **Change passphrase can be opened from an app's own page.** New component `E2eChangePassphraseDialog`
  (`<E2eChangePassphraseDialog Profile="@id" OnClose="..." />`, rendered while it should be open) shows the same change
  passphrase dialog as Settings for one profile: the passphrases with the Advanced tab, the estimate, the progress and
  the sign out everywhere. The dialog itself is now its own component, `E2eProfileDialog`, which the Settings section
  uses; with change passphrase on its own pages, an app can remove the section.
- **A passphrase can be given its own Argon2id costs.** `ISyncProtectedProfileService.CreateAsync`, `SetUpAsync` and
  `ChangePassphraseAsync`, and `ISyncKeyRotationService.RotateAsync`, take an optional `Argon2idParameters`. New
  `ISyncProtectedProfileService.GetArgon2ParametersAsync` says which costs apply to a profile, and
  `IE2eProtectedProfileManager.GetArgon2ParametersAsync` reads the ones in its keys document. **Breaking:** the new
  parameter comes before `progress`, so calls that pass `progress` by position name it or pass the costs (or `null`)
  first, and `SyncProtectedProfileService` takes the app's `Argon2idOptions`.

### Changed
- **End to end encryption is a Settings section of its own.** `E2eProfilesSection` was drawn inside the Sync section;
  it is now registered separately, between Sync and the Argon2id benchmark, and shown as its own panel, so an app
  whose own pages set and change the passphrases can remove it: `services.RemoveSettingsSection<E2eProfilesSection>()`.
- **Changing a passphrase keeps the profile's Argon2id costs.** A passphrase change reset the costs to the app's
  configured ones. It now keeps the ones in the profile's keys document unless others are chosen (on the Advanced tab,
  or by passing them).
- **A keys document's Argon2id settings have no upper limit.** Reading a keys document refused more than 4 GiB of
  memory, 100 iterations or 16 lanes, which put a ceiling on what a user may choose, and would have left a profile made
  with higher costs unable to unlock. Only what Argon2id and the cipher cannot use is refused now: no iterations or
  lanes, less than 8 KiB of memory for each lane, and a key length other than the cipher's. **Breaking:**
  `E2eKeysDocumentService.MaxMemoryKib`, `MaxIterations` and `MaxParallelism` are removed.
- **The passphrase dialogs' view model.** **Breaking:** `E2eProfilesViewModel` takes an `E2ePassphraseFormViewModel`,
  and its `ConfirmPassphrase`, `CurrentPassphrase` and `AcknowledgedNoRecovery` are now on its `PassphraseForm`, where
  a new passphrase is entered (`Passphrase` is only the one entered to unlock). `OpenSetPassphrase`, `OpenCreate` and
  `OpenChangePassphrase` are `OpenSetPassphraseAsync`, `OpenCreateAsync` and `OpenChangePassphraseAsync` (the commands
  keep their names): they read the Argon2id settings that apply before the dialog opens. `Sync.Argon2id.Text` is
  reworded. In the template, `INotebookService.CreateAsync` takes the Argon2id settings, and `NotebooksViewModel` uses
  the framework's passphrase form in place of its own passphrase fields and validation.

- **A profile's first passphrase is set where its pages are.** The panel shown in place of a profile with no
  passphrase yet (`E2eUnlockPanel`) sent the user to Settings. Its Set passphrase button now opens the dialog there,
  with the same two tabs, and the profile's pages show once it is set. `Sync.E2e.Panel.NeedsPassphrase` no longer says
  "Set one in Settings." **Breaking:** `E2eUnlockPanelViewModel` takes an `E2ePassphraseFormViewModel` in place of the
  `INavigationService`, and `OpenSettings` is replaced by `OpenSetPassphraseAsync`, `SetPassphraseAsync` and
  `CloseSetPassphrase`.

### Fixed
- **Buttons and spinners are repainted before a key is derived.** Argon2id runs on the page's only thread, and the
  running state rendered just before it (the Benchmark button disabled with its spinner, the "Deriving key" step) was
  not painted until it finished. `BouncyCastleArgon2idKeyDerivationService` now gives the browser a frame to paint
  first.

## 0.8.4

### Changed
- **The Argon2id benchmark has no upper limit on memory.** Any memory of at least 8 KiB per lane can be benchmarked;
  if the device cannot provide it, the benchmark says it failed. `Sync.Argon2id.InvalidMemory` now takes only the
  minimum (`{0}`). **Breaking:** `Argon2idSettingsViewModel.MaxMemoryMib` is removed.
- **The Argon2id panel is named for what it is: a benchmark.** It shows the parameters the app derives passphrase keys
  with and times them, or other values, on this device; it does not change them. **Breaking:** `Argon2idPanel` is
  renamed `Argon2idBenchmarkPanel`, and `Argon2idSettingsViewModel` is renamed `Argon2idBenchmarkViewModel`, with the
  same members, and the panel is titled "Argon2id Benchmarking" (`Sync.Argon2id.Title`). Apps that register
  `AddBlazwizSyncModule` need no change.
- **The Argon2id benchmark warns about the browser's memory limit.** A note under its description (new resource
  `Sync.Argon2id.MemoryLimit`) says the app can use at most 2 GiB in the browser, so large memory values can run out
  and stop the app until the page is reloaded.
- **The Argon2id benchmark in Settings runs with settings you choose.** Memory, iterations, parallelism and key length
  are now fields, starting at the settings new passphrases use. Memory is in MiB: pick a common size (16 to
  512 MiB) or type any value. The benchmark times one key with the values entered; they never change the settings new
  passphrases use. Values Argon2id cannot use (e.g. less than 8 KiB of memory per lane) are explained under the fields
  and the benchmark stays disabled until they are fixed. New `Argon2idSettingsViewModel` members: `MemoryMib`,
  `Iterations`, `Parallelism`, `KeyLength`, `CommonMemoryMib`, `Invalid`, `CanBenchmark`, `MaxMemoryMib` and
  `MinKeyLength`; new resources `Sync.Argon2id.MibUnit`, `BytesUnit`, `InvalidMemory`, `InvalidIterations`,
  `InvalidParallelism` and `InvalidKeyLength`, and `Sync.Argon2id.Text` reworded. **Breaking:**
  `Argon2idSettingsViewModel.MemoryText` and the `Sync.Argon2id.MemoryKib` and `Sync.Argon2id.Bytes` resources are
  removed; apps that override those resources remove the overrides.

## 0.8.3

### Fixed
- **Profiles syncing at the same time no longer fight over the status pill.** Each pass's progress was kept in one
  shared count, so two profiles syncing at once (e.g. a new protected profile's first sync while another syncs) made the
  pill jump between their counts. Each running pass now has its own entry in the new `SyncStatus.Passes`
  (`SyncPassStatus`: profile, done, total), and while more than one runs the pill shows a pill for each, with its own
  progress. The status pill now names the profile being synced, e.g. "Syncing Work 12 of 201" (new
  `Sync.Status.SyncingProfileChanges` and `Sync.Status.SyncingProfile` resources). A profile with no name to show (e.g.
  a locked one whose label is not known on this device) shows only the progress, never its id.
  **Breaking:** `SyncStatus.ChangesDone` and `ChangesTotal` are removed (read `Passes`), `ISyncService.TrackAsync`
  takes the profile the work is on, `SyncPillViewModel` takes an `ISyncProfileDisplayNames`, and
  `ISyncStatusPresenter` has a new `PresentPass`. Apps that register the sync module through `AddBlazwizSyncModule`
  need no change.

## 0.8.2

### Changed
- **The status pill says how far a sync has got.** While a pass works through its changes it shows "Syncing 120 of
  2000" instead of only "Syncing", so a large sync (e.g. after importing many files) no longer looks stuck.
  `SyncStatus.ChangesDone` and `ChangesTotal` carry the count; the text is the new `Sync.Status.SyncingChanges`
  resource. The status updates at most every 250ms during a pass, so a long pass does not redraw the UI for each file.
- **Deleting a protected profile shows in the status pill.** `ISyncProtectedProfileService.DeleteAsync` runs through the
  new `ISyncService.TrackAsync`, so the pill says "Syncing 120 of 2000" while the profile's files are deleted from S3,
  and stays syncing until both the deletion and any pass running at the same time have ended. The profile is listed
  with the new `SyncProtectedProfile.IsDeleting` from the moment deleting starts until it is gone, and reloading the
  profiles meanwhile leaves it to the deletion. **Breaking:** `SyncProtectedProfileService` takes an `ISyncService`;
  apps that register it through `AddBlazwizSyncModule` need no change.

### Fixed
- **Profile labels are no longer lost when the profiles are loaded again.** Signed in with a password, labels are only
  kept in memory, and `ISyncProtectedProfileService.LoadAsync` cleared them, so locked profiles went back to showing
  their ids (e.g. after opening a page that reloads the list). Labels of profiles still there are now kept, with any
  stored ones laid over them.

## 0.8.1

### Added
- **A name to remember the user by.** With `AppSettings.EnableRememberMeName` on, ticking "Remember me" on the sign-in
  page shows an optional name box. The name is kept with the remembered user (`SavedUser.DisplayName`) and shown in the
  remembered users list instead of the email address (which stays as the tooltip). Off by default; the template turns
  it on in `wwwroot/appsettings.json`. New text: `Login.RememberMe.Name`, `Login.RememberMe.Name.Placeholder` and
  `Login.RememberMe.Name.Help`.
- **`IE2eDeviceUnlockService`** (UI.Sync), to unlock a protected profile with biometrics from anywhere in the app, not
  only the unlock panel: `CanUnlockAsync`, `UnlockAsync` (syncs first when online, asks for the passkey if needed, and
  returns false when the passphrase is needed) and `ForgetAsync`. Registered by the sync module.
- **The template's Notebooks page opens notebooks with biometrics.** For a user remembered with a passkey, a locked
  notebook with biometrics turned on shows a grey fingerprint, and Open unlocks it with the passkey before showing its
  pages. An eraser button forgets biometric unlock for that notebook on this device, after asking. The template now
  allows device unlock for every notebook (`UseProfileDefaults` in `Program.cs`, and `UseProfileSettings` in
  `NotesSyncProfile`); before, it set neither, so the unlock panel never offered biometrics.

## 0.8.0

### Added
- **Changing a passphrase rotates the profile's keys** (security review SR3). Before, the same master and file keys
  were wrapped again, so anyone who had the old passphrase and a copy of the keys document could still read everything,
  including files written later. Now:
  - `IE2eProtectedProfileManager.ChangePassphraseAsync` makes a new master key and a new active file key
    (`IE2eKeysDocumentService.RotateAsync`). Earlier file keys are kept, retired, so old files still decrypt. The keys
    document is marked `rotation_in_progress` until `CompleteRotationAsync`.
  - `ISyncKeyRotationService` (Sync) runs the whole change: it syncs the profile and refuses when offline or when the
    sync fails, estimates the time from one timed upload, encrypts every file again and uploads them.
  - In Settings, the user sees the estimate and a warning, then a progress bar, and is then signed out on every device.
    There is no skip. If the sign out fails it is offered again until it works.
  - A change cut short must be finished before the profile is used: `E2eUnlockPanel` shows Finish passphrase change in
    place of the profile's pages, on any device.
  - A device that unlocks with the passkey is asked for the new passphrase once the keys have changed elsewhere
    (`E2eErrorCodes.KeysChanged`).
  - A device that reads a file under a key it does not know checks its session (`ISyncKeyMissingHandler`).
- **Sync pass progress.** `ISyncService.SyncAsync` and `ISyncEngine.SyncAsync` take an optional
  `IProgress<SyncPassProgress>`, told how many of a pass's planned changes are done.
- **`ISyncScratchStore`**, for short-lived objects under `scratch/` in the user's prefix, which sync never sees. A sync
  profile whose remote pattern could match `scratch/` is refused at startup, as one matching `keys/` is.
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
  - `deploy-policy.json` gains an `AppSyncEvents` statement.
- **The template's new `--events` option** (on by default) adds user events. Apps created with `--events false` are
  told at creation what that leaves out, and deploy with `-Events disabled`.
- **`deploy.ps1 -Aot`** publishes the portal compiled ahead of time to WebAssembly, with SIMD: the optimal build, several
  times faster at run time (Argon2id key derivation most of all). It needs `dotnet workload install wasm-tools`. The
  app's README ("The optimal build") explains it, and how to turn it on in the portal's project file instead.

### Removed
- **`cloudformation/test-auth.ps1`**, from the base stack and from apps. It was run only by hand, with the deployer's
  credentials. The repository checks its deployed template app with Reqnroll deploy tests instead
  (`tests/Devoctomy.Blazwiz.DeployTests`, run by a Deploy tests workflow; `docs/deploying.md`), which sign in as a test
  user with SRP and need no AWS credentials of their own.
- **The app client's `ALLOW_ADMIN_USER_PASSWORD_AUTH` flow,** which only `test-auth.ps1` used: the portal's client
  allows SRP and token refresh only. `deploy-policy.json` loses `cognito-idp:AdminInitiateAuth`. Redeploy with
  `deploy.ps1`.
- **`IE2eKeysDocumentService.RewrapMasterKeyAsync`.** A passphrase is no longer changed without rotating the keys; use
  `RotateAsync`.

### Changed
- **Passphrase changes, for code that calls the services:**
  - `IE2eProtectedProfileManager.ChangePassphraseAsync` and `ISyncProtectedProfileService.ChangePassphraseAsync` take
    an `IProgress<E2eProgress>?` before the cancellation token, rotate the keys, and leave the profile's files to be
    encrypted again. Call `ISyncKeyRotationService.PrepareAsync` and `RotateAsync` for the whole change.
  - `IE2eProtectedProfileManager` gains `IsRotationInProgress` and `CompleteRotationAsync`; implementations add them.
  - `E2eStep` gains `ReencryptFiles` and `UploadFiles`, `E2eProfileState` gains `RotationInProgress`, and
    `E2eDialogKind` gains `FinishRotation`.
  - `ISyncService.SyncAsync` and `ISyncEngine.SyncAsync` take an `IProgress<SyncPassProgress>?` before the
    cancellation token. Callers passing the token by position pass it by name (`ct:`), and implementations add the
    parameter.
  - The constructors of `E2eProfilesViewModel`, `E2eUnlockPanelViewModel` and `EncryptingSyncFileStore` take new
    arguments.
- The text `Sync.E2e.Dialog.ChangePassphrase.Text` now says what a passphrase change does. An app that overrides it
  should reword its own. New text keys: `Sync.E2e.Rotation.*`, `Sync.E2e.FinishRotation`,
  `Sync.E2e.State.RotationInProgress`, `Sync.E2e.Step.ReencryptFiles`, `Sync.E2e.Step.UploadFiles`,
  `Error.E2e.KeysChanged`, `Error.Sync.Rotation.Offline` and `Error.Sync.Rotation.SyncFailed`.
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
    users' secrets, and `deploy-policy.json` loses its Secrets Manager statements. Redeploy with `deploy.ps1`.
  - Keys documents already in Secrets Manager are not moved: set the profiles up again, or copy each secret once into
    `<identity id>/keys/` with the AWS CLI. The stack deletes nothing in Secrets Manager, so they stay restorable until
    you delete them.
- **Generated apps build with the .NET 10 SDK only.** The template's `global.json` now rolls forward within .NET 10
  (`latestFeature`), not to a newer major SDK (`latestMajor`). With a .NET 11 SDK also installed, the UI tests' build
  of the portal ran on .NET 11 inside a .NET 10 `dotnet test` and failed (`MSB4216`, `ComputeWasmBuildAssets`).

### Fixed
- A long list shown with Blazor's `Virtualize` no longer runs to its end (or back to its start) on one turn of the
  mouse wheel: the shell turns off scroll anchoring on `body` (`overflow-anchor: none`), which Chrome reads for the
  page, instead of only the `html` setting Virtualize makes.

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
