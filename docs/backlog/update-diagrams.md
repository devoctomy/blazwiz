# Update the diagrams for the package structure

Added 2026-11-03, after Phases 2 and 3 of `docs/reviews/2026-11-03-upgradeability-review.md`.

The five documents in `docs/diagrams` were written for the old single-app layout. Their flows are still broadly right,
but the names, paths and who-calls-whom are out of date, so they mislead anyone reading them to find code.

## What is out of date

Everywhere:
- Paths name the old projects (`blazwiz.Core/...`, `blazwiz.Portal.Application/...`, `blazwiz.Portal/...`). Code now
  lives in the packages (`src/Devoctomy.Blazwiz.<Package>/...`) or the template app (`template/MyApp.Portal...`).
- Storage keys and names use the old `blazwiz_` prefix; they are named after the app now (`IAppStorageKeys`, e.g.
  `myapp_session`).
- Notes is the framework's example, not part of it: diagrams of the framework should use a neutral profile, with
  notes only where the template's example is the subject.

By document:
- `authentication.md`: `CognitoAuthenticationService` and the credentials provider are in Auth.Cognito, the AWS
  credentials are fetched by `AwsCredentialsSessionHandler` (a session lifecycle handler), not `MainLayoutViewModel`.
  New since: changing and resetting passwords (`IPasswordService`, `CognitoPasswordService`, the Profile page form and
  the `/forgot-password` page).
- `prf-remember-me.md`: `WebAuthnService` and `webauthn-bridge.js` are in Browser (the bridge is loaded by the
  package's JS initializer), the PRF classes in Cryptography, `PasswordCryptoService` in Auth, `PasskeyUnlockService`
  in UI.Sync. New since: a password change encrypts the remembered password again, or forgets it.
- `session-management.md`: the services moved to Auth and Auth.Cognito; `SessionTimerViewModel` reads
  `IAuthenticationService.RefreshMargin` instead of Cognito's constant; the session lifecycle (`ISessionLifecycle`,
  handlers) drives what modules do at sign in and out.
- `sync.md`: sync is started and stopped by `SyncSessionHandler` (UI.Sync), not `MainLayoutViewModel`; the wiring is
  `AddBlazwizSyncModule` (UI.Sync) and `AddBlazwizSync` (Sync); the notes profile is configured in the template app
  (`NotesSyncProfile`, a `SyncProfileDefinition` found by `AddProfilesFromAssemblyContaining`).
- `sync-framework.md`: the engine is the Sync package, not `blazwiz.Core`; the setup should start from
  `AddBlazwizSyncModule(configuration, sync => ...)` in `Program.cs`, and show a neutral profile rather than notes.

## Also worth adding

- An overview of the packages and what depends on what (from the package table in the plan), and of how an app is
  composed in `Program.cs` (`AddBlazwizApp`, `AddBlazwizUi`, `AddBlazwizCognito`, `AddBlazwizSyncModule`).
- The extension points (shell regions, Settings sections, sign-in options, session lifecycle handlers, navigation
  items, page assemblies, page replacement) and where modules plug into them.
- Text: how a string is found (app override, app resources, package resources, by culture), pseudo-localisation, and
  choosing the language at startup.

## Done when

- No diagram names an old project, path or storage key; every class named exists where it says.
- Each diagram's flow is checked against the code (and the UI tests where they cover it).
- Framework diagrams do not depend on the notes example.
