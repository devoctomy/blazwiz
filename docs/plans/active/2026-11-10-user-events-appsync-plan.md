---
title: User events over AppSync Events, starting with "sign out everywhere"
type: active
status: in progress
added: 2026-11-09
urgency: medium
before-1.0: recommended
reason: an instance of the app has no way to tell the user's other instances that something changed; sign-out, key rotation and MFA all need one
areas: [AWS stack, Auth, Auth.Cognito, Browser, UI, new Events and Events.AppSync packages, template]
effort: medium (the channel and the first feature); each later event is small
functional-change: yes
blocked-by: none
related:
  - ../../reviews/2026-11-08-security-review/backlog/passphrase-change-key-rotation.md (other devices must learn the keys changed)
  - ../../reviews/2026-11-08-security-review/backlog/mfa-totp.md
  - ../../reviews/2026-11-08-security-review/security-review.md (SR6, SR20)
  - ../backlog/protected-profile-concurrent-writes.md (the race the channel narrows but does not close)
---

# User events over AppSync Events, starting with "sign out everywhere"

A user's instances of the app (a desktop browser, a phone, a second tab) share data through S3 but never talk to each
other. When one of them does something that affects the others, nothing tells them: a passphrase change leaves other
devices holding keys that no longer match, a sign out on one device leaves the others signed in until their tokens
expire, and a future MFA set-up would not be noticed until the next sign in. This plan adds a per-user event channel
and uses it first for the simplest, most testable feature: signing out everywhere.

## Principle: push is an accelerator, never the source of truth

AppSync Events delivers an event only to the instances connected when it is published. It keeps no history and replays
nothing on reconnect. An instance that is closed, offline, or in the background (mobile browsers freeze a backgrounded
PWA within minutes and drop its socket) never receives it. Every feature built on the channel therefore needs a durable
fallback that works with no channel at all, and the event carries only "something changed, check now", never the
change itself and never a secret. The receiving instance does what it would have done anyway, just sooner.

For sign out, the durable fallback already exists: Cognito's `GlobalSignOut` revokes the user's refresh tokens and,
with `EnableTokenRevocation` on (it is), their access tokens. An instance that misses the event finds out when it next
asks Cognito. To make that happen as soon as it matters, every instance checks its session with Cognito when it
resumes (see "Resume check" below), so a frozen or offline instance signs itself out the moment it wakes, before it
syncs anything. A frozen instance runs no code, so it is no risk while it sleeps. What remains is an instance that
stays open and in use but has silently lost its socket. It finds out at its next token renewal, at most
`AccessTokenValidity` (60 minutes) less the 5-minute renewal margin. That is accepted (see "Decided").

## Why AppSync Events

- **Serverless, pay as you go:** $1.00 per million event operations (connections, publishes, subscriptions,
  deliveries) and $0.08 per million connection minutes. A user with three instances open all day is a few thousand
  connection minutes a month; this rounds to nothing.
- **No schema, no Lambda:** unlike AppSync GraphQL subscriptions it is plain pub/sub on named channels. Unlike API
  Gateway WebSockets it needs no connection table or Lambda. Unlike SNS it delivers to a browser.
- **Authenticates with the user pool token the app already has**, so no new credential flow. A short JavaScript
  handler on the channel namespace (APPSYNC_JS, no Lambda) limits each user to their own channel.
- **Alternative considered, AWS IoT Core:** MQTT over WebSockets with policies keyed on the Cognito identity id, which
  matches how the S3 prefix is isolated, plus retained messages. Rejected for now because an authenticated Cognito
  identity needs an IoT policy attached to it per identity, which means a Lambda at sign in or letting the role attach
  a policy to itself from the browser. Worth revisiting if retained messages turn out to matter.

## Design

### Channel

- One AppSync Events API per stack, with a channel namespace `users`. Each user's channel is `/users/<sub>`, where
  `sub` is the user pool subject from the token. Every instance of a user subscribes to it after sign in and publishes
  to it when it does something the others should hear about.
- **Authorization:** the API's connection, publish and subscribe auth mode is the Cognito user pool. The namespace's
  `onSubscribe` and `onPublish` handlers reject any channel whose path is not `/users/` followed by `ctx.identity.sub`
  (`util.unauthorized()`), so a user can neither listen to nor write to another user's channel. No IAM auth is
  enabled on the API, so the identity pool's AWS credentials cannot reach it.
