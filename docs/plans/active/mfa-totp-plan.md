---
title: MFA with an authenticator app (TOTP), with remembered devices
type: active
status: in progress
added: 2026-11-03
updated: 2026-11-23
urgency: medium
before-1.0: recommended
reason: a phished or reused password alone yields the tokens, AWS credentials, wrapped keys and ciphertext
areas: [Auth, Auth.Cognito, UI, AWS stack]
effort: large
functional-change: yes
blocked-by: none
decision: remembered devices (2026-11-11); the five choices below (2026-11-23)
related:
  - ../../reviews/2026-11-08-security-review/security-review.md (SR6)
  - ../../diagrams/prf-remember-me.md (where the remembered password, and now the device credential, are kept)
---

# MFA with an authenticator app (TOTP), with remembered devices

Raised by the upgradeability review of 2026-11-03 (item I4, Phase 3, since removed) and again as SR6 of the security
review of 2026-11-08. It was in that review's backlog (`backlog/mfa-totp.md`) until it became this plan.

## The gap

Signing in needs only the password. A phished or reused password gives the ID token, so the AWS credentials, so the
wrapped keys and the ciphertext, and the power to delete them. Cognito can ask for a second factor, a code from an
authenticator app (TOTP), but the user pool has MFA off and the sign-in flow reports Cognito's MFA challenges as
`Auth.ChallengeUnsupported`.

## Decisions

1. **Who decides whether MFA is on (2026-11-23):** the app developer, through a stack parameter: off, optional (each
   user turns it on in their Profile) or required (every user sets it up at their first sign in after it is turned
   on). The default is optional, as SR6 recommends: nobody is made to do anything, and anyone can turn it on.
2. **Remembering devices (2026-11-11, 2026-11-23):** Cognito remembered devices, always remembered once confirmed
   (`DeviceOnlyRememberedOnUserPrompt: false`, no "Remember this device" box). Without it, passkey quick sign in would
   ask for a code at every reload, since the refresh token is held in memory and every reload signs in again with the
   remembered password.
3. **Revoking a device (2026-11-23):** Settings lists the user's remembered devices, each with a Forget button.
4. **Sign out everywhere (2026-11-23):** also forgets every remembered device, so each one asks for a code again.
5. **The QR code (2026-11-23):** drawn in C# with the QRCoder package (MIT), as an SVG. No JavaScript.

**Out of scope: Cognito passkeys.** Cognito's own WebAuthn sign in (the choice-based `USER_AUTH` flow) is not wanted.
The app's passkey is used only on the device, for its PRF output, and never reaches Cognito. Remembered devices work
with the SRP flow; moving to `USER_AUTH` would need this design looked at again.

**Offline is not affected.** Offline sign in never calls Cognito, so MFA never comes up there.

## Design

### Where the device credential is kept

Remembering a device gives the client three values: the device key, the device group key and a device password the
client makes up. With the password they skip MFA on that device, so they are a credential like the password, and are
kept with it: in the remembered user's `WebAuthnStoredCredential`, encrypted under the passkey's PRF key by the same
`IPasswordCryptoService`. Passkey quick sign in stays one touch. Forgetting the remembered user forgets the device
credential with it.

So **a device is only confirmed with Cognito when there is somewhere safe to keep its credential**: a remembered user
with a passkey on this device. A user who types their password without a passkey answers the code at every sign in,
and leaves no device behind in Cognito that nothing could ever use. The authentication service keeps the new device's
details from the last sign in in memory; the login page asks it to remember the device only when it will store the
result.

### Contract (Auth)

- `LoginResult` gains `MfaCodeRequired` (answer with a code), `MfaSetupSecret` (the secret to show, when the pool
  requires MFA and the user has none yet; the UI builds the `otpauth://` URI and QR code from it, with the app's
  name) and `RememberedDeviceRejected` (the stored device was forgotten elsewhere: drop it).
- `IAuthenticationService`:
  - `LoginAsync(username, password, rememberedDevice)`: `rememberedDevice` is the opaque credential stored for this
    device, or null.
  - `CompleteMfaAsync(code)`: answers the code challenge.
  - `CompleteMfaSetupAsync(code)`: confirms the authenticator set up during sign in, which finishes signing in.
  - `RememberDeviceAsync(deviceName)`: confirms the device the last sign in came from, and returns its credential as
    an opaque string to store, or null when there is nothing to remember (no new device, or the placeholder).
  - `SignOutEverywhereAsync` also forgets every remembered device.
- `IMfaService` (new): `GetStatusAsync` (on or off), `BeginSetupAsync` (the secret and URI), `CompleteSetupAsync(code)`
  (verify, then make it the preferred MFA), `DisableAsync`. Failures throw a `BlazwizException` with an
  `AuthErrorCodes` code, like `IPasswordService`.
- `IRememberedDeviceService` (new): `ListAsync` (key, name, last signed in, whether it is this device) and
  `ForgetAsync(deviceKey)`.
- Placeholders for local development: `LocalMfaService` keeps whether MFA is on in local storage, and when it is the
  placeholder sign in asks for a code and accepts any six digits. `LocalRememberedDeviceService` lists nothing.

### Cognito (Auth.Cognito)

