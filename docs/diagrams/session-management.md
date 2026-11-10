# Session management and expiry

How long a session lasts, when the tokens are renewed, what ends a session, what modules do at sign in and sign out,
and what a reload keeps and loses.

Main classes:

- `CognitoAuthenticationService.GetCurrentUserAsync` (`src/Devoctomy.Blazwiz.Auth.Cognito/Services/CognitoAuthenticationService.cs`): the one place sessions are checked, renewed and ended
- `SessionTimerViewModel` (`src/Devoctomy.Blazwiz.UI/ViewModels/SessionTimerViewModel.cs`): the countdown under the user's name, and the clock that drives renewal. It reads the renewal window from `IAuthenticationService.RefreshMargin`
- `MainLayoutViewModel` (`src/Devoctomy.Blazwiz.UI/ViewModels/MainLayoutViewModel.cs`): starts the session when the shell loads, signs out, and reacts to `SessionEnded`
- `SessionLifecycle` (`src/Devoctomy.Blazwiz.Auth/Sessions/SessionLifecycle.cs`) and the `ISessionLifecycleHandler`s packages and apps add with `AddSessionLifecycleHandler<T>()`
- `OfflineModeService` (`src/Devoctomy.Blazwiz.Core/Offline/OfflineModeService.cs`) and `InMemoryOfflineSessionStore` (`src/Devoctomy.Blazwiz.Auth/Services/InMemoryOfflineSessionStore.cs`)

Storage names use the template's app name, `myapp`, which the storage clients add (`IAppStorageKeys`).

## Lifetimes

| Thing | Lifetime | Set by |
|---|---|---|
| ID and access tokens (`UserSession.ExpiresAt`) | 60 minutes | `IdTokenValidity` / `AccessTokenValidity` in `src/Devoctomy.Blazwiz.Aws/cloudformation/stack.yaml` |
| Renewal window | Last 5 minutes before `ExpiresAt` | `CognitoAuthenticationService.TokenRefreshMargin`, read through `IAuthenticationService.RefreshMargin` |
| Refresh token | 12 hours, and only while this tab stays open | `RefreshTokenValidity`, kept in memory only |
| AWS credentials | As issued by the identity pool (about 1 hour), fetched again 5 minutes before expiry | `CognitoAwsCredentialsProvider.RefreshMargin` |
| Offline session | 15 minutes from going offline, no renewal | `OfflineModeService.SessionLength` |
| Placeholder session (local development) | 8 hours, no renewal (`RefreshMargin` is zero) | `LocalAuthenticationService.SessionDuration` |
| An unlocked sync profile | 15 minutes without a read or write, by default | `E2eProfileSettings.IdleTimeout`, run by `E2eIdleLock` |

A session is only ever renewed **before** it expires. A token that expired while nobody was using the tab is not
brought back to life, even if the refresh token is still valid.

## What survives a reload

| Kept (localStorage, IndexedDB, OPFS, a picked folder) | Lost (memory) |
|---|---|
| `myapp_cognito_session`: the session with ID and access tokens | The refresh token, so the session can no longer be renewed |
| `myapp_saved_users`: remembered users and encrypted passwords | The offline session and offline mode, so a reload always leaves offline mode |
| `myapp_e2e_keys_{profile id}_{user}`, `myapp_e2e_index_{user}`, `myapp_e2e_labels_{user}`: this device's copies of the profiles' keys, their index, and their labels | Every PRF-derived key and every unlocked profile's keys in `IDerivedKeyStore` |
| `myapp_sync_disabled`, `myapp_user_settings` (theme, sidebar), `myapp_culture`, `myapp_data_versions` | AWS credentials and the S3 client |
| The profiles' files, the sync index and the sync event log | Scheduler, connectivity and sync status |

So after a reload the user is still signed in until the ID token reaches its renewal window, then is sent to the
login page (one touch with a passkey). Every sync profile is locked again until its passphrase is entered, or the
passkey is used where the profile allows it ([prf-remember-me.md](prf-remember-me.md), diagram 4).

## 1. The session lifecycle

What modules do at sign in, when the shell loads, and at sign out is not written into the shell: each package adds an
`ISessionLifecycleHandler`, and `SessionLifecycle` calls them in `Order`, one at a time. One that throws is skipped,
so a module failing (for example offline) never stops the others, the sign in or the sign out.