- **Event shape:** a small JSON object:

  ```json
  { "type": "signed-out-everywhere", "reason": "user-requested", "device": "Android-Edge-624834",
    "at": "<ISO 8601>", "from": "<instance id>" }
  ```

  `reason` is `user-requested` or `keys-rotated` and only chooses the message the receiving instance shows; every
  reason ends the session the same way. `device` is the sending device's name (below), shown in that message. `from`
  is a random id the instance makes when it connects, so it can ignore its own events. Nothing else: no tokens, no
  keys, no file names.
- **Device name:** each browser has a name, kept by `IDeviceNameService` in `Browser`, stored only in its local
  storage (short name `device_name`, prefixed by the storage client) and never sent anywhere but in events on the
  user's own channel. Until the user sets one it is made on first use as `<Platform>-<Browser>-<6 random digits>`,
  e.g. `Android-Edge-624834`. The app name is left out: every event comes from the same app. Platform and browser are
  a best guess, used only to make the name recognisable: `navigator.userAgentData` where it exists (Chromium),
  otherwise the user-agent string (Firefox, Safari; on iOS `CriOS` and `EdgiOS` still name Chrome and Edge), and
  `Unknown` for anything not recognised. Nothing relies on the guess being right. A "This device" section in Settings
  (in `UI`) renames it: trimmed, 1 to 32 characters. Receivers show it as plain text and cut it to 32 characters,
  since it comes from another instance.
