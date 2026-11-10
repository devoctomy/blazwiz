# Devoctomy.Blazwiz.Events

User events for blazwiz apps. Each signed-in user has a channel that all their open instances of the app (tabs and
devices) share, so one can tell the others something changed. The first event is "signed out everywhere".

Events only speed things up. An instance that is closed, offline or in the background when an event is sent never
receives it: mobile browsers freeze a backgrounded app and drop its connection. Every feature built on events
therefore has a fallback that works without them. For signing out everywhere, the fallback is the sign-in service
itself, which revokes every session, plus the resume check in `Devoctomy.Blazwiz.Auth`. Events carry no secrets: no
tokens, keys or file names.

## What it does

- Connects the channel when the session starts, and disconnects it on sign out or when offline mode is turned on.
  It reconnects with backoff when the connection drops, and straight away when the app resumes with its session still
  good.
- When the user signs out everywhere (`IAuthenticationService.SignOutEverywhereAsync`), it publishes
  `signed-out-everywhere` with the reason (`user-requested` or `keys-rotated`) and this device's name.
- When another instance publishes it, it ends the session here (`IAuthenticationService.EndSessionAsync`). The UI
  then shows which device did it.
- Registering it makes "Sign out everywhere" show on the Profile page. The page shows the button only when some
  `ISignOutEverywhereHandler` exists.

## Usage

To reach every device, use `Devoctomy.Blazwiz.Events.AppSync`. This package on its own gives the local channel. It
uses the browser's `BroadcastChannel`, which reaches the user's other tabs in the same browser and nothing else. It
needs no AWS, so it suits development with the placeholder sign in. After `AddBlazwizApp`:

```csharp
builder.Services.AddBlazwizEvents();
```

To add your own channel, implement `IUserEventChannel` and register it with `AddBlazwizEvents<TChannel>()`.

## Versions

Every `Devoctomy.Blazwiz.*` package and the `Devoctomy.Blazwiz.Templates` template are released together at one
version. Use the same version of all of them; an app made from the template sets it once, as `BlazwizVersion` in its
`Directory.Packages.props`. Before 1.0 any release may break. See [versioning](https://github.com/devoctomy/blazwiz/blob/main/docs/versioning.md) and
the [changelog](https://github.com/devoctomy/blazwiz/blob/main/CHANGELOG.md).

Part of [blazwiz](https://github.com/devoctomy/blazwiz): a framework for Blazor WebAssembly PWAs that sign in with AWS Cognito, sync files to S3 with end
to end encryption, and work offline.
