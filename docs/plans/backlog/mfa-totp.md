# MFA with an authenticator app (TOTP)

Added 2026-11-03, from Phase 3 of `docs/reviews/2026-11-03-upgradeability-review.md` (optional there, not done).

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
- **Interplay**: passkey quick sign in and offline sign in use the remembered password; decide whether they also need
  the second factor, or whether the passkey counts as one. Remembered devices (Cognito device tracking) are optional.
- **Text and tests**: all text in `UiText`, pseudo-localisation coverage, unit tests for the new client calls and
  view models, a Playwright test against the placeholder, and an `@aws` scenario if Cognito integration tests exist
  by then.