- **Connection lifecycle:** connect in `ISessionLifecycleHandler.OnShellLoadedAsync` (the background hook), reconnect
  with backoff when the socket drops or the device comes back online (`Sync`'s `network.js` already watches this),
  re-authenticate when the token is renewed, disconnect on sign out and when the session ends. Never connect while
  offline or with the placeholder sign in.
- **Resume check:** when the page becomes visible again (`visibilitychange`) or the device comes back online, the
  instance checks its session with Cognito before it reconnects to the channel or syncs: `GetUser` with the current
  access token, or a renewal if the token is due. Cognito refuses both after `GlobalSignOut`, and the instance then ends
  its session as an expired one does. This lives in `Auth` / `Auth.Cognito` and works with no channel at all; the
  placeholder sign in skips it. It is what closes the gap for missed events, on every platform.

### Packages

- **`Devoctomy.Blazwiz.Events`** (depends on `Auth`, `Browser`): the contract, `IUserEventChannel`
  (`PublishAsync(UserEvent)`, `event EventHandler<UserEvent> Received`, `ConnectionState`), the `UserEvent` model and
  its known `Type` constants, the session lifecycle handler that connects and disconnects, `UserEventsOptions`
  (configuration section `Events`), and a local implementation on the browser's `BroadcastChannel` API
  (`wwwroot/js/user-events-local.js`) that reaches the same user's other tabs in the same browser with no AWS. It is
  the implementation for development with the placeholder sign in, and what the UI tests drive.
- **`Devoctomy.Blazwiz.Events.AppSync`** (depends on `Events`, `Auth.Cognito`): `AppSyncUserEventChannel` and its ES
  module `wwwroot/js/appsync-events.js`, which speaks the AppSync Events WebSocket protocol directly (subprotocol
  `aws-appsync-event-ws`, the token in the header-encoded subprotocol, `/event/realtime`) and publishes over HTTPS
  `POST /event`. No Amplify dependency: the protocol is small and documented, and vendoring Amplify for it would be
  most of the app's JavaScript. `AddBlazwizAppSyncEvents(configuration)` registers it when `Events:HttpEndpoint` and
  `Events:RealtimeEndpoint` are set. Without them it registers the local channel in development (the way
  `AddBlazwizCognito` falls back to the placeholder) and no channel at all otherwise (see "Optional in the template").
- The package table in the README and `docs/diagrams/overview.md` gain both packages. `Sync` does not depend on
  `Events`. Key rotation reuses the session-ending event, so `UI.Sync` needs no listener of its own.
- **Nothing depends on `Events` except the app.** `Auth` and `UI` never reference it; the two directions meet in
  `Auth`:
  - **Sending:** `Auth` defines `ISignOutEverywhereHandler` (`OnSigningOutEverywhereAsync(SignOutReason, string
    deviceName)`), which `SignOutEverywhereAsync` runs after `GlobalSignOut` succeeds and before the local session
    ends. `Events` registers the handler that publishes the event.
  - **Receiving:** `Events` ends the session through `IAuthenticationService.EndSessionAsync(SessionEndedReason,
    string? deviceName)`. `SessionEnded` becomes `EventHandler<SessionEndedEventArgs>` (`Reason`, `DeviceName`), so
    the UI picks the message without knowing where it came from. This changes the shipped `SessionEnded` signature
    (allowed before 1.0; the analyzer's `*REMOVED*` line and a `CHANGELOG.md` entry record it).
  - **Device name** is `IDeviceNameService` in `Browser`, a fact about the browser rather than about events.
  - **UI** (which already depends on `Auth` and `Browser`) holds the Profile button, the messages and the "This
    device" Settings section.
  - Without `Events`, `SignOutEverywhereAsync` still works: `GlobalSignOut` revokes every device and the resume check
    signs the others out when they next wake. Key rotation relies on this. Only the Profile page button needs
    `Events` (see "Optional in the template").

### Optional in the template

Events add AWS resources that are charged per connection minute and message. The template never forces that expense
on an app: the developer decides. They are on by default, and turning the option off creates nothing.

- **Template option `Events`** (bool, default `true`, computed `IncludeEvents`, like `Sync`). Its description says
  what it adds and what it costs. When it is off, a post-action (manual instructions) warns the developer at creation:
  without events, "Sign out everywhere" is not offered, and after a passphrase change other devices sign out only when
  they are next opened or come back online, not at once.
- **What the option controls:** the `Events` and `Events.AppSync` package references, `AddBlazwizAppSyncEvents` in
  `Program.cs`, the `Events` section of `appsettings.json`, and the stack parameter below. Markers as usual
  (`//#if (IncludeEvents)` in `Program.cs`, project files and the deploy settings), with the `exclude` lists in
  `template.json` updated and `template/Directory.Build.targets` defining the option for the repository build.
- **Stack:** the base stack gains a parameter `Events` (`enabled` / `disabled`, default `disabled`, so apps made
  before events existed get nothing new; apps created with events pass `enabled`) and a condition
  on the API, the channel namespace and their outputs. The app's `template/cloudformation` deploy settings pass
  `disabled` when the app was created without events. `deploy.ps1` writes the `Events` section and the CSP entries only when
  the outputs exist.
- **Missing infrastructure:** an app that includes events but has no endpoints configured outside development (the
  stack was deployed with events disabled, or the settings were not written) registers no channel, logs one warning
  naming the missing settings, and runs as an app without events. It never falls back to the local channel in
  production, which would look like it works while reaching nobody.
- **The button:** the Profile page shows "Sign out everywhere" only when an `ISignOutEverywhereHandler` is registered,
  that is, when a channel is. `SignOutEverywhereAsync` itself stays available for key rotation either way.

### First feature: sign out everywhere

- **Auth:** `IAuthenticationService.SignOutEverywhereAsync(SignOutReason reason)`, with `SignOutReason.UserRequested`
  (the Profile page button) and `SignOutReason.KeysRotated` (the last step of a passphrase change, from
  `docs/reviews/2026-11-08-security-review/backlog/passphrase-change-key-rotation.md`). The Cognito implementation
  calls `GlobalSignOut` with the access token (`CognitoUser.GlobalSignOutAsync` in
  `Amazon.Extensions.CognitoAuthentication`), runs every `ISignOutEverywhereHandler`, then ends the local session as
  `SignOutAsync` does. The placeholder implementation runs the handlers and ends the local session. If `GlobalSignOut`
  fails because the device is offline, the button reports it and does nothing else: a local sign out that pretends
  to be global would be worse than an error.
- **Events:** the `ISignOutEverywhereHandler` publishes `signed-out-everywhere` with the reason and the device name.
  On receiving it, an instance calls `EndSessionAsync` with the reason and the sender's device name, and the session
  ends the way an expired one does (`SessionEnded` fires, the shell returns to the sign-in page, and every
  `ISessionLifecycleHandler` runs its sign-out work: the PRF-derived keys are wiped by `DerivedKeySessionHandler`,
  protected profiles lock).
- **UI:** a "Sign out everywhere" button on the Profile page, under the existing sign out, with a confirmation that
  says what it does and that devices which are closed or offline sign out the next time they are opened or come back
  online. On `SessionEnded` the sign-in page shows the message for the reason: "You were signed out from {device}"
  (user requested), "Your passphrase was changed on {device}. Sign in and unlock with the new one" (keys rotated), or
  "You were signed out" (a refused resume check, which never saw the event and so knows no reason or device). The
  "This device" Settings section renames the device. Text in `UiText`; `PseudoLocalizationTests` covers it.
- **Stack (`stack.yaml`, all under the `Events` condition):** `AWS::AppSync::Api` with `EventConfig` (auth provider
  `AMAZON_COGNITO_USER_POOLS` on the stack's user pool and client, as the connection, default publish and default
  subscribe modes), an
  `AWS::AppSync::ChannelNamespace` `users` with the handler code inline, and outputs `EventsHttpEndpoint` and
  `EventsRealtimeEndpoint`. `deploy.ps1` writes them into `appsettings.Production.json` under `Events`, and
  `Add-ContentSecurityPolicy` adds both endpoints (`https:` and `wss:`) to `connect-src`. `deploy-policy.json` gains
  `appsync:*Api`, `appsync:*ChannelNamespace` and `appsync:TagResource` on the stack's resources. `remove.ps1` needs
  nothing: the API is a stack resource.
- **`test-auth.ps1`:** as the test user, publish to the user's own channel over HTTPS (expect 200), then to another
  user's channel (expect the handler's rejection); then `GlobalSignOut` and confirm the old access token is refused.
  Subscriptions are not tested from PowerShell; the UI tests cover the browser side against the local channel, and
  the maintainer tests the deployed channel by hand with two browsers.

## Steps

- [x] `Events` package: contract, model, options, local `BroadcastChannel` implementation, lifecycle handler, unit
      tests with a fake JavaScript runtime, `PublicAPI.Unshipped.txt`, README.
- [x] Browser: `IDeviceNameService` (reads, makes the default and saves the name in local storage), unit tests.
- [x] UI: the "This device" Settings section and its view model, text in `UiText`, unit tests, and a Playwright test
      that renames the device and sees the new name in the other page's sign-out message.
- [x] `Events.AppSync` package: the ES module and channel, token renewal and reconnect, unit tests, README.
- [x] Auth: `SignOutEverywhereAsync(SignOutReason)`, `SignOutReason`, `ISignOutEverywhereHandler`, `EndSessionAsync`,
      `SessionEndedReason` and `SessionEndedEventArgs` in the contract, Cognito and placeholder implementations, the
      existing `SessionEnded` subscribers moved to the new signature, unit tests.
- [x] Auth: the resume check (on `visibilitychange` to visible and on coming back online, check the session with
      Cognito before reconnecting or syncing; end the session if it is refused), unit tests with `FakeTimeProvider`.
- [x] UI: the Profile page button, confirmation and the per-reason "signed out" messages, in `UiText`; view
      model tests; a Playwright test that signs in on two pages of one browser context, signs out everywhere on one,
      and sees the other return to sign in.
- [x] Stack, `deploy.ps1`, `deploy-policy.json`, CSP, `test-auth.ps1`; parse-check them here. Deploying and running
      `test-auth.ps1` is the maintainer's, as always.
- [x] Template: the `Events` option and its creation warning, `AddBlazwizAppSyncEvents` in `Program.cs` and the
      `Events` section in `appsettings.json` behind `IncludeEvents`, the stack parameter in the deploy settings,
      `exclude` lists, README. `test-template.ps1` creates apps with events on and off.
- [x] Missing infrastructure: no channel and one warning when endpoints are missing outside development; the Profile
      button hidden without a handler; unit tests for both.
- [x] Documentation: package table (root README, `docs/diagrams/overview.md`), a `docs/diagrams/user-events.md`
      sequence of sign out everywhere, `template/cloudformation/README.md` (the API, its cost, the deployer actions),
      `CHANGELOG.md`.
- [x] Upgrade test: `./templatepack/test-template.ps1 -UpgradeFrom latest`, since the stack, a configuration section
      and `Program.cs` change.
- [ ] Later events, each its own small change once the channel exists: `mfa-changed`, `profile-deleted`. Key rotation
      needs no new event: it calls `SignOutEverywhereAsync(SignOutReason.KeysRotated)`.

## As built

Where the build differs from the design above, or settled what it left open:

- **The session is passed down, not looked up.** `ISignOutEverywhereHandler.OnSigningOutEverywhereAsync` and
  `IUserEventChannel.PublishAsync` take the `UserSession`. The AppSync channel getting it from
  `IAuthenticationService` made a dependency cycle (auth service, notifier, handler, channel, auth service).
  `ISignOutEverywhereNotifier` (Auth) runs the handlers, and its `HasHandlers` is what shows the Profile button.
- **The resume check** is `ISessionResumeMonitor` (Auth). It is started with the session by
  `SessionResumeSessionHandler`, listens to `IPageResumeService` (Browser), and raises `SessionResumed` when the check
  passes. `UserEventsSessionHandler` reconnects on that. The check uses Cognito `GetUser` (`CheckAccessTokenAsync`). A
  sync started by the same resume runs alongside the check, not after it: one pass can run before a revoked instance
  signs out, syncing the user's own files under keys they still hold.
- **Configuration:** `AppSyncEventsConfig` (in `Events.AppSync`, section `Events`) holds the API's hosts as the stack
  outputs give them (`Dns.Http`, `Dns.Realtime`), not URLs. `appsettings.json` has no `Events` section: without one,
  development uses the local channel, and `deploy.ps1` writes it to `appsettings.Production.json`.
- **The UI handover:** `SessionEndedNoticeService` (UI) carries the reason from the shell to the sign-in page. An
  expired session shows no message, as before.
- **`test-auth.ps1`** takes `-SkipSignOutEverywhere`, because its last check signs the test user out on every device.
- **Verified on the deployed stack** (`myapp-test`, two browsers): signing out everywhere on one signed the other out
  at once, naming the device. AppSync accepts the publish made just after `GlobalSignOut`, with the access token
  Cognito has just revoked, so the publish stays after it.
- **The upgrade test** (`test-template.ps1 -UpgradeFrom latest`, from release 0.7.2) passed. It also showed that an
  app made before events, whose `deploy.ps1` passes no `-Events`, would have got the API on its next deploy from the
  base stack's default. The default is now `disabled`; apps created with events pass `enabled`.

## Decided

- **The event only ends the session.** It does not clear remembered passwords or passkeys (`saved_users`). It is
  sent for two reasons only: the user asks to sign out everywhere, or the keys are rotated. Key rotation ends with a
  full sign out everywhere (`GlobalSignOut` and the event, reason `keys-rotated`), so every instance, the rotating one
  included, drops its old keys, and an instance that misses the event is caught by the resume check. A stronger
  "forget this user on every device" level is left out: a device that misses the event would sign in again with what
  it remembers, so that level would need a durable marker (a Cognito attribute or a file in S3) checked at sign in.
  Revisit only if a "lost device" feature is asked for.
- **One API per stack.** Each stack is already per environment and keeps its own resources. The API name
  `${AppName}-${Environment}-events` is at most 39 characters (AppName max 21, Environment max 10), within AppSync's
  limit of 50 (`[A-Za-z0-9_\- ]+`). The default limit is 50 Event APIs per region and can be raised, so several
  environments in one account are fine. A `sub` (36 characters) fits the 50-character channel segment limit.
- **Missed events are caught by a resume check, and token lifetimes stay as they are.** AppSync Events does not
  replay, and mobile browsers freeze backgrounded PWAs, so many instances will miss the event. Rather than deliver it
  to a sleeping instance (Web Push would need a Lambda and a visible notification for every message), every instance
  checks its session with Cognito when it resumes or comes back online. `AccessTokenValidity` and `IdTokenValidity`
  stay at 60 minutes: shortening them would only narrow the remaining case (an instance open and in use whose socket
  dropped silently), they are independent of events, and 5 minutes would not work anyway with the 5-minute renewal
  margin (`CognitoAuthenticationService.TokenRefreshMargin`).
