# Sign in and sessions

This page covers how users sign in, how long they stay signed in, offline mode, passkeys, passwords, and signing out
on every device. The packages involved are `Devoctomy.Blazwiz.Auth` (the contract and sessions) and
`Devoctomy.Blazwiz.Auth.Cognito` (signing in with AWS Cognito).

## Setting it up

```csharp
builder.Services.AddBlazwizUi(builder.Configuration);
builder.Services.AddBlazwizCognito(builder.Configuration, allowPlaceholderSignIn: builder.HostEnvironment.IsDevelopment());
```

`AddBlazwizCognito` reads the `AwsCognito` section of `wwwroot/appsettings.json`:

```json
"AwsCognito": {
  "Region": "eu-west-2",
  "UserPoolId": "...",
  "UserPoolClientId": "...",
  "IdentityPoolId": "..."
}
```

You don't fill these in by hand for a deployed app: the deploy script writes them to `appsettings.Production.json`
(see [Deploying](deploying.md)).

### The placeholder sign in

While `Region`, `UserPoolId` and `UserPoolClientId` are empty, there is no user pool to sign in to. What happens then
depends on `allowPlaceholderSignIn`:

- **`true`**: the app uses a placeholder sign in that accepts any username and password. Pass
  `builder.HostEnvironment.IsDevelopment()`, so this only happens when you `dotnet run` locally (and in the UI tests).
- **`false`**: the app refuses to start, with an error naming the missing settings. A deployed app with missing
  settings never lets anyone in.

`IAuthenticationService.IsPlaceholder` tells you which you have.

> If you add the sync module without calling `AddBlazwizCognito` first, it adds Cognito itself with
> `allowPlaceholderSignIn: false`. Always call `AddBlazwizCognito` yourself, before the sync module, so you choose.

### How Cognito sign in works

Signing in uses SRP (Secure Remote Password) through AWS's `Amazon.Extensions.CognitoAuthentication` library: the
password is proved to Cognito without ever being sent. Once signed in, the app gets temporary AWS credentials for the
user from the Cognito identity pool (`IAwsCredentialsProvider`). The sync module uses them to reach the user's folder
in the data bucket.

## The authentication service

Everything about the current user goes through `IAuthenticationService` (`Devoctomy.Blazwiz.Auth.Services.Interfaces`).
Inject it into a view model:

```csharp
var user = await authService.GetCurrentUserAsync();
Greeting = greetingService.GetGreeting(user?.DisplayName);
```

`GetCurrentUserAsync()` returns a `UserSession`, or null when nobody is signed in or the session has expired.
`UserSession` has `Username`, `DisplayName`, `Email`, `SignedInAt`, `ExpiresAt`, `IsOffline`, and the Cognito
`IdToken` and `AccessToken`.

The sign-in page, the Profile page and the shell call the rest of the service for you: `LoginAsync`,
`CompleteNewPasswordAsync` (for a user invited with a temporary password), `LogoutAsync`, and the offline and
sign-out-everywhere methods below. You only call them yourself if you replace those pages.

## How long a session lasts

| What | How long |
|---|---|
| A Cognito session (its tokens) | 60 minutes, renewed in the last 5 minutes while the tab is open |
| The refresh token that renews it | 12 hours, and only while the tab stays open (it is kept in memory) |
| An offline session | 15 minutes, never renewed (`OfflineModeService.SessionLength`) |
| A placeholder session | 8 hours, never renewed |

The countdown under the user's name in the sidebar (`SessionTimer`) shows how long is left.

When a session ends without the user signing out (it ran out, it could not be renewed, or another device signed
them out), the service signs out and raises `SessionEnded` with a `SessionEndedEventArgs`: the reason
(`SessionEndedReason`: `Expired`, `Revoked`, `SignedOutEverywhere` or `KeysRotated`) and, when another device did it,
that device's name. The shell then returns to the sign-in page, which says why. Show a device name only as plain
text: it comes from another device.

## Doing something when a session starts or ends

