# Passkey (WebAuthn PRF) "remember me"

A remembered user signs in with one touch: their passkey's PRF output derives a key that decrypts the password saved
for them on this device, and that password signs in to Cognito as usual (see [authentication.md](authentication.md)).
The same PRF output derives the key that unlocks end to end encryption on this device.

Main classes:

- `LoginViewModel` (`blazwiz.Portal.Application/ViewModels/LoginViewModel.cs`)
- `WebAuthnService` and `webauthn-bridge.js` (`blazwiz.Portal/Services/WebAuthnService.cs`, `blazwiz.Portal/wwwroot/js/webauthn-bridge.js`), using SimpleWebAuthn
- `PrfKeyDerivator` (`blazwiz.Core/Cryptography/PrfKeyDerivator.cs`): HKDF
- `InMemoryDerivedKeyStore` (`blazwiz.Core/Cryptography/InMemoryDerivedKeyStore.cs`)
- `PasswordCryptoService` (`blazwiz.Portal.Application/Services/PasswordCryptoService.cs`): AES-GCM
- `PasskeyUnlockService` (`blazwiz.Portal.Application/Services/PasskeyUnlockService.cs`)

## Where things are kept

| What | Where | Key / field | Lifetime |
|---|---|---|---|
| PRF output (32 bytes from the authenticator) | Memory, briefly | `WebAuthnService.DeriveKeys` local | Zeroed straight after the keys are derived |
| `rememberme` key (HKDF of the PRF output) | Memory | `InMemoryDerivedKeyStore["rememberme"]` | Until `Clear()`: the login page with nobody signed in, a failed passkey sign in, or a reload |
| `e2e-kek` key (HKDF of the PRF output) | Memory | `InMemoryDerivedKeyStore["e2e-kek"]` | Same as above |
| Saved user: email, credential id, user id, created at, **encrypted password** | localStorage | `blazwiz_saved_users` | Until the user is removed from the list |
| Decrypted password | Memory, briefly | `AttemptWebAuthnLoginAsync` local | Passed to `LoginAsync`, then dropped |
| Passkey private key | Authenticator (platform or roaming) | n/a | Never leaves the authenticator |

The keys are derived as `HKDF(ikm = PRF output, salt = AppIdentity.Id, info = "blazwiz:{key name}")`. The PRF input is
the fixed salt `"WebAuthn PRF Registration Salt"`, so the same passkey always gives the same keys. Changing
`AppIdentity` changes every key, and anything encrypted with the old ones can no longer be decrypted.

There is no relying party server: the WebAuthn challenge is random and generated in the browser, so the assertion
itself proves nothing to anyone. What proves the user is that the PRF output (only available after the authenticator
checks their fingerprint, face or PIN) decrypts the saved password, and that Cognito accepts that password.

## 1. Setting up a passkey after a password sign in

Shown when "Remember me" was ticked and `AppSettings.EnablePortalLoginAuthn` is on.

```mermaid
sequenceDiagram
    autonumber
    actor User
    participant VM as LoginViewModel
    participant WA as WebAuthnService
    participant JS as webauthn-bridge.js
    participant Authn as Authenticator
    participant KDF as PrfKeyDerivator
    participant PCS as PasswordCryptoService
    box rgb(240,240,255) In memory
        participant Mem as Memory (.NET fields)
        participant DKS as DerivedKeyStore (memory)
    end
    box rgb(255,245,230) Browser storage
        participant LS as localStorage
    end

    Note over VM,Mem: Password sign in succeeded (authentication.md, diagram 1). Password still in LoginViewModel.Password
    VM->>LS: SET blazwiz_saved_users (email only, no passkey yet)
    VM->>User: "Use a passkey next time?"
    User->>VM: Set up
    VM->>WA: RegisterAsync(email)
    WA->>JS: webAuthnWrapper.register(email)
    JS->>Authn: navigator.credentials.create (rp = hostname, extensions prf {})
    Authn->>User: Fingerprint / face / PIN
    Authn-->>JS: New credential, prf.enabled
    opt PRF supported
        JS->>Authn: navigator.credentials.get (allowCredentials = new id, prf.eval.first = "WebAuthn PRF Registration Salt")
        Authn->>User: Verify again
        Authn-->>JS: prf.results.first (32 bytes)
    end
    JS-->>WA: {credentialID, userID, prfEnabled, prfOutput (hex)}
    WA->>Mem: prfOutput bytes
    WA->>KDF: DeriveKeys(prfOutput)
    loop For each configured key: "rememberme", "e2e-kek"
        KDF->>KDF: HKDF(prf, salt = app id, info = "blazwiz:{name}")
        KDF->>DKS: SetKey(name, key) (stores a copy)
        KDF->>KDF: Zero the local key
    end
    WA->>Mem: Zero prfOutput
    WA-->>VM: Success, PrfEnabled, KeysDerived

    alt PRF enabled and keys derived
        VM->>PCS: EncryptAsync(Password)
        PCS->>DKS: TryGetKey("rememberme")
        DKS-->>PCS: Key copy
        PCS->>PCS: AES-GCM encrypt, zero key copy
        PCS-->>VM: base64 ciphertext
        VM->>LS: SET blazwiz_saved_users ({email, webAuthnCredential {credentialID, userID, createdAt, encryptedPassword}})
        VM->>Mem: Password = "" (CompleteLogin)
        VM->>User: Navigate to /
    else No PRF support
        VM->>User: "Your authenticator doesn't support the required security features"
        Note over VM: User continues without a passkey (email stays remembered)
    end
```

