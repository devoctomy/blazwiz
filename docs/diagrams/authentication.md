# Authentication (Cognito) and AWS credentials

How a typed email and password becomes a signed-in session, how that session becomes temporary AWS credentials for
the data bucket in S3 (the user's files and their end to end keys), and how passwords are changed and reset.

Main classes:

- `LoginViewModel` (`src/Devoctomy.Blazwiz.UI/ViewModels/LoginViewModel.cs`)
- `CognitoAuthenticationService` (`src/Devoctomy.Blazwiz.Auth.Cognito/Services/CognitoAuthenticationService.cs`)
- `CognitoUserPoolClient`, which uses `Amazon.Extensions.CognitoAuthentication` for SRP (`src/Devoctomy.Blazwiz.Auth.Cognito/Services/CognitoUserPoolClient.cs`)
- `CognitoAwsCredentialsProvider` and `CognitoIdentityClient` (`src/Devoctomy.Blazwiz.Auth.Cognito/Services/`)
- `AwsCredentialsSessionHandler` (`src/Devoctomy.Blazwiz.Auth.Cognito/Services/AwsCredentialsSessionHandler.cs`): a session lifecycle handler that fetches and forgets the credentials
- `CognitoPasswordService` (`src/Devoctomy.Blazwiz.Auth.Cognito/Services/CognitoPasswordService.cs`), behind `IPasswordService` (Auth)
- `ChangePasswordViewModel` and `ForgotPasswordViewModel` (`src/Devoctomy.Blazwiz.UI/ViewModels/`)
- `MainLayoutViewModel` (`src/Devoctomy.Blazwiz.UI/ViewModels/MainLayoutViewModel.cs`) and `SessionLifecycle` (`src/Devoctomy.Blazwiz.Auth/Sessions/SessionLifecycle.cs`)

`AddBlazwizCognito` registers `CognitoAuthenticationService` and `CognitoPasswordService` when the region, user pool
and app client are configured (`AwsCognito` in `appsettings.json`). When they are not, and the app allows it
(`allowPlaceholderSignIn`, the template passes the host environment's `IsDevelopment()`), `LocalAuthenticationService`
and `LocalPasswordService` (Auth) take their place: any non-empty credentials sign in, an 8 hour session is stored
under `myapp_session` in localStorage, and nothing calls AWS. When the app does not allow it, `AddBlazwizCognito`
throws, so a deployment missing its settings fails to start.

Storage names below use the template's app name, `myapp`: the storage clients add the prefix set with
`AddBlazwizApp` (`IAppStorageKeys`), so the code names them `cognito_session`, `saved_users` and so on.

## Where things are kept

| What | Where | Key / field | Lifetime |
|---|---|---|---|
| Session (username, display name, email, `SignedInAt`, `ExpiresAt`, **ID token**, **access token**) | localStorage | `myapp_cognito_session` | Survives reloads. Removed on sign out or expiry |
| **Refresh token** | Memory | `CognitoAuthenticationService.refreshToken` | This tab only. Lost on reload or close |
| Pending new-password challenge (username + Cognito session id) | Memory | `CognitoAuthenticationService.pendingNewPassword`, `CognitoUserPoolClient.challengedUser` | Until answered or abandoned |
| Typed password | Memory | `LoginViewModel.Password` | Cleared by `CompleteLogin()` |
| Remembered users (email, plus passkey data, see [prf-remember-me.md](prf-remember-me.md)) | localStorage | `myapp_saved_users` | Until removed from the list |
| AWS temporary credentials (identity id, access key, secret, session token, expiration) | Memory | `CognitoAwsCredentialsProvider.cached` | Until 5 minutes before they expire, sign out, offline mode, or `Clear()` |
| S3 client built from those credentials | Memory | `CognitoS3TargetProvider.cached` (UI.Sync) | Replaced when the credentials object changes |

The refresh token is never written to storage. Anything that can run script in the origin can read localStorage, and a
stored refresh token would let it act as the user for the token's whole validity (12 hours, `RefreshTokenValidity` in
the base stack, `src/Devoctomy.Blazwiz.Aws/cloudformation/stack.yaml`). The ID and access tokens last 60 minutes.

## 1. Sign in with email and password (SRP)

```mermaid
sequenceDiagram
    autonumber
    actor User
    participant VM as LoginViewModel
    participant SIO as Sign-in options
    participant Auth as CognitoAuthenticationService
    participant UPC as CognitoUserPoolClient
    participant Life as SessionLifecycle
    box rgb(240,240,255) In memory
        participant Mem as Memory (.NET fields)
    end
    box rgb(255,245,230) Browser storage
        participant LS as localStorage
    end
    box rgb(235,250,235) AWS
        participant UP as Cognito User Pool
    end

    User->>VM: Enter email + password, click Sign in
    VM->>Mem: Password held in LoginViewModel.Password
    VM->>Mem: offlineModeService.Exit() (offline flag cleared)
    VM->>Auth: LoginAsync(email, password)
    Auth->>Mem: pendingNewPassword = null
    Auth->>UPC: SignInAsync(email, password)
    UPC->>UP: StartWithSrpAuthAsync, SRP-6a (the password never leaves the browser)
    UP-->>UPC: IdToken, AccessToken, RefreshToken, ExpiresIn

    alt Tokens returned
        UPC-->>Auth: CognitoSignInResult
        Auth->>Mem: refreshToken = result.RefreshToken
        Auth->>Auth: CreateSession(), reading the email and name claims from the ID token
        Auth->>LS: SET myapp_cognito_session = {username, displayName, email, signedInAt, expiresAt, idToken, accessToken}
        Auth-->>VM: LoginResult Success
        VM->>SIO: ApplyAsync() each (e.g. "Start with sync off" sets myapp_sync_disabled)
        VM->>Life: SignedInAsync() (e.g. sync asks for persistent storage)
        opt Remember me ticked
            VM->>LS: SET myapp_saved_users (adds the email)
            Note over VM: Offers passkey setup when AppSettings.EnablePortalLoginAuthn is on, see prf-remember-me.md
        end
        VM->>Mem: Password = "" (CompleteLogin)
        VM->>User: Navigate to /, the shell starts the session (session-management.md, diagram 1)
    else Wrong password, unconfirmed, throttled
        UP-->>UPC: NotAuthorizedException etc.
        UPC-->>Auth: CognitoException
        Auth-->>VM: LoginResult Failed, with an error code (AuthErrorCodes) described from resources
    else Network failure or timeout
        UPC-->>Auth: HttpRequestException / TaskCanceledException
        Auth-->>VM: LoginResult Failed, ServiceUnreachable = true
    end
```

## 2. New password challenge (invited users)

An invited user signs in with a temporary password and Cognito replies with `NEW_PASSWORD_REQUIRED` instead of tokens.

```mermaid
sequenceDiagram
    autonumber
    actor User
    participant VM as LoginViewModel
    participant Auth as CognitoAuthenticationService
    participant UPC as CognitoUserPoolClient
    box rgb(240,240,255) In memory
        participant Mem as Memory (.NET fields)
    end
    box rgb(255,245,230) Browser storage
        participant LS as localStorage
    end
    box rgb(235,250,235) AWS
        participant UP as Cognito User Pool
    end

    User->>VM: Sign in with temporary password
    VM->>Auth: LoginAsync(email, tempPassword)
    Auth->>UPC: SignInAsync
    UPC->>UP: SRP sign in
    UP-->>UPC: Challenge NEW_PASSWORD_REQUIRED + session id
    UPC->>Mem: challengedUser = CognitoUser
    UPC-->>Auth: ChallengeName, SessionId (no tokens)
    Auth->>Mem: pendingNewPassword = {username, sessionId}
    Auth-->>VM: NewPasswordRequired = true
    VM->>Mem: Password = "", remember the Remember me choice
    VM->>User: Show new password form

    User->>VM: New password + confirmation
    VM->>Auth: CompleteNewPasswordAsync(newPassword)
    Auth->>UPC: CompleteNewPasswordAsync(sessionId, newPassword)
    UPC->>UP: RespondToNewPasswordRequiredAsync
    alt Accepted
        UP-->>UPC: Tokens
        UPC->>Mem: challengedUser = null
        Auth->>Mem: pendingNewPassword = null, refreshToken = result.RefreshToken
        Auth->>LS: SET myapp_cognito_session
        Auth-->>VM: Success
        VM->>Mem: Password = NewPassword (the one to remember)
        Note over VM: Continues as in diagram 1 (sign-in options, Remember me, navigate)
    else Weak password (InvalidPasswordException)
        Auth-->>VM: Failed, challenge kept so the user can try again
    else Challenge expired (NotAuthorizedException)
        Auth->>Mem: pendingNewPassword = null
        Auth-->>VM: Failed, SignInExpired ("sign in again")
    end
```

## 3. Getting AWS credentials from the identity pool

When the shell starts a session it runs every session lifecycle handler. `AwsCredentialsSessionHandler` fetches the
credentials in the background, so the first call to AWS does not wait for them. After that, anything calling AWS
(sync's S3 client, which also reaches the end to end keys) asks `IAwsCredentialsProvider`, which returns the cached set
until it is within 5 minutes of expiry.

```mermaid
sequenceDiagram
    autonumber
    participant Life as SessionLifecycle
    participant H as AwsCredentialsSessionHandler
    participant Creds as CognitoAwsCredentialsProvider
    participant Auth as CognitoAuthenticationService
    participant IdC as CognitoIdentityClient
    box rgb(240,240,255) In memory
        participant Mem as Memory (.NET fields)
    end
    box rgb(255,245,230) Browser storage
        participant LS as localStorage
    end
    box rgb(235,250,235) AWS
        participant IP as Cognito Identity Pool
    end

    Life->>H: OnSessionStartedAsync(session)
    H-)Creds: GetCredentialsAsync() (not awaited, failures ignored)

    alt Placeholder auth, or no identity pool configured
        Creds-->>H: null
    else Offline mode on
        Creds->>Mem: cached = null
        Creds-->>H: null (no AWS calls in offline mode)
    else Online
        Creds->>Auth: GetCurrentUserAsync()
        Auth->>LS: GET myapp_cognito_session
        Note over Auth: May renew the tokens first, see session-management.md
        Auth-->>Creds: Session (ID token)
        Creds->>Creds: Wait on gate (concurrent callers share one fetch)
        Creds->>Mem: Read cached
        alt Cached for this user and more than 5 min from expiry
            Mem-->>Creds: AwsTemporaryCredentials
        else Missing, other user, or near expiry
            Creds->>Mem: cached = null
            Creds->>IdC: GetCredentialsAsync(idToken)
            IdC->>IP: GetId(IdentityPoolId, Logins {cognito-idp.{region}.amazonaws.com/{poolId}: idToken})
            IP-->>IdC: IdentityId
            IdC->>IP: GetCredentialsForIdentity(IdentityId, Logins)
            IP-->>IdC: AccessKeyId, SecretKey, SessionToken, Expiration
            IdC-->>Creds: AwsTemporaryCredentials
            Creds->>Mem: cached = {username, credentials}
        end
        Creds-->>H: Credentials
    end
```

The user pool and identity pool calls are public APIs, so their clients are built with `AnonymousAWSCredentials`. The
ID token is the only proof. The identity id names the user's prefix in the data bucket (`{identityId}/...`).

## 4. Using the credentials: the data bucket

`CognitoS3TargetProvider` is in UI.Sync, which joins sync to Cognito. Sync (`S3SyncRemoteStore`) and the end to end
keys (`S3UserKeysStore`, in Sync) both get the same target from it.

```mermaid
sequenceDiagram
    autonumber
    participant Sync as SyncService / ConnectivityService
    participant Keys as S3UserKeysStore
    participant Target as CognitoS3TargetProvider
    participant Creds as CognitoAwsCredentialsProvider
    box rgb(240,240,255) In memory
        participant Mem as Memory (.NET fields)
    end
    box rgb(235,250,235) AWS
        participant S3 as S3 data bucket
    end

    Sync->>Target: GetTargetAsync()
    Target->>Creds: GetCredentialsAsync()
    Creds-->>Target: Credentials (cached or fresh, diagram 3)
    alt No credentials (signed out, offline mode, refused)
        Target->>Mem: Dispose the S3 client
        Target-->>Sync: null (reported as offline, reason NoCredentials)
    else Same credentials object as last time
        Target->>Mem: Reuse cached S3SyncTarget
    else New credentials
        Target->>Mem: Dispose old client, cached = {credentials, new AmazonS3Client}
    end
    Target-->>Sync: S3SyncTarget(client, bucket, "{identityId}/")
    Sync->>S3: ListObjectsV2 / GetObject / PutObject / DeleteObject

    alt S3 answers 403
        S3-->>Sync: SyncAccessDeniedException
        Sync->>Target: Invalidate()
        Target->>Creds: Clear()
        Creds->>Mem: cached = null
        Target->>Mem: Dispose S3 client
        Note over Sync: SyncService runs the profile's pass once more with new credentials
    end

    Keys->>Target: GetTargetAsync()
    Target-->>Keys: S3SyncTarget (the same client)
    Keys->>S3: GetObject / PutObject (If-Match or If-None-Match: *) / DeleteObject "{identityId}/keys/{profile id}" and "{identityId}/keys/index"
    alt 412 Precondition Failed (changed on another device since it was read)
        S3-->>Keys: E2eDocumentChangedException
        Note over Keys: E2eProtectedProfileManager reads it again and reapplies its change (up to 3 attempts)
    end
```

## 5. Sign out

```mermaid
sequenceDiagram
    autonumber
    actor User
    participant Layout as MainLayoutViewModel
    participant Life as SessionLifecycle
    participant SyncH as SyncSessionHandler (UI.Sync)
    participant Auth as CognitoAuthenticationService
    participant AwsH as AwsCredentialsSessionHandler
    participant Login as LoginViewModel
    box rgb(240,240,255) In memory
        participant Mem as Memory (.NET fields)
    end
    box rgb(255,245,230) Browser storage
        participant LS as localStorage
    end
    box rgb(235,250,235) AWS
        participant UP as Cognito User Pool
    end

    User->>Layout: Sign out
    Layout->>Life: SigningOutAsync()
    Life->>SyncH: OnSigningOutAsync()
    SyncH->>Mem: Stop the folder monitor and the scheduler, lock every profile (keys zeroed)
    Layout->>Auth: LogoutAsync()
    Auth->>Mem: pendingNewPassword = null, offline session cleared
    Auth->>Mem: token = refreshToken, refreshToken = null
    Auth->>LS: REMOVE myapp_cognito_session
    opt Had a refresh token and not in offline mode
        Auth->>UP: RevokeToken(refreshToken) (best effort)
    end
    Layout->>Life: SignedOutAsync()
    Life->>AwsH: OnSignedOutAsync()
    AwsH->>Mem: credentialsProvider.Clear(), cached = null
    Life->>Mem: DerivedKeySessionHandler: DerivedKeyStore.Clear() (zeroes the PRF-derived keys and anything left)
    Layout->>Login: Navigate to /login
    Login->>Auth: GetCurrentUserAsync() returns null
    Login->>Mem: DerivedKeyStore.Clear() again (nobody signed in, so no keys)
    Login->>LS: GET myapp_saved_users (show remembered users)
    Note over LS: Files, sync index, event log and this device's key copies stay, partitioned by user
```

## 6. Changing the password (Profile page)

The form is `ChangePasswordForm` (`src/Devoctomy.Blazwiz.UI/Components/`) on the Profile page. The remembered password, if this user has one on this device,
would no longer sign in, so it is encrypted again with the new one, or forgotten if the passkey's key is not in memory.

```mermaid
sequenceDiagram
    autonumber
    actor User
    participant VM as ChangePasswordViewModel
    participant PS as CognitoPasswordService
    participant Auth as CognitoAuthenticationService
    participant UPC as CognitoUserPoolClient
    participant PCS as PasswordCryptoService
    box rgb(255,245,230) Browser storage
        participant LS as localStorage
    end
    box rgb(235,250,235) AWS
        participant UP as Cognito User Pool
    end

    User->>VM: Current password, new password, confirmation
    alt A field empty, or the confirmation differs
        VM->>User: PasswordsRequired / PasswordsDoNotMatch
    else Filled in and matching
        VM->>PS: ChangePasswordAsync(current, new)
        Note over PS: Offline mode: refused (PasswordChangeOffline), nothing called
        PS->>Auth: GetCurrentUserAsync() (renews the tokens if they are about to expire)
        PS->>UPC: ChangePasswordAsync(username, tokens, current, new)
        UPC->>UP: ChangePassword (with the access token)
        alt Changed
            VM->>LS: GET myapp_saved_users (this user's passkey credential)
            opt The user has a remembered password
                VM->>PCS: EncryptAsync(new)
                alt "rememberme" key in memory (signed in with the passkey)
                    PCS-->>VM: New encrypted password
                else No key (signed in with a typed password)
                    VM->>VM: EncryptedPassword = null (the passkey must be set up again)
                end
                VM->>LS: SET myapp_saved_users
            end
            VM->>User: "Password changed" (or changed, and the passkey sign in forgotten)
        else Wrong current password, weak new one, throttled, unreachable
            PS-->>VM: BlazwizException with an AuthErrorCodes code
            VM->>User: The code, described from resources
        end
    end
```

## 7. Resetting a forgotten password (`/forgot-password`)

Linked from the sign-in page (`ForgotPasswordLink`). Cognito emails a code, and the code sets a new password. Neither
step says whether the address has an account.

```mermaid
sequenceDiagram
    autonumber
    actor User
    participant VM as ForgotPasswordViewModel
    participant PS as CognitoPasswordService
    participant UPC as CognitoUserPoolClient
    box rgb(235,250,235) AWS
        participant UP as Cognito User Pool
    end

    User->>VM: Email, "Send code"
    VM->>PS: RequestPasswordResetAsync(email)
    PS->>UPC: ForgotPasswordAsync(email)
    UPC->>UP: ForgotPassword
    alt Sent, or no such user (UserNotFoundException is swallowed)
        UP-->>User: Email with a code (only if the account exists)
        VM->>User: Step Reset: code, new password, confirmation
    else Throttled, unreachable, offline mode
        VM->>User: The error, described from resources
    end

    User->>VM: Code + new password
    VM->>PS: ResetPasswordAsync(email, code, newPassword)
    PS->>UPC: ConfirmForgotPasswordAsync(email, code, newPassword)
    UPC->>UP: ConfirmForgotPassword
    alt Reset
        VM->>User: Step Done, "Back to sign in" (/login)
    else Wrong or expired code, no such user (reported as a wrong code), weak password
        VM->>User: CodeMismatch / CodeExpired / InvalidPassword
    end
    Note over VM: A passkey sign in remembered on this device still holds the old password, so it fails and asks for the password
```
