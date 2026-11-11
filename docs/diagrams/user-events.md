# User events: signing out everywhere

A user's open instances of the app (tabs and devices) share an event channel, so one can tell the others that
something changed. The first event is "signed out everywhere". It is sent when the user asks for it on the Profile
page, and when they change a profile's passphrase (section 6).

Events only make things happen sooner. An instance that is closed, offline or in the background when an event is sent
never receives it: mobile browsers freeze a backgrounded app and drop its connection, and AppSync Events keeps nothing
to replay. The sign-in service is the durable record: `GlobalSignOut` revokes every session, and each instance checks
its session when it resumes.

| Part | Package | Class |
|---|---|---|
| Sign out everywhere, end a session from elsewhere, the resume check | Auth, Auth.Cognito | `IAuthenticationService` (`SignOutEverywhereAsync`, `EndSessionAsync`, `CheckSessionAsync`), `CognitoAuthenticationService`, `LocalAuthenticationService` |
| Hooks other packages implement | Auth | `ISignOutEverywhereHandler`, run by `SignOutEverywhereNotifier` |
| Checking the session when the app resumes | Auth, Browser | `SessionResumeMonitor` (started by `SessionResumeSessionHandler`), `PageResumeService` (`js/page-resume.js`) |
| The device's name | Browser | `DeviceNameService` (`js/device.js`), local storage `device_name` |
| The channel | Events, Events.AppSync | `IUserEventChannel`: `LocalUserEventChannel` (`BroadcastChannel`), `AppSyncUserEventChannel` (`js/appsync-events.js`) |
| Connecting, receiving, publishing | Events | `UserEventsSessionHandler`, `UserEventsSignOutHandler` |
| The button, the messages, the Settings section | UI | `ProfileViewModel`, `MainLayoutViewModel`, `SessionEndedNoticeService`, `LoginViewModel`, `DeviceSettingsViewModel` |
| Signing out everywhere after a passphrase change | UI.Sync | `E2eRotationSignOut`, `KeyRotationSignOutHandler`, `SessionCheckKeyMissingHandler` |

Nothing depends on `Events` but the app: `Auth` defines the hooks, `Events` implements them, and `UI` only talks to
`Auth`. Without `Events` (an app created with `--events false`), the Profile page does not offer the button, because
`SignOutEverywhereNotifier.HasHandlers` is false. `SignOutEverywhereAsync` still works for key rotation:
`GlobalSignOut` revokes every session, and the resume check signs other devices out when they next wake.

## 1. Signing out everywhere

```mermaid
sequenceDiagram
    actor User
    participant Profile as ProfileViewModel
    participant Auth as CognitoAuthenticationService
    participant Cognito as Cognito user pool
    participant Notifier as SignOutEverywhereNotifier
    participant Handler as UserEventsSignOutHandler
    participant Channel as AppSyncUserEventChannel
    participant AppSync as AppSync Events API
    participant Other as Other device

    User->>Profile: Sign out everywhere, then confirm
    Profile->>Auth: SignOutEverywhereAsync(UserRequested)
    Auth->>Cognito: GlobalSignOut(access token)
    alt Unreachable, or offline mode
        Auth-->>Profile: false (nothing changed)
        Profile-->>User: "Couldn't reach the sign-in service, so nothing was changed."
    else Revoked (or already revoked)
        Cognito-->>Auth: every refresh and access token revoked
        Auth->>Notifier: NotifyAsync(session, UserRequested)
        Notifier->>Handler: OnSigningOutEverywhereAsync(session, reason, device name)
        Handler->>Channel: PublishAsync(session, signed-out-everywhere)
        Channel->>AppSync: POST /event to /users/<sub>
        AppSync-->>Other: data on /users/<sub>
        Auth->>Auth: LogoutAsync (local session removed)
        Auth-->>Profile: true
        Profile->>Profile: lifecycle SigningOutAsync, SignedOutAsync
        Profile-->>User: login page
    end
```

The event is `{ "type": "signed-out-everywhere", "reason": "user-requested" | "keys-rotated", "device": "<name>",
"at": "<time>", "from": "<instance id>" }`. It carries no tokens, keys or file names. The `users` namespace's handlers
refuse any channel but the caller's own `/users/<sub>`.

## 2. Receiving it

```mermaid
sequenceDiagram
    participant AppSync as AppSync Events API
    participant Channel as AppSyncUserEventChannel
    participant Handler as UserEventsSessionHandler
    participant Auth as IAuthenticationService
    participant Shell as MainLayoutViewModel
    participant Notice as SessionEndedNoticeService
    participant Login as LoginViewModel

    AppSync->>Channel: data (the event, as JSON)
    Channel->>Channel: ignore it if it is this instance's own (from)
    Channel->>Handler: Received
    Handler->>Auth: EndSessionAsync(SignedOutEverywhere or KeysRotated, sanitised device name)
    Auth->>Auth: LogoutAsync
    Auth->>Shell: SessionEnded(reason, device name)
    Shell->>Notice: Set(reason, device name)
    Shell->>Shell: LogoutAsync: modules stop, keys wiped, login page
    Login->>Notice: Take()
    Login-->>Login: "You were signed out from <device>." / "Your passphrase was changed on <device>..."
```

## 3. Devices that miss it: the resume check