## 2. One-touch sign in with a remembered passkey

```mermaid
sequenceDiagram
    autonumber
    actor User
    participant VM as LoginViewModel
    participant WA as WebAuthnService
    participant JS as webauthn-bridge.js
    participant Authn as Authenticator
    participant PCS as PasswordCryptoService
    participant Auth as CognitoAuthenticationService
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

    User->>VM: Open /login
    VM->>Auth: GetCurrentUserAsync() returns null
    VM->>DKS: Clear() (nobody signed in, so no keys)
    VM->>LS: GET blazwiz_saved_users
    LS-->>VM: Users with credential ids + encrypted passwords
    VM->>User: Show remembered users

    User->>VM: Tap their user
    VM->>WA: AuthenticateAsync(credentialID)
    WA->>JS: webAuthnWrapper.authenticate(credentialID)
    JS->>Authn: navigator.credentials.get (prf.eval.first = "WebAuthn PRF Registration Salt")
    Authn->>User: Fingerprint / face / PIN
    Authn-->>JS: Assertion + prf.results.first
    JS-->>WA: {success, userVerified, prfOutput (hex)}
    WA->>DKS: DeriveKeys: SetKey("rememberme"), SetKey("e2e-kek")
    WA->>Mem: Zero prfOutput
    WA-->>VM: Success, KeysDerived

    VM->>PCS: DecryptAsync(encryptedPassword)
    PCS->>DKS: TryGetKey("rememberme")
    PCS-->>VM: Password (AES-GCM tag checks out, so it is the right passkey)
    VM->>Mem: Password held in a local variable

    alt "Sign in offline" ticked
        Note over VM: Diagram 3
    else Online
        VM->>Mem: offlineModeService.Exit()
        VM->>Auth: LoginAsync(email, password)
        Auth->>UP: SRP sign in (authentication.md, diagram 1)
        alt Signed in
            UP-->>Auth: Tokens
            Auth->>Mem: refreshToken
            Auth->>LS: SET blazwiz_cognito_session
            VM->>User: Navigate to / (keys stay in DerivedKeyStore for E2E)
        else Sign-in service unreachable
            Auth-->>VM: ServiceUnreachable
            Note over VM: Falls back to an offline sign in (diagram 3, reason SignInUnreachable)
        else Refused (e.g. password changed elsewhere)
            Auth-->>VM: Failed
            VM->>DKS: Clear() (keys must not outlive a failed sign in)
            VM->>User: "Biometric login failed. Please enter your password manually."
        end
    end
```

## 3. Offline sign in with a remembered passkey

Only possible with a passkey: decrypting the saved password proves the user on this device, and nothing calls Cognito
or AWS. The session is memory only, lasts 15 minutes, and has no tokens.

