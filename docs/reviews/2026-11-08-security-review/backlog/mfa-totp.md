---
title: MFA with an authenticator app (TOTP)
type: backlog
status: ready
added: 2026-11-03
moved-here: 2026-11-09
review: ../security-review.md
finding: SR6
severity: medium
urgency: medium
before-1.0: recommended
reason: a phished or reused password alone yields the tokens, AWS credentials, wrapped keys and ciphertext
areas: [Auth, Auth.Cognito, UI, AWS stack]
effort: large
functional-change: yes
origin: Phase 3 of the upgradeability review of 2026-11-03 (removed)
---

# MFA with an authenticator app (TOTP)

Raised by the upgradeability review of 2026-11-03 (removed) and again as SR6 of `../security-review.md`, which is
why it lives beside that review.

**Was blocked by design (2026-11-08), resolved by remembered devices (2026-11-11, below):** the refresh token is never stored, so a passkey sign in is a fresh Cognito sign in
with the remembered password every time (see `docs/diagrams/prf-remember-me.md`). With MFA on, Cognito would ask for
the second factor at every biometric sign in, which defeats the one-touch sign in. Before MFA can be turned on, decide
how a passkey sign in satisfies it: for example by counting the passkey as the second factor (Cognito remembered
devices, `ConfirmDevice` with `DeviceKey`, so a device that signed in with MFA once is not asked again), or by keeping
the refresh token on the device encrypted under the passkey's PRF key, so a passkey sign in renews the session instead
of signing in again. Either is a design change to the session model and needs its own plan.

## Remembered devices (decided, 2026-11-11)

**Offline is not affected.** Offline sign in (`CognitoAuthenticationService.SignInOfflineAsync`) never calls Cognito:
it starts a local offline session, and the passkey's PRF unlocks things on the device. MFA is a Cognito challenge, so
it can only come up when the app signs in online. The problem is online sign in only: because the refresh token is
held in memory, every reload or new tab is a full SRP sign in with the remembered password, and each would get the
MFA challenge.

**Cognito remembered devices solve that** without changing the session model (the refresh token stays in memory):

1. **User pool** (`stack.yaml`): `DeviceConfiguration` with `ChallengeRequiredOnNewDevice: true`, and
   `DeviceOnlyRememberedOnUserPrompt` set to `true` (the user ticks "remember this device") or `false` (always). Decide
   which.
2. **First sign in on a device**: the user answers the MFA challenge. The authentication result carries
   `NewDeviceMetadata` (`DeviceKey`, `DeviceGroupKey`).
3. **Confirm the device**: the client generates a random device password and its SRP verifier, then calls
   `ConfirmDevice`. If remembering is up to the user, it also calls `UpdateDeviceStatus` with `remembered`.
4. **Later sign ins** send `DEVICE_KEY` and answer the `DEVICE_SRP_AUTH` / `DEVICE_PASSWORD_VERIFIER` challenges,
   and Cognito does not ask for MFA on that device.

**Storage**: the device key, device group key and device password are a credential like the password. Keep them with
the remembered password, encrypted under the passkey's PRF key (`docs/diagrams/prf-remember-me.md`), so passkey quick
sign in stays one biometric touch. Forgetting the passkey on the device forgets the device credential too.

**Revocation**: `ForgetDevice` puts MFA back on a device. Decide whether Settings lists the user's devices
(`ListDevices`) with a "forget" action, and whether sign out everywhere also forgets devices.

**Library**: `Amazon.Extensions.CognitoAuthentication` does all of it (checked against the library's source on
2026-11-11; update the package if 3.1.5 lacks any of it): `CognitoUser.GenerateDeviceVerifier(deviceGroupKey,
devicePass, deviceKey)`, `ConfirmDeviceAsync(accessToken, deviceKey, deviceName, passwordVerifier, salt)` and
`UpdateDeviceStatusAsync`. `StartWithSrpAuthAsync` answers `DEVICE_SRP_AUTH` and `DEVICE_PASSWORD_VERIFIER` itself
when `CognitoUser.Device` is set and the request has `DeviceGroupKey` and `DevicePass`. It throws if those are missing
when the challenge comes, and dereferences `Device` without a null check, so always set all three for a remembered
device.

**Trust**: with the device credential and the password, an attacker skips MFA. Both sit behind the device's
authenticator (the PRF), which is the trust the passkey sign in already relies on, but write it down in the security
notes.

**Out of scope: Cognito passkeys.** Cognito's own WebAuthn sign in (the choice-based `USER_AUTH` flow) is not
wanted. The app's passkey is used only on the device, for its PRF output, and never reaches Cognito; Cognito is the
gate to the encrypted data, not the authenticator. Remembered devices work with the SRP flow; moving to `USER_AUTH`
would need this design looked at again.

Review item I4. Users can turn on a second factor: a code from an authenticator app (TOTP), asked for after the
password. Same pattern as the password flows: a contract in Auth, the Cognito implementation in Auth.Cognito, a
placeholder for local development, and the UI in the UI package.

- **Library**: `Amazon.Extensions.CognitoAuthentication` already supports it (3.1.5): `AssociateSoftwareTokenAsync`
  (the secret for the QR code), `VerifySoftwareTokenAsync`, `UpdateSoftwareMfaSettingsAsync`, and
  `RespondToMfaAuthAsync` for the `SOFTWARE_TOKEN_MFA` challenge at sign in. Never hand-rolled TOTP.
- **Sign in**: `CognitoAuthenticationService` reports `SOFTWARE_TOKEN_MFA` as an unsupported challenge today
  (`Auth.ChallengeUnsupported`); it becomes a second step on the sign-in page, like the new-password challenge.
- **Profile page**: set up (show the QR code and secret, confirm with a first code) and turn off. The QR code needs a
  library or a small component; decide which.
- **User pool** (`stack.yaml`): `MfaConfiguration` `OPTIONAL` (or `ON`, as the app developer's choice) with software
  token MFA enabled. Infrastructure is left to the end of the plan (D4): ship this with the AWS base stack (U15).
- **Interplay**: passkey quick sign in uses the remembered password to sign in to Cognito, so without remembered
  devices it would be asked for the second factor every time. Remembered devices are required, not optional (see
  above). Offline sign in never calls Cognito and is not affected.
- **Text and tests**: all text in `UiText`, pseudo-localisation coverage, unit tests for the new client calls and
  view models, a Playwright test against the placeholder, and an `@aws` scenario if Cognito integration tests exist
  by then.
