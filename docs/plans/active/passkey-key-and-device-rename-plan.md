---
title: Drop the passkey's key after use, and warn that renaming a device forgets it
type: plan
status: in progress (done; waiting for the maintainer to try it in clypse, then the full suites)
added: 2026-11-25
updated: 2026-11-25
origin: the maintainer, using clypse
areas: [UI.Sync, UI, Encryption, Auth, Auth.Cognito]
related: clypse docs/plans/active/user-feedback-fixes-plan.md
---

# Drop the passkey's key after use, and warn that renaming a device forgets it

Two issues the maintainer found using clypse. They are fixed here, not in clypse, as agreed with the maintainer.
clypse's plan (`docs/plans/active/user-feedback-fixes-plan.md` there) tracks the rest, and checks these from a local
build.

## 1. Locking a protected profile leaves the passkey's key in memory

### The problem

After a passkey sign-in, the passkey's derived key encryption key (`E2eKeyNames.KeyEncryptionKey`, `e2e-kek`) stays
in `IDerivedKeyStore` for the whole session. `PasskeyKeysService.EnsureKeyAsync` finds it there and returns at once,
so:

- unlocking a profile with biometrics (`E2eDeviceUnlockService.UnlockAsync`, `E2eUnlockPanelViewModel`) never prompts
  for the passkey;
- after a lock, the next biometric unlock doesn't prompt either. Locking wipes the profile's keys but not the key that
  unwraps them.

Only a page refresh clears it, which is why unlocking prompts after one. The key that unwraps every profile's master
key on this device stays in memory long after it was needed.

### The change

- [x] Don't keep the key encryption key after it has been used. Remove it from `IDerivedKeyStore` (and zero it), through
      the new `IPasskeyKeysService.ForgetKey`:
  - [x] after a sign-in has derived it, once nothing at sign-in needs it: `E2eDeviceKeySessionHandler` (UI.Sync) on
        session start;
  - [x] after a biometric unlock (`E2eDeviceUnlockService.UnlockAsync`, `E2eUnlockPanelViewModel`), whether it worked
        or not;
  - [x] on `E2eProtectedProfileManager.Lock` (and so `LockAll`), in case it is still there.
- [x] The flows that need the key ask for the passkey first (`IPasskeyKeysService.EnsureKeyAsync`), and remove the key
      afterwards:
  - [x] turning on device unlock: the unlock panel already asked for the passkey first; it now forgets the key once
        biometrics are turned on (or the passphrase was wrong);
  - [x] rewrapping after a passphrase change: `E2eProfilesViewModel.StartRotationAsync` asks for the passkey first when
        biometrics are on for the profile here, and forgets the key afterwards. Cancelled, or without a passkey,
        biometrics are turned off here, as before.
- `E2eKeyNames.LabelKey`, also derived from the passkey at sign-in, stays in memory for the session: labels are read
  while profiles are locked. Agreed with the maintainer; only the key encryption key is dropped.
- [x] Unit tests (`Given…_When…_Then…`): `ForgetKey`; the key forgotten after a biometric unlock, a cancelled one and a
      failed one, in the service and the panel; after turning on biometrics and after a wrong passphrase; on session
      start; on lock (manager tests now put the key back with `Device.UsePasskey()` where they unlock with it after a
      lock); the passphrase change asking for the passkey only when biometrics are on, and forgetting it after.
- [x] Update `docs/blazwiz/sync-and-encryption.md` and `docs/diagrams/prf-remember-me.md` (and the UI.Sync and
      Encryption READMEs) where they say the key is kept for the session.
- [x] `CHANGELOG.md` (Security, Added, Changed).

## 2. Renaming the device doesn't rename the remembered device

### The problem

`DeviceSettingsViewModel.SaveAsync` saves the new name on this device only (`IDeviceNameService`). Cognito sets a
remembered device's name once, when it is confirmed (`CognitoUserPoolClient.ConfirmDeviceAsync`), and has no call to
rename it. The remembered devices list keeps the old name until the user forgets the device and remembers it again
through MFA.

### The change

- [x] When this device is remembered and its name changes, Settings > This device warns before saving: the device will
      be forgotten, and a code will be asked for at the next sign-in, when it is remembered again under the new name.
- [x] On confirm: forget this device (`IRememberedDeviceService`), drop its stored credential (the new
      `ISavedDeviceCredentialService`, taken out of `RememberedDevicesViewModel`), then save the name. On cancel:
      change nothing, and the old name is shown again. If forgetting fails, the error is shown and the name isn't saved.
- [x] No warning when the device isn't remembered, when the name hasn't changed, or when the devices can't be listed
      (e.g. offline: the name is saved here only).
- [x] The remembered devices list on the same page drops the device: `IRememberedDeviceService.Forgotten`.
- [x] Resources for the warning and its button in `UiText.resx` (`Settings.Device.Forget.Text`, `.Confirm`; Cancel
      is `Common.Cancel`).
- [x] Unit tests for `DeviceSettingsViewModel`, `SavedDeviceCredentialService`, `RememberedDevicesViewModel` and both
      `IRememberedDeviceService` implementations.
- [x] Update `docs/blazwiz/sign-in-and-sessions.md`.
- [x] `CHANGELOG.md` (Changed, Added).

## Checking

- [x] Unit tests of the changed projects only while working: Auth, Auth.Cognito, Encryption, UI and UI.Sync unit tests
      pass.
- [x] Rebuild the packages into clypse (`eng/use-local-blazwiz.ps1` there) and check both changes in the running clypse
      portal. The Playwright browser has no passkey, so only the paths without one were checked there (passphrase
      unlock and lock, renaming a device that isn't remembered, no console errors).
- [ ] The maintainer tries biometric unlock and renaming a remembered device in their own browser.
- [ ] Once they confirm: blazwiz's full suites once (with coverage of the changed files), then clypse's.
- [ ] Leave the changes uncommitted for the maintainer.