```mermaid
sequenceDiagram
    autonumber
    participant VM as LoginViewModel
    participant Auth as CognitoAuthenticationService
    participant OSS as InMemoryOfflineSessionStore
    participant OMS as OfflineModeService
    box rgb(240,240,255) In memory
        participant Mem as Memory (.NET fields)
        participant DKS as DerivedKeyStore (memory)
    end
    box rgb(255,245,230) Browser storage
        participant LS as localStorage
    end

    Note over VM,DKS: Passkey checked and password decrypted (diagram 2)
    VM->>Auth: SignInOfflineAsync(email, Chosen or SignInUnreachable)
    Auth->>Mem: pendingNewPassword = null, refreshToken = null
    Auth->>LS: REMOVE blazwiz_cognito_session
    Auth->>OSS: Start(email, displayName, email)
    OSS->>Mem: Session = {..., ExpiresAt = now + 15 min, IsOffline = true, no tokens}
    Auth->>Auth: Listen for offline mode changes
    Auth->>OMS: Enter(reason)
    OMS->>Mem: reason = Chosen / SignInUnreachable
    OMS-->>OMS: Changed(true) to ConnectivityService, SessionTimer, ...
    VM->>LS: SET blazwiz_sync_disabled (only if "start with sync off" changed)
    VM->>VM: Navigate to /
    Note over DKS: rememberme and e2e-kek stay in memory, so E2E can unlock from this device's cached keys
```

## 4. What the PRF keys unlock after sign in: end to end encryption

On startup the shell initialises E2E. The `e2e-kek` derived from the passkey unwraps this device's copy of the
master key. If the page was reloaded the derived keys are gone, so the banner offers "Unlock with passkey".

```mermaid
sequenceDiagram
    autonumber
    actor User
    participant Banner as E2eBannerViewModel
    participant PUS as PasskeyUnlockService
    participant WA as WebAuthnService
    participant KM as E2eKeyManager
    participant Ring as E2eKeyRing
    participant Keys as SecretsManagerUserKeysStore
    box rgb(240,240,255) In memory
        participant DKS as DerivedKeyStore (memory)
    end
    box rgb(255,245,230) Browser storage
        participant LS as localStorage
    end
    box rgb(235,250,235) AWS
        participant SM as Secrets Manager
    end

    KM->>Keys: GetAsync()
    Keys->>SM: GetSecretValue "{sub}/keys"
    SM-->>Keys: keys.json (master key wrapped by passphrase, file keys wrapped by master key)
    KM->>LS: GET blazwiz_e2e_keys_account_{user}
    LS-->>KM: {wrappedMasterKey (wrapped by e2e-kek), cachedDocument}
    KM->>DKS: TryGetKey("e2e-kek")

    alt Key present but this device has no keys (or they were wrapped by another passkey)
        DKS-->>KM: kek copy
        KM->>Ring: Clear(Refuse)
        Note over KM: Status NeedsSetup: the user enters the passphrase in Settings, which unwraps the master key and saves blazwiz_e2e_keys_account_{user}
    else Key present and this device is set up
        DKS-->>KM: kek copy
        KM->>KM: Unwrap master key with kek, unwrap each file key with master key
        KM->>Ring: Load(masterKey, fileKeys, activeKeyId)
        Ring->>DKS: SetKey("e2e:account:master"), SetKey("e2e:account:file:{id}") for each
        KM->>KM: Zero kek, master key and file key copies
        opt keys.json changed since last time
            KM->>LS: SET blazwiz_e2e_keys_account_{user} (new cachedDocument)
        end
        Note over KM: Status Unlocked, writes are encrypted
    else Key missing (e.g. after a reload, or signed in with a typed password)
        KM->>Ring: Clear(Refuse) (nothing may be written in plaintext)
        Note over KM: Status Blocked
        Banner->>PUS: CanUnlockAsync()
        PUS->>LS: GET blazwiz_saved_users (credential id for this user)
        Banner->>User: "Unlock with passkey"
        User->>Banner: Unlock
        Banner->>PUS: UnlockAsync()
        PUS->>WA: AuthenticateAsync(credentialID)
        WA->>DKS: DeriveKeys: SetKey("rememberme"), SetKey("e2e-kek")
        Banner->>KM: InitializeAsync() again, now Unlocked
        Banner->>Banner: syncScheduler.SyncNowAsync()
    end
```

## 5. Removing a remembered user

```mermaid
sequenceDiagram
    autonumber
    actor User
    participant VM as LoginViewModel
    box rgb(240,240,255) In memory
        participant Mem as Memory (.NET fields)
    end
    box rgb(255,245,230) Browser storage
        participant LS as localStorage
    end

    User->>VM: Remove user from the list
    VM->>Mem: savedUsers.Remove(user)
    VM->>LS: SET blazwiz_saved_users (without them, so their encrypted password is gone)
    Note over LS: The passkey itself stays on the authenticator until the user deletes it there
```