| Handler | Package | Order | What it does |
|---|---|---|---|
| `DataMigrationRunner` | Auth | `int.MinValue` | Shell loading: runs the packages' `IDataMigration`s, recording versions in `myapp_data_versions` |
| `AwsCredentialsSessionHandler` | Auth.Cognito | 0 | Session started: fetches the AWS credentials in the background. Signed out: forgets them |
| `SessionResumeSessionHandler` | Auth | 0 | Session started: checks the session with the sign-in service whenever the app resumes (`SessionResumeMonitor`). Signing out: stops ([user-events.md](user-events.md), diagram 3) |
| `UserEventsSessionHandler` | Events | 0 | Session started: connects the user's event channel, and ends the session when another device signs out everywhere. Signing out: disconnects ([user-events.md](user-events.md)) |
| `SyncSessionHandler` | UI.Sync | 100 | Signed in: asks for persistent storage. Shell loading: reads "sync turned off". Session started: starts sync ([sync.md](sync.md), diagram 1). Signing out: stops sync and locks every profile |
| `DerivedKeySessionHandler` | Auth | `int.MaxValue` | Signed out: wipes every key in `IDerivedKeyStore` (the passkey's `rememberme`, `e2e-kek` and `e2e-labels`), after the modules have locked what they hold, whatever page the app shows next |

Apps add their own handlers the same way.

```mermaid
sequenceDiagram
    autonumber
    actor User
    participant Login as LoginViewModel
    participant Layout as MainLayoutViewModel
    participant Auth as IAuthenticationService
    participant Life as SessionLifecycle
    participant H as Handlers, in Order

    User->>Login: Sign in (password or passkey, online or offline)
    Login->>Auth: LoginAsync / SignInOfflineAsync
    Login->>Login: Sign-in options: ApplyAsync() each
    Login->>Life: SignedInAsync()
    Life->>H: OnSignedInAsync() (while the click still counts, e.g. permission prompts)
    Login->>Layout: Navigate to /

    Note over Layout: Also on opening the app with a saved session
    Layout->>Auth: GetCurrentUserAsync()
    alt No session
        Layout->>Layout: Navigate to /login
    else Signed in
        Layout->>Life: ShellLoadingAsync(session) (awaited: the shell waits for it)
        Life->>H: OnShellLoadingAsync(session) (data migrations, settings the shell shows)
        Layout->>Layout: IsReady = true, the page renders
        Layout-)Life: SessionStartedAsync(session) (not awaited)
        Life->>H: OnSessionStartedAsync(session) (background work: AWS credentials, sync)
    end

    User->>Layout: Sign out (or the session ends, diagram 5)
    Layout->>Life: SigningOutAsync()
    Life->>H: OnSigningOutAsync() (stop work while still signed in)
    Layout->>Auth: LogoutAsync()
    Layout->>Life: SignedOutAsync()
    Life->>H: OnSignedOutAsync() (forget what they hold)
    Layout->>Layout: Navigate to /login
```

## 2. How `GetCurrentUserAsync` decides

Everything that needs the user (the shell, the session timer, sync's user context, the AWS credentials provider)
goes through this method, so this is where sessions are renewed and ended.

```mermaid
sequenceDiagram
    autonumber
    participant Caller as Any caller
    participant Auth as CognitoAuthenticationService
    participant Clock as TimeProvider
    box rgb(240,240,255) In memory
        participant OSS as Offline session (memory)
        participant OMS as Offline mode flag (memory)
        participant Mem as refreshToken (memory)
    end
    box rgb(255,245,230) Browser storage
        participant LS as localStorage
    end
    box rgb(235,250,235) AWS
        participant UP as Cognito User Pool
    end

    Caller->>Auth: GetCurrentUserAsync()
    Auth->>OSS: Session?
    alt Offline session exists
        Auth->>Clock: GetUtcNow()
        alt Past ExpiresAt (15 min)
            Auth->>Auth: EndSessionAsync() (diagram 5)
            Auth-->>Caller: null
        else Still valid
            Auth-->>Caller: Offline session (no tokens)
        end
    else No offline session
        Auth->>OMS: IsOffline?
        alt Offline mode on (e.g. its session already ended)
            Auth-->>Caller: null (nothing may call Cognito)
        else Online
            Auth->>LS: GET myapp_cognito_session
            alt Nothing stored
                Auth-->>Caller: null
            else No ID token
                Auth->>LS: REMOVE myapp_cognito_session
                Auth-->>Caller: null
            else More than 5 min before ExpiresAt
                Auth-->>Caller: Stored session
            else Already past ExpiresAt
                Auth->>Auth: EndSessionAsync()
                Auth-->>Caller: null
            else In the last 5 min, but no refresh token in this tab
                Note over Auth: Signed in from another tab, or the page was reloaded
                Auth->>Auth: EndSessionAsync()
                Auth-->>Caller: null
            else In the last 5 min, refresh token held
                Auth->>Mem: Read refreshToken
                Auth->>UP: StartWithRefreshTokenAuthAsync (REFRESH_TOKEN_AUTH)
                alt New tokens
                    UP-->>Auth: IdToken, AccessToken, ExpiresIn
                    Auth->>Mem: refreshToken (replaced only if Cognito returns a new one)
                    Auth->>LS: SET myapp_cognito_session {same SignedInAt, new tokens, new ExpiresAt}
                    Auth-->>Caller: Renewed session
                else No ID token returned, or the refresh token expired or was revoked (CognitoException)
                    Auth->>Auth: EndSessionAsync()
                    Auth-->>Caller: null
                else Network failure
                    Note over Auth,LS: Stored session kept, renewal tried again on the next call while still valid
                    Auth-->>Caller: null
                end
            end
        end
    end
```

## 3. The session timer drives renewal

The countdown ticks every second and reads the session again every 30 seconds, but only while reading has no side
effects. In the renewal window it stops reading until the token has expired, so the renewal happens when something
else (a sync pass, a credentials fetch, a page) next asks for the user, or the session ends at expiry. The UI test
`ShellTests.GivenSessionAboutToExpire_WhenLeftIdle_ThenLoginPageIsShownAtExpiry` covers the end.

```mermaid
sequenceDiagram
    autonumber
    participant Timer as ITimer from TimeProvider (1 s)
    participant VM as SessionTimerViewModel
    participant Auth as IAuthenticationService
    box rgb(240,240,255) In memory
        participant Mem as Memory (.NET fields)
    end
    box rgb(255,245,230) Browser storage
        participant LS as localStorage
    end

    VM->>Auth: GetCurrentUserAsync() (on init)
    Auth->>LS: GET myapp_cognito_session
    VM->>Mem: expiresAt, isOffline, lastReadAt, nextReadAt = now + 30 s
    loop Every second
        Timer->>VM: TickAsync()
        alt Before the renewal window (ExpiresAt - RefreshMargin) and 30 s since the last read
            VM->>Auth: GetCurrentUserAsync() (picks up tokens renewed elsewhere)
            VM->>Mem: expiresAt, nextReadAt
        else In the renewal window (last 5 min)
            VM->>VM: No read, just count down
        else Just passed expiresAt (first time), or 30 s since the last read after it
            VM->>Auth: GetCurrentUserAsync()
            Auth->>Auth: Expired, EndSessionAsync() (diagram 5)
            Auth-->>VM: null
            VM->>Mem: expiresAt = null
        else Nothing to read
            VM->>VM: UpdateText()
        end
        VM->>VM: Text from UiText: Session.Remaining / Session.OfflineRemaining / Session.Expired
    end
    Note over VM: Offline mode turning on or off triggers an immediate read too
```

## 4. Renewal while the app is in use

```mermaid
sequenceDiagram
    autonumber
    participant Sync as Sync pass / page
    participant Creds as CognitoAwsCredentialsProvider
    participant Auth as CognitoAuthenticationService
    box rgb(240,240,255) In memory
        participant Mem as Memory (.NET fields)
    end
    box rgb(255,245,230) Browser storage
        participant LS as localStorage
    end
    box rgb(235,250,235) AWS
        participant UP as Cognito User Pool
        participant IP as Cognito Identity Pool
    end

    Note over Sync,IP: 57 minutes after sign in, a sync pass needs S3
    Sync->>Creds: GetCredentialsAsync()
    Creds->>Auth: GetCurrentUserAsync()
    Auth->>LS: GET myapp_cognito_session (ExpiresAt in 3 min)
    Auth->>Mem: refreshToken
    Auth->>UP: REFRESH_TOKEN_AUTH
    UP-->>Auth: New ID + access tokens (60 min)
    Auth->>LS: SET myapp_cognito_session (new ExpiresAt)
    Auth-->>Creds: Session with new ID token
    Creds->>Mem: cached credentials
    alt Credentials still more than 5 min from expiry
        Creds-->>Sync: Cached credentials
    else Near expiry
        Creds->>IP: GetId + GetCredentialsForIdentity (new ID token)
        IP-->>Creds: New credentials
        Creds->>Mem: cached = new credentials
        Creds-->>Sync: New credentials (S3 client rebuilt)
    end
    Note over Mem: This repeats every hour until the refresh token's 12 hours run out, then diagram 5
```

## 5. A session ends

`EndSessionAsync` signs out and raises `SessionEnded` with the reason (`SessionEndedReason`): `Expired` here, `Revoked`
from the resume check, `SignedOutEverywhere` or `KeysRotated` when another device signed the user out everywhere, with
that device's name ([user-events.md](user-events.md)). The shell hands the reason to the login page
(`SessionEndedNoticeService`), which says why for every reason but `Expired`. `LogoutAsync` on its own (the Sign out
button) and `SignOutEverywhereAsync` do not raise it.

```mermaid
sequenceDiagram
    autonumber
    participant Auth as CognitoAuthenticationService
    participant Layout as MainLayoutViewModel
    participant Life as SessionLifecycle
    participant SyncH as SyncSessionHandler
    participant AwsH as AwsCredentialsSessionHandler
    participant Login as LoginViewModel
    box rgb(240,240,255) In memory
        participant Mem as Memory (.NET fields)
        participant DKS as DerivedKeyStore (memory)
    end
    box rgb(255,245,230) Browser storage
        participant LS as localStorage
    end
    box rgb(235,250,235) AWS
        participant UP as Cognito User Pool
    end

    Note over Auth: Expired, refresh refused, no refresh token in the renewal window, offline session expired, or offline mode turned off
    Auth->>Auth: LogoutAsync()
    Auth->>Mem: pendingNewPassword = null, offline session cleared, refreshToken = null
    Auth->>LS: REMOVE myapp_cognito_session
    opt Had a refresh token and online
        Auth->>UP: RevokeToken (best effort)
    end
    Auth-)Layout: SessionEnded(Expired)
    Layout->>Mem: SessionEndedNoticeService.Set(reason), isEndingSession = true (ignore repeats)
    Layout->>Life: SigningOutAsync()
    Life->>SyncH: OnSigningOutAsync()
    SyncH->>Mem: Stop the scheduler (cancel passes, reset sync and connectivity status), lock every profile
    Layout->>Auth: LogoutAsync() (already signed out, harmless)
    Layout->>Life: SignedOutAsync()
    Life->>AwsH: OnSignedOutAsync()
    AwsH->>Mem: AWS credentials cleared
    Layout->>Login: Navigate to /login
    Login->>Auth: GetCurrentUserAsync() returns null
    Login->>DKS: Clear() (rememberme, e2e-kek, e2e-labels and any profile keys zeroed)
    Login->>Mem: SignInOffline = offlineModeService.IsOffline (offer offline again if still in offline mode)
    Login->>LS: GET myapp_saved_users
```

## 6. Offline mode sessions

Turning offline mode on (the switch in Settings, `OfflineSettingsSection`) swaps the online session for a 15 minute
offline one. Signing in offline from the login page does the same without an online session first (see
[prf-remember-me.md](prf-remember-me.md), diagram 3). The UI tests in `SyncTests` (`GivenSignedIn_WhenOfflineModeTurnedOnInSettings_...`,
`GivenOfflineMode_WhenTurnedOff_...`, `GivenOfflineMode_WhenPageReloaded_...`) cover these.

```mermaid
sequenceDiagram
    autonumber
    actor User
    participant Switch as OfflineModeSwitchViewModel
    participant Auth as CognitoAuthenticationService
    participant OSS as InMemoryOfflineSessionStore
    participant OMS as OfflineModeService
    participant Conn as ConnectivityService
    box rgb(240,240,255) In memory
        participant Mem as Memory (.NET fields)
    end
    box rgb(255,245,230) Browser storage
        participant LS as localStorage
    end
    box rgb(235,250,235) AWS
        participant UP as Cognito User Pool
    end

    rect rgb(245,245,245)
    Note over User,UP: Going offline
    User->>Switch: Turn offline mode on
    Switch->>Auth: GoOfflineAsync()
    Auth->>Auth: GetCurrentUserAsync() (must be signed in)
    Auth->>OSS: Start(username, displayName, email)
    OSS->>Mem: Session {ExpiresAt = now + 15 min, IsOffline = true}
    Auth->>Mem: token = refreshToken, refreshToken = null
    Auth->>LS: REMOVE myapp_cognito_session
    Auth->>UP: RevokeToken (while still online)
    Auth->>OMS: Enter(Chosen)
    OMS->>Mem: reason = Chosen
    OMS-)Conn: Changed(true)
    Conn->>Mem: status = {offline, Reason = OfflineMode}
    Note over Mem: AWS credentials are cleared on their next use and none are fetched
    end

    rect rgb(245,245,245)
    Note over User,UP: Offline session runs out
    Note over Auth: Session timer reads at ExpiresAt
    Auth->>OSS: Session expired
    Auth->>Auth: EndSessionAsync() (diagram 5)
    Note over OMS: Offline mode stays on, so the login page offers signing in offline again
    end

    rect rgb(245,245,245)
    Note over User,UP: Turning offline mode off
    User->>Switch: Turn offline mode off
    Switch->>OMS: Exit()
    OMS->>Mem: reason = null
    OMS-)Auth: Changed(false)
    Auth->>Auth: Offline session exists, so EndSessionAsync() (diagram 5)
    Note over Auth: There are no tokens to carry on with, so the user signs in online again
    end
```
