# Devoctomy.Blazwiz.Auth

Sign in for blazwiz apps: the authentication contract (`IAuthenticationService`), sessions and their lifecycle,
offline sign in, remembered passwords, password changes and resets, two-step verification with an authenticator app
and remembered devices, and migrations of stored data, run at sign in.
It does not talk to an identity provider itself: `Devoctomy.Blazwiz.Auth.Cognito` signs in with AWS Cognito, and
without it a placeholder sign in is used for local development.

## Usage

`AddBlazwizUi` (in `Devoctomy.Blazwiz.UI`) registers it for you. On its own, after `AddBlazwizApp`:

```csharp
builder.Services.AddBlazwizAuth();

// Run code when a session starts or ends.
builder.Services.AddSessionLifecycleHandler<MySessionHandler>();

// Change data the app keeps in the browser to a new format, at sign in.
builder.Services.AddDataMigration<MyDataMigration>();

// Act when the user signs out everywhere, e.g. tell their other devices (Devoctomy.Blazwiz.Events does).
builder.Services.AddSignOutEverywhereHandler<MySignOutEverywhereHandler>();
```

## Two-step verification

A sign in can need a code from the user's authenticator app (TOTP). `LoginAsync` then returns a `LoginResult` with
`MfaCodeRequired`: pass the code to `CompleteMfaAsync`. When the sign-in service requires an app and the user has none,
it returns `MfaSetupSecret` instead: show it, then pass the app's first code to `CompleteMfaSetupAsync`. A wrong code
can be tried again.

A device can be remembered so it is not asked again: after signing in, `RememberDeviceAsync(deviceName)` returns a
credential to keep, as safely as the password (the sign-in page keeps it encrypted with the passkey), and
`LoginAsync(username, password, rememberedDevice)` signs in with it. When the result says `RememberedDeviceRejected`
(it was forgotten elsewhere), drop it.

`IMfaService` turns two-step verification on and off for the signed-in user, and `IRememberedDeviceService` lists and
forgets their remembered devices. Failures throw a `BlazwizException` with one of `AuthErrorCodes`. Without an identity
provider, `LocalMfaService` remembers who turned it on, after which the placeholder sign in asks for a code and accepts
any six digits; `LocalRememberedDeviceService` has no devices.

## Signing out everywhere

`IAuthenticationService.SignOutEverywhereAsync(reason)` signs the user out on every device:

1. The sign-in service forgets every remembered device and revokes every session they have (Cognito's
   `ForgetDevice` and `GlobalSignOut`).
2. Every `ISignOutEverywhereHandler` runs.
3. The user is signed out here.

It needs the sign-in service. When the service cannot be reached, or in offline mode, it returns false and nothing
changes. A sign out here that pretended to be everywhere would be worse than an error.

Other devices find out in one of two ways:

- **When they are told.** `Devoctomy.Blazwiz.Events` publishes an event, and the receiving device calls
  `EndSessionAsync(reason, deviceName)`.
- **When they next resume.** `SessionResumeMonitor` (started with the session) runs `CheckSessionAsync` whenever the
  page becomes visible again or the browser comes back online. A revoked session then ends, with the reason
  `Revoked`.

`SessionEnded` carries `SessionEndedEventArgs`: the reason and, when another device ended the session, that device's
name. Show the name as plain text only, since it comes from another device.

## Versions

Every `Devoctomy.Blazwiz.*` package and the `Devoctomy.Blazwiz.Templates` template are released together at one
version. Use the same version of all of them; an app made from the template sets it once, as `BlazwizVersion` in its
`Directory.Packages.props`. Before 1.0 any release may break. See [versioning](https://github.com/devoctomy/blazwiz/blob/main/docs/versioning.md) and
the [changelog](https://github.com/devoctomy/blazwiz/blob/main/CHANGELOG.md).

Part of [blazwiz](https://github.com/devoctomy/blazwiz): a framework for Blazor WebAssembly PWAs that sign in with AWS Cognito, sync files to S3 with end
to end encryption, and work offline.