```mermaid
sequenceDiagram
    participant Browser as Browser (page-resume.js)
    participant Resume as PageResumeService
    participant Monitor as SessionResumeMonitor
    participant Auth as CognitoAuthenticationService
    participant Cognito as Cognito user pool
    participant Handler as UserEventsSessionHandler

    Browser->>Resume: visible again, or back online
    Resume->>Monitor: Resumed
    Monitor->>Auth: CheckSessionAsync()
    Auth->>Auth: GetCurrentUserAsync (renews or ends the session when due)
    Auth->>Cognito: GetUser(access token)
    alt Revoked (NotAuthorizedException)
        Auth->>Auth: EndSessionAsync(Revoked): "You were signed out."
    else Accepted
        Auth-->>Monitor: true
        Monitor->>Handler: SessionResumed
        Handler->>Handler: reconnect the channel if it dropped
    else Unreachable, throttled, or offline mode
        Auth-->>Monitor: true (kept; checked again next time)
    end
```

An instance that stays open and in use but silently loses its connection misses the event and passes no resume. It
finds out at its next token renewal, at most `AccessTokenValidity` (60 minutes) less the 5-minute renewal margin
(`CognitoAuthenticationService.TokenRefreshMargin`). A frozen instance runs no code, so it is no risk while it sleeps.
The resume check and a sync started by the same resume run side by side, so one sync pass can run before the check
signs a revoked instance out. That pass syncs the user's own files under keys they still hold.

## 4. The connection

`UserEventsSessionHandler` connects the channel when the session starts (`OnSessionStartedAsync`). It does not
connect for an offline session, or while offline mode is on, because offline mode makes no network calls. When the
connection drops it reconnects with backoff (`FirstReconnectDelay` doubling up to `MaxReconnectDelay`), and straight
away after a resume whose check passed. It disconnects on sign out.

`AppSyncUserEventChannel` connects to `wss://<Events:RealtimeEndpoint>/event/realtime` with the subprotocols
`aws-appsync-event-ws` and `header-<base64url of { host, Authorization }>`. It then sends `connection_init`, waits
for `connection_ack`, and subscribes to `/users/<sub>`. The `ka` keep-alives reset a timer, and if they stop the
connection is closed and reconnected. Publishing is `POST https://<Events:HttpEndpoint>/event`, authorised with the
session's Cognito access token.

`AddBlazwizAppSyncEvents` picks the channel:

- When the `Events` settings are present, the AppSync channel.
- Without them in development, `LocalUserEventChannel`, which reaches this browser's other tabs only (the UI tests
  use it).
- Without them anywhere else, no channel, and `MissingEventsConfigurationWarning` logs that the app runs without
  events.

## 5. The device's name

`DeviceNameService` keeps a name for this browser in local storage (`device_name`). Until the user renames it under
Settings, This device, it is made on first use as `<Platform>-<Browser>-<6 random digits>`, e.g.
`Android-Edge-624834`. Platform and browser are a best guess from `navigator.userAgentData` or the user-agent string,
with `Unknown` when not recognised. The name is only ever sent in events on the user's own channel. Receivers
sanitise it (control characters removed, cut to 32 characters) and show it as plain text.

## 6. After a passphrase change

Changing a profile's passphrase rotates its keys and encrypts its files again ([sync.md](sync.md), diagram 8). It ends
by signing out everywhere with the reason `KeysRotated`, so every device forgets the old keys and unlocks again with
the new passphrase.

```mermaid
sequenceDiagram
    participant VM as E2eProfilesViewModel / E2eUnlockPanelViewModel
    participant Out as E2eRotationSignOut
    participant Auth as IAuthenticationService
    participant Notifier as SignOutEverywhereNotifier
    participant Rot as KeyRotationSignOutHandler
    participant Svc as SyncKeyRotationService
    participant Mgr as E2eProtectedProfileManager
    participant Events as UserEventsSignOutHandler

    Note over VM: every file is encrypted again and uploaded
    VM->>Out: SignOutEverywhereAsync()
    Out->>Auth: SignOutEverywhereAsync(KeysRotated)
    alt Unreachable, or offline mode
        Auth-->>Out: false (nothing changed)
        Out-->>VM: false
        VM-->>VM: the rotation mark stays, "Sign out everywhere" is offered again
    else Revoked
        Auth->>Notifier: NotifyAsync(session, KeysRotated)
        Notifier->>Rot: OnSigningOutEverywhereAsync
        Rot->>Svc: CompleteAsync()
        Svc->>Mgr: CompleteRotationAsync(scope): the keys document's rotation mark is cleared
        Notifier->>Events: OnSigningOutEverywhereAsync: publishes signed-out-everywhere, reason keys-rotated
        Auth->>Auth: LogoutAsync
        Auth-->>Out: true
        Out->>Out: lifecycle SigningOutAsync, SignedOutAsync, login page
    end
```

The mark is cleared after every session is revoked and before this device signs out, while it can still reach the
user's keys. If clearing it fails, the device is signed out all the same and the mark stays: whoever unlocks the
profile next is asked to finish the change, which finds nothing left to encrypt and signs out everywhere again.

Other devices:

- **Open and connected** devices receive the event and sign out at once, and the sign-in page says the passphrase was
  changed.
- **Closed, offline or backgrounded** devices miss it. Their tokens are revoked, so the resume check (section 3) signs
  them out before they sync.
- **A device that unlocks with the passkey** is asked for the new passphrase: its copy of the old master key no longer
  unwraps the keys document (`E2eErrorCodes.KeysChanged`).
- **An open device whose connection dropped silently** keeps its session until its next token renewal. If it reads a
  file written under the new key first, `SessionCheckKeyMissingHandler` runs `CheckSessionAsync`, which signs it out.