- `ICognitoUserPoolClient` and `CognitoUserPoolClient`, translating only (as now), all through
  `Amazon.Extensions.CognitoAuthentication` 3.1.5 or the AWS SDK it wraps:
  - Sign in sends the device (`CognitoUser.Device`, with `DeviceGroupKey` and `DevicePass` on the request) when there
    is one. The library answers `DEVICE_SRP_AUTH` and `DEVICE_PASSWORD_VERIFIER` itself. It throws if those are
    missing and dereferences `Device` without a null check, so all three are always set together.
  - `RespondToMfaAuthAsync` for `SOFTWARE_TOKEN_MFA`.
  - `MFA_SETUP` at sign in: `AssociateSoftwareToken` and `VerifySoftwareToken` with the challenge's session, then
    `RespondToAuthChallenge`.
  - Signed in: `AssociateSoftwareTokenAsync`, `VerifySoftwareTokenAsync`, `UpdateSoftwareMfaSettingsAsync` (or
    `SetUserMFAPreference`), `GetUser` for the status.
  - Devices: `GenerateDeviceVerifier`, `ConfirmDeviceAsync`, `ListDevicesAsync`, `ForgetDeviceAsync`.
  - `CognitoSignInResult` carries `NewDeviceMetadata` (device key and group key).
- `CognitoAuthenticationService` turns `SOFTWARE_TOKEN_MFA` and `MFA_SETUP` into the new `LoginResult` states, like
  `NEW_PASSWORD_REQUIRED`; a wrong code lets the user try again, an expired session sends them back to sign in. A
  stored device credential the pool no longer knows (forgotten elsewhere) is dropped, and the sign in is retried
  without it.
- `CognitoMfaService` and `CognitoRememberedDeviceService`, needing the sign-in service (offline mode makes no calls),
  as `CognitoPasswordService` does.
- `ICognitoCurrentDevice` (scoped, in memory) holds the key of the device this session signed in from:
  `CognitoAuthenticationService` sets it, and `CognitoRememberedDeviceService` reads it to mark "this device".

### UI

- **Setting up an authenticator** (Profile, and the sign-in set-up step) works on the phone the app runs on, where
  nothing can scan a QR code: the secret is shown as text with a Copy button (`IClipboardService`), and an "Open in
  authenticator app" link (the `otpauth://` URI) hands it straight to the authenticator app. The QR code is for
  setting up from another screen.
- **Sign in:** a code step after the password, like the new password step, for typed and passkey sign in. For
  "required", a set-up step as above, then a first code.
- **Passkey:** setting up a passkey also remembers the device, and stores its credential encrypted with the password.
  A passkey sign in that answered a code (a device not remembered yet) remembers the device then. The device name is
  the one the user gave this device in Settings (`IDeviceNameService`).
- **Profile:** a "Two-step verification" panel: turn on (QR code, secret with Copy, the app link, first code), or
  turn off.
- **Settings:** a "Remembered devices" section listing them, this one marked, each with a Forget button. Forgetting
  this device also drops its stored credential.
- `IQrCodeService` in UI (QRCoder), so view models get an SVG string and the logic stays testable.

### AWS base stack

- Parameter `Mfa`: `off`, `optional` (default) or `required`. `MfaConfiguration` `OFF`, `OPTIONAL` or `ON`, with
  `EnabledMfas: [SOFTWARE_TOKEN_MFA]` unless off.
- `DeviceConfiguration`: `ChallengeRequiredOnNewDevice: true`, `DeviceOnlyRememberedOnUserPrompt: false`.
- `deploy.ps1` gains `-Mfa`, kept from the stack on an upgrade when not given, like `-Events`.

### Trust

With the device credential and the password, an attacker skips MFA. Both sit behind the device's authenticator (the
PRF), the trust passkey sign in already relies on. Written into the security notes (`docs/diagrams/prf-remember-me.md`
and the Auth.Cognito README).

## Work

Coverage is measured with the unit tests after each phase (AGENTS.md, Code quality).

### Phase 1: contract and Cognito

- [x] `LoginResult`, `IAuthenticationService` additions, `IMfaService`, `IRememberedDeviceService`,
      `RememberedDevice`, new `AuthErrorCodes` (and their `UiText` entries)
- [x] `ICognitoUserPoolClient` and `CognitoUserPoolClient`: device sign in, MFA answer, MFA set-up at sign in and
      signed in, devices
- [x] `CognitoAuthenticationService`: the challenges, the device credential, remember device, forget on sign out
      everywhere
- [x] `CognitoMfaService`, `CognitoRememberedDeviceService`, `ICognitoCurrentDevice`, registered in
      `AddBlazwizCognito`
- [x] Placeholders: `LocalAuthenticationService` code step, `LocalMfaService`, `LocalRememberedDeviceService`
- [x] Unit tests, coverage of every changed line (2026-11-23: all covered)

### Phase 2: UI

- [ ] `IQrCodeService` (QRCoder)
- [ ] Sign-in code step and set-up step (`LoginViewModel`, `LoginView`)
- [ ] Device credential with the passkey (`WebAuthnStoredCredential`, `LoginViewModel`)
- [ ] Profile panel (`MfaSettingsViewModel`, component)
- [ ] Settings section (`RememberedDevicesViewModel`, component)
- [ ] `UiText` entries, pseudo-localisation
- [ ] Unit tests, coverage; the count of uncovered markup lines
- [ ] Playwright test against the placeholder

### Phase 3: stack and docs

- [ ] `stack.yaml` (`Mfa`, `MfaConfiguration`, `EnabledMfas`, `DeviceConfiguration`), `deploy.ps1` `-Mfa`
- [ ] `docs/blazwiz` guide, package READMEs (Auth, Auth.Cognito, UI, Aws), `template/README.md` if it lists deploy
      parameters, `docs/diagrams` (sign in, `prf-remember-me.md`)
- [ ] `CHANGELOG.md`
- [ ] SR6 in the security review marked done
- [ ] All AGENTS.md build and test steps, `test-template.ps1 -SkipRepositoryTests`, and `-UpgradeFrom latest` for the
      stack change
- [ ] Maintainer: deploy, and test MFA, a remembered device, passkey sign in and forgetting devices against real
      Cognito
