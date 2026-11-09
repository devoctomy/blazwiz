---
title: User events over AppSync Events, starting with "sign out everywhere"
type: backlog
status: not started
added: 2026-11-09
urgency: medium
before-1.0: recommended
reason: an instance of the app has no way to tell the user's other instances that something changed; sign-out, key rotation and MFA all need one
areas: [AWS stack, Auth, Auth.Cognito, UI, a new Events package, template]
effort: medium (the channel and the first feature); each later event is small
functional-change: yes
blocked-by: none
related:
  - ../../reviews/2026-11-08-security-review/backlog/passphrase-change-key-rotation.md (other devices must learn the keys changed)
  - ../../reviews/2026-11-08-security-review/backlog/mfa-totp.md
  - ../../reviews/2026-11-08-security-review/security-review.md (SR6, SR20)
---

# User events over AppSync Events, starting with "sign out everywhere"

A user's instances of the app (a desktop browser, a phone, a second tab) share data through S3 but never talk to each
other. When one of them does something that affects the others, nothing tells them: a passphrase change leaves other
devices holding keys that no longer match, a sign out on one device leaves the others signed in until their tokens
expire, and a future MFA set-up would not be noticed until the next sign in. This plan adds a per-user event channel
and uses it first for the simplest, most testable feature: signing out everywhere.

## Principle: push is an accelerator, never the source of truth

blazwiz is offline-first, so an instance that is closed or offline when an event is published never receives it.
Every feature built on the channel therefore needs a durable fallback that works with no channel at all, and the
event carries only "something changed, check now", never the change itself and never a secret. The receiving instance
does what it would have done anyway, just sooner.

For sign out, the durable fallback already exists: Cognito's `GlobalSignOut` revokes the user's refresh tokens and,
with `EnableTokenRevocation` on (it is), their access tokens. An instance that misses the event finds out at its next
token use. The lag is bounded by `AccessTokenValidity` (60 minutes in `stack.yaml`; Cognito allows 5) and by the
identity pool's AWS credentials, which last an hour and cannot be shortened. The event removes that lag while the
instance is open.

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
- **Event shape:** a small JSON object, `{ "type": "signed-out-everywhere", "at": "<ISO 8601>", "from": "<instance id>" }`.
  `from` is a random id the instance makes when it connects, so it can ignore its own events. Nothing else: no tokens,
  no keys, no file names.
