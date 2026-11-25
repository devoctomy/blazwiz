---
title: Drop the passkey's key after use, and warn that renaming a device forgets it
type: plan
status: not started
added: 2026-11-25
origin: the maintainer, using clypse
areas: [UI.Sync, UI, Encryption, Auth.Cognito]
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

- [ ] Don't keep the key encryption key after it has been used. Remove it from `IDerivedKeyStore` (and zero it):
  - [ ] after a sign-in has derived it, once nothing at sign-in needs it;
  - [ ] after a biometric unlock (`E2eDeviceUnlockService.UnlockAsync`, `E2eUnlockPanelViewModel`), whether it worked
        or not;
  - [ ] on `E2eProtectedProfileManager.Lock` and `LockAll`, in case it is still there.
- [ ] The flows that need the key ask for the passkey first (`IPasskeyKeysService.EnsureKeyAsync`), and remove the key
      afterwards:
  - [ ] turning on device unlock (`E2eProtectedProfileManager.EnableDeviceUnlockAsync`, which now throws without it);
  - [ ] rewrapping after a passphrase change or key rotation (`SaveRotatedLocalKeysAsync`, which turns device unlock
        off when the key isn't in memory). It must not turn device unlock off silently because the key was dropped.
- [ ] Decide whether `E2eKeyNames.LabelKey`, also derived from the passkey at sign-in, stays in memory (labels are read
      while locked) or follows the same rule. Agree it with the maintainer.
- [ ] Unit tests (`Given…_When…_Then…`): the key is gone after a biometric unlock, after a failed one and after a
      lock; a biometric unlock after a lock asks for the passkey again; turning on device unlock and changing the
      passphrase ask for it when it isn't in memory.
- [ ] Update `docs/blazwiz/sync-and-encryption.md` and `docs/diagrams/prf-remember-me.md` where they say the key is
      kept for the session.
- [ ] `CHANGELOG.md` (Security or Changed).

## 2. Renaming the device doesn't rename the remembered device

### The problem

`DeviceSettingsViewModel.SaveAsync` saves the new name on this device only (`IDeviceNameService`). Cognito sets a
remembered device's name once, when it is confirmed (`CognitoUserPoolClient.ConfirmDeviceAsync`), and has no call to
rename it. The remembered devices list keeps the old name until the user forgets the device and remembers it again
through MFA.

### The change

- [ ] When this device is remembered and its name changes, Settings > Device warns before saving: the device will be
      forgotten, and MFA will be asked for at the next sign-in, when it can be remembered again under the new name.
- [ ] On confirm: save the name and forget this device (`IRememberedDeviceService`), so the next sign-in remembers it
      under the new name. On cancel: change nothing.
- [ ] No warning when the device isn't remembered, or when the name hasn't changed.
- [ ] Resources for the warning and its buttons in `UiText.resx`.
- [ ] Unit tests for `DeviceSettingsViewModel`: remembered and renamed warns; confirm saves and forgets; cancel does
      neither; not remembered saves without a warning.
- [ ] Update `docs/blazwiz/sign-in-and-sessions.md` if it describes the device name.
- [ ] `CHANGELOG.md` (Changed).

## Checking

- [ ] Unit tests of the changed projects only while working.
- [ ] Rebuild the packages into clypse (`eng/use-local-blazwiz.ps1` there) and check both changes in the running clypse
      portal; hand it to the maintainer to try.
- [ ] Once they confirm: blazwiz's full suites once, then clypse's.
- [ ] Leave the changes uncommitted for the maintainer.