Register a session lifecycle handler. It is called when the user signs in, when the shell loads, when the session
starts, and when the user signs out. See
[Session lifecycle handlers](shell-and-extension-points.md#session-lifecycle-handlers).

## Offline mode

Offline mode is a switch, held in memory only, that stops the app calling the sign-in service or AWS. A reload always
leaves it.

There are two ways in:

- **The user chooses it**, with the switch on the Settings page (`OfflineSettingsSection`). The online session is
  swapped for a 15-minute offline one (`IAuthenticationService.GoOfflineAsync()`).
- **Signing in can't reach Cognito.** A user remembered on this device with a passkey can sign in offline instead
  (`SignInOfflineAsync`). Their passkey proves who they are, by decrypting the password remembered for them.

While offline, `OfflineBanner` shows above every page, with a button to sign in online again. Sync makes no network
calls.

To check or follow offline mode in your own code, inject `IOfflineModeService` (`Devoctomy.Blazwiz.Core.Offline.Interfaces`):

```csharp
if (offlineModeService.IsOffline)
{
    // Don't try to reach the network.
}

offlineModeService.Changed += OnOfflineChanged;   // raised with true or false; unsubscribe in Dispose
```

`Reason` says why (`OfflineEntryReason.Chosen` or `OfflineEntryReason.SignInUnreachable`).

## Passkeys and "Remember me"

When the user ticks "Remember me" on the sign-in page, their email is remembered on this device. With
`AppSettings.EnablePortalLoginAuthn` set to `true`, they are then offered a **passkey** (WebAuthn: the device's
fingerprint, face or PIN). Their password is kept on the device encrypted with a key that only their passkey can
produce, so next time they sign in with one touch, and can sign in offline.

`AppSettings.EnableRememberMeName` lets the user give a name to be remembered by, shown on the sign-in page instead of
their email address.

The keys a passkey produces are salted with the app's id (`AppIdentity.Id`). Packages that need such a key ask for one
by name with `AddBlazwizPrfKeyDerivation(prf => prf.AddKey("my-key"))`, and read it from `IDerivedKeyStore`. The sync
module uses them to let a profile be unlocked with the passkey (see
[Sync and end to end encryption](sync-and-encryption.md#unlocking-with-biometrics)).

[docs/diagrams/prf-remember-me.md](../diagrams/prf-remember-me.md) shows the flows in detail.

## Passwords

- **Changing a password**: the Profile page has a form for it (`ChangePasswordForm`).
- **Forgotten passwords**: the sign-in page links to `/forgot-password`, where Cognito emails the user a code.
  Replace or turn off that page like any framework page; turning it off also hides the link:

  ```csharp
  builder.Services.DisablePage(BlazwizPages.ForgotPassword);
  ```

Both go through `IPasswordService` (`ChangePasswordAsync`, `RequestPasswordResetAsync`, `ResetPasswordAsync`), which
uses Cognito when it is configured and a placeholder otherwise.

## Signing out everywhere

`IAuthenticationService.SignOutEverywhereAsync(reason)` signs the user out on every device:

1. Cognito revokes every session they have (`GlobalSignOut`).
2. Every registered `ISignOutEverywhereHandler` runs, for example to tell the user's other open devices at once.
3. The user is signed out on this device.

It needs Cognito. When Cognito can't be reached, or in offline mode, it returns `false` and nothing changes: pretending
to sign out everywhere would be worse than an error.

The Profile page offers "Sign out everywhere" when at least one `ISignOutEverywhereHandler` is registered, and the
Settings page then shows a This device section, where the user names the device (`IDeviceNameService`) so other
devices can say which one signed them out.

Add a handler of your own with:

```csharp
builder.Services.AddSignOutEverywhereHandler<MySignOutEverywhereHandler>();
```

### How other devices find out

- **At once, with user events.** `AddBlazwizAppSyncEvents` connects each signed-in device to a channel for the user,
  over AWS AppSync Events. When one device signs out everywhere, the others end their session straight away and say
  which device did it.
- **When they next wake up.** Whenever the app becomes visible again or comes back online, it checks its session with
  Cognito (`CheckSessionAsync`). A revoked session ends with `SessionEndedReason.Revoked`. This works with or without
  user events, and catches devices that were closed, asleep or offline when the event was sent.

### User events

```csharp
builder.Services.AddBlazwizAppSyncEvents(builder.Configuration, allowLocalChannel: builder.HostEnvironment.IsDevelopment());
```

Call it after `AddBlazwizCognito`. It reads the `Events` section (`HttpEndpoint`, `RealtimeEndpoint`), which the deploy
script writes. Without it:

- in development (`allowLocalChannel: true`), the browser's other tabs stand in, through the browser's
  `BroadcastChannel`;
- anywhere else, the app runs without events and logs a warning.

AppSync Events is charged per use (connection minutes and event operations), with no fixed monthly charge. Apps
created with `--events false` leave it out; the generated README says how to add it later. Events carry no secrets:
they only say that something happened.

## Changing stored data at sign in

If your app changes the format of data it keeps in the browser, a data migration can convert it when the user signs
in. Implement `IDataMigration` (its `Area`, `Version`, `Scope` and `MigrateAsync`) and register it:

```csharp
builder.Services.AddDataMigration<MyDataMigration>();
```

Migrations run in order before modules start. A migration must do nothing when there is nothing to migrate, and must
be safe to run again if it was stopped part way.