- **Connection lifecycle:** connect in `ISessionLifecycleHandler.OnShellLoadedAsync` (the background hook), reconnect
  with backoff when the socket drops or the device comes back online (`Sync`'s `network.js` already watches this),
  re-authenticate when the token is renewed, disconnect on sign out and when the session ends. Never connect while
  offline or with the placeholder sign in.

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
  `Events:RealtimeEndpoint` are set, and falls back to the local channel otherwise, the way `AddBlazwizCognito` falls
  back to the placeholder in development.
- The package table in the README and `docs/diagrams/overview.md` gain both packages. `Sync` does not depend on
  `Events`; a later plan may have `UI.Sync` listen for a `keys-changed` event.

### First feature: sign out everywhere

- **Auth:** `IAuthenticationService.SignOutEverywhereAsync()`. The Cognito implementation calls `GlobalSignOut` with
  the access token (`CognitoUser.GlobalSignOutAsync` in `Amazon.Extensions.CognitoAuthentication`), then ends the
  local session as `SignOutAsync` does. The placeholder implementation just ends the local session. If `GlobalSignOut`
  fails because the device is offline, the button reports it and does nothing else: a local sign out that pretends to
  be global would be worse than an error.
- **Events:** after a successful `GlobalSignOut` and before the local session ends, publish `signed-out-everywhere`.
  On receiving it, an instance ends its session the way an expired one does (`SessionEnded` fires, the shell returns
  to the sign-in page, and every `ISessionLifecycleHandler` runs its sign-out work: the PRF-derived keys are wiped by
  `DerivedKeySessionHandler`, protected profiles lock). It shows `UiText` saying the user signed out from another
  device.
- **UI:** a "Sign out everywhere" button on the Profile page, under the existing sign out, with a confirmation that
  says what it does and that other devices may take up to an hour to notice if they are offline. Text in `UiText`;
  `PseudoLocalizationTests` covers it.
- **Stack (`stack.yaml`):** `AWS::AppSync::Api` with `EventConfig` (auth provider `AMAZON_COGNITO_USER_POOLS` on the
  stack's user pool and client, as the connection, default publish and default subscribe modes), an
  `AWS::AppSync::ChannelNamespace` `users` with the handler code inline, and outputs `EventsHttpEndpoint` and
  `EventsRealtimeEndpoint`. `deploy.ps1` writes them into `appsettings.Production.json` under `Events`, and
  `Add-ContentSecurityPolicy` adds both endpoints (`https:` and `wss:`) to `connect-src`. `deploy-policy.json` gains
  `appsync:*Api`, `appsync:*ChannelNamespace` and `appsync:TagResource` on the stack's resources. `remove.ps1` needs
  nothing: the API is a stack resource.
- **`test-auth.ps1`:** as the test user, publish to the user's own channel over HTTPS (expect 200), then to another
  user's channel (expect the handler's rejection); then `GlobalSignOut` and confirm the old access token is refused.
  Subscriptions are not tested from PowerShell; the UI tests cover the browser side against the local channel, and
  the maintainer tests the deployed channel by hand with two browsers.

### Access token validity

Lower `AccessTokenValidity` and `IdTokenValidity` from 60 to 15 minutes in the same change, so the fallback lag for an
instance that misses the event is a quarter of what it is now. The portal renews with the in-memory refresh token
while a tab is open, so users see no difference. Going to 5 minutes is possible but means a renewal every few minutes
on every open tab; decide after watching the deployed app.

## Steps

- [ ] `Events` package: contract, model, options, local `BroadcastChannel` implementation, lifecycle handler, unit
      tests with a fake JavaScript runtime, `PublicAPI.Unshipped.txt`, README.
- [ ] `Events.AppSync` package: the ES module and channel, token renewal and reconnect, unit tests, README.
- [ ] Auth: `SignOutEverywhereAsync` in the contract, Cognito and placeholder implementations, unit tests.
- [ ] UI: the Profile page button, confirmation and the "signed out from another device" message, in `UiText`; view
      model tests; a Playwright test that signs in on two pages of one browser context, signs out everywhere on one,
      and sees the other return to sign in.
- [ ] Stack, `deploy.ps1`, `deploy-policy.json`, CSP, `test-auth.ps1`; parse-check them here. Deploying and running
      `test-auth.ps1` is the maintainer's, as always.
- [ ] Template: `AddBlazwizAppSyncEvents` in `Program.cs`, `Events` section in `appsettings.json` (empty), README.
- [ ] Documentation: package table (root README, `docs/diagrams/overview.md`), a `docs/diagrams/user-events.md`
      sequence of sign out everywhere, `template/cloudformation/README.md` (the API, its cost, the deployer actions),
      `CHANGELOG.md`.
- [ ] Upgrade test: `./templatepack/test-template.ps1 -UpgradeFrom latest`, since the stack, a configuration section
      and `Program.cs` change.
- [ ] Later events, each its own small change once the channel exists: `keys-changed` (from the key rotation plan),
      `mfa-changed`, `profile-deleted`.

## Open questions

- Should the event also tell a receiving instance to clear its remembered password and passkey (`saved_users`), or
  only end the session? Ending the session matches "sign out"; clearing more is "I lost this device", which may want
  its own button and event.
- One API per stack or one per environment? A stack is already per environment (`AppName-Environment`), so one per
  stack. Confirm the API name length limit allows `${AppName}-${Environment}-events`.
- Whether to keep `AccessTokenValidity` at 15 minutes or go lower, after seeing renewal traffic in the deployed app.
