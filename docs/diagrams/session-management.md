# Session management and expiry

How long a session lasts, when the tokens are renewed, what ends a session, and what a reload keeps and loses.

Main classes:

- `CognitoAuthenticationService.GetCurrentUserAsync` (`blazwiz.Portal.Application/Services/CognitoAuthenticationService.cs`): the one place sessions are checked, renewed and ended
- `SessionTimerViewModel` (`blazwiz.Portal.Application/ViewModels/SessionTimerViewModel.cs`): the countdown under the user's name, and the clock that drives renewal
- `MainLayoutViewModel` (`.../ViewModels/MainLayoutViewModel.cs`): reacts to `SessionEnded`
- `OfflineModeService` (`blazwiz.Core/Offline/OfflineModeService.cs`) and `InMemoryOfflineSessionStore` (`.../Services/InMemoryOfflineSessionStore.cs`)

## Lifetimes

| Thing | Lifetime | Set by |
|---|---|---|
| ID and access tokens (`UserSession.ExpiresAt`) | 60 minutes | `IdTokenValidity` / `AccessTokenValidity` in `cloudformation/stack.yaml` |
| Renewal window | Last 5 minutes before `ExpiresAt` | `CognitoAuthenticationService.RefreshMargin` |
| Refresh token | 12 hours, and only while this tab stays open | `RefreshTokenValidity`, kept in memory only |
| AWS credentials | As issued by the identity pool (about 1 hour), refetched 5 minutes before expiry | `CognitoAwsCredentialsProvider.RefreshMargin` |
| Offline session | 15 minutes from going offline, no renewal | `OfflineModeService.SessionLength` |
| Placeholder session (local development) | 8 hours | `LocalAuthenticationService.SessionDuration` |

A session is only ever renewed **before** it expires. A token that expired while nobody was using the tab is not
brought back to life, even if the refresh token is still valid.

## What survives a reload

| Kept (localStorage, IndexedDB) | Lost (memory) |
|---|---|
| `blazwiz_cognito_session`: the session with ID and access tokens | The refresh token, so the session can no longer be renewed |
| `blazwiz_saved_users`: remembered users and encrypted passwords | The offline session and offline mode, so a reload always leaves offline mode |
| `blazwiz_e2e_keys_{user}`: this device's wrapped master key | Every PRF-derived and E2E key in `DerivedKeyStore` |
| `blazwiz_sync_disabled`, theme, user settings | AWS credentials and the S3 client |
| Notes, sync index, sync event log | Scheduler, connectivity and sync status |

So after a reload the user is still signed in until the ID token reaches its renewal window, then is sent to the
login page (one touch with a passkey). E2E stays locked until they use their passkey again.

## 1. How `GetCurrentUserAsync` decides

Everything that needs the user (the shell, the session timer, sync's user context, the AWS credentials provider)
goes through this method, so this is where sessions are renewed and ended.

```mermaid
sequenceDiagram
    autonumber
    participant Caller as Any caller
    participant Auth as CognitoAuthenticationService
    participant Clock as ISystemClock
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
        Auth->>Clock: UtcNow
        alt Past ExpiresAt (15 min)
            Auth->>Auth: EndSessionAsync() (diagram 4)
            Auth-->>Caller: null
        else Still valid
            Auth-->>Caller: Offline session (no tokens)
        end
    else No offline session
        Auth->>OMS: IsOffline?
        alt Offline mode on (e.g. its session already ended)
            Auth-->>Caller: null (nothing may call Cognito)
        else Online
            Auth->>LS: GET blazwiz_cognito_session
            alt Nothing stored
                Auth-->>Caller: null
            else No ID token
                Auth->>LS: REMOVE blazwiz_cognito_session
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
                Auth->>UP: InitiateAuth REFRESH_TOKEN_AUTH
                alt New tokens
                    UP-->>Auth: IdToken, AccessToken, ExpiresIn
                    Auth->>Mem: refreshToken (replaced only if Cognito returns a new one)
                    Auth->>LS: SET blazwiz_cognito_session {same SignedInAt, new tokens, new ExpiresAt}
                    Auth-->>Caller: Renewed session
                else Refresh token expired or revoked (CognitoException)
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

## 2. The session timer drives renewal

The countdown ticks every second and re-reads the session every 30 seconds, but only while reading has no side
effects. In the renewal window it stops reading until the token has expired, so the renewal happens when something
else (a sync pass, a credentials fetch, a page) next asks for the user, or the session ends at expiry.

```mermaid
sequenceDiagram
    autonumber
    participant Timer as ITimer (1 s)
    participant VM as SessionTimerViewModel
    participant Auth as CognitoAuthenticationService
    box rgb(240,240,255) In memory
        participant Mem as Memory (.NET fields)
    end
    box rgb(255,245,230) Browser storage
        participant LS as localStorage
    end

    VM->>Auth: GetCurrentUserAsync() (on init)
    Auth->>LS: GET blazwiz_cognito_session
    VM->>Mem: expiresAt, isOffline, lastReadAt, nextReadAt = now + 30 s
    loop Every second
        Timer->>VM: TickAsync()
        alt Before the renewal window and 30 s since the last read
            VM->>Auth: GetCurrentUserAsync() (picks up tokens renewed elsewhere)
            VM->>Mem: expiresAt, nextReadAt
        else In the renewal window (last 5 min)
            VM->>VM: No read, just count down
        else Just passed expiresAt (first time)
            VM->>Auth: GetCurrentUserAsync()
            Auth->>Auth: Expired, EndSessionAsync() (diagram 4)
            Auth-->>VM: null
            VM->>Mem: expiresAt = null
        else Nothing to read
            VM->>VM: UpdateText()
        end
        VM->>VM: Text = "Session mm:ss" / "Offline mm:ss" / "Session expired"
    end
    Note over VM: Offline mode turning on or off triggers an immediate read too
```

## 3. Renewal while the app is in use

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
    Auth->>LS: GET blazwiz_cognito_session (ExpiresAt in 3 min)
    Auth->>Mem: refreshToken
    Auth->>UP: REFRESH_TOKEN_AUTH
    UP-->>Auth: New ID + access tokens (60 min)
    Auth->>LS: SET blazwiz_cognito_session (new ExpiresAt)
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
    Note over Mem: This repeats every hour until the refresh token's 12 hours run out, then diagram 4
```

## 4. A session ends

`EndSessionAsync` signs out and raises `SessionEnded`. `LogoutAsync` on its own (the Sign out button) does not raise it.

```mermaid
sequenceDiagram
    autonumber
    participant Auth as CognitoAuthenticationService
    participant Layout as MainLayoutViewModel
    participant Sched as SyncScheduler
    participant Creds as CognitoAwsCredentialsProvider
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
    Auth->>LS: REMOVE blazwiz_cognito_session
    opt Had a refresh token and online
        Auth->>UP: RevokeToken (best effort)
    end
    Auth-)Layout: SessionEnded
    Layout->>Mem: isEndingSession = true (ignore repeats)
    Layout->>Sched: StopAsync()
    Sched->>Mem: Cancel passes, reset sync and connectivity status
    Layout->>Auth: LogoutAsync() (already signed out, harmless)
    Layout->>Creds: Clear()
    Creds->>Mem: cached = null
    Layout->>Login: Navigate to /login
    Login->>Auth: GetCurrentUserAsync() returns null
    Login->>DKS: Clear() (rememberme, e2e-kek, master and file keys zeroed)
    Login->>Mem: SignInOffline = offlineModeService.IsOffline (offer offline again if still in offline mode)
    Login->>LS: GET blazwiz_saved_users
```

## 5. Offline mode sessions

Turning offline mode on (the switch in the shell) swaps the online session for a 15 minute offline one. Signing in
offline from the login page does the same without an online session first (see
[prf-remember-me.md](prf-remember-me.md), diagram 3).

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
    Auth->>LS: REMOVE blazwiz_cognito_session
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
    Auth->>Auth: EndSessionAsync() (diagram 4)
    Note over OMS: Offline mode stays on, so the login page offers signing in offline again
    end

    rect rgb(245,245,245)
    Note over User,UP: Turning offline mode off
    User->>Switch: Turn offline mode off
    Switch->>OMS: Exit()
    OMS->>Mem: reason = null
    OMS-)Auth: Changed(false)
    Auth->>Auth: Offline session exists, so EndSessionAsync() (diagram 4)
    Note over Auth: There are no tokens to carry on with, so the user signs in online again
    end
```
