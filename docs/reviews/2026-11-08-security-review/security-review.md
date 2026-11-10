---
title: "Security review 01: blazwiz"
type: security-review
number: 1
date: 2026-11-08
updated: 2026-11-09
reviewer: Claude Fable 5.1
model: claude-fable-5-1
tool: Claude Code
branch: security-review
commit: 2e63f07
scope:
  - the packages (src/Devoctomy.Blazwiz.*)
  - the template app (template/)
  - the AWS stack and its scripts (src/Devoctomy.Blazwiz.Aws/cloudformation, template/cloudformation)
  - the build, CI and dependencies
method: static review of the source; no test suite, browser or deployed stack was exercised for the findings
findings:
  critical: 0
  high: 0
  medium: 8
  low: 26
  informational: 15
status:
  fixed: 39
  fixed-in-part: 3
  open: 4
  not-applicable: 3
fixes-applied: 2026-11-08
fixes-merged: 963afec
stack-deployed: true
backlog:
  - ../../plans/active/2026-11-10-key-rotation-plan.md (moved from backlog/passphrase-change-key-rotation.md)
  - backlog/mfa-totp.md
  - backlog/deployer-permissions-boundary.md
  - backlog/csp-in-build.md
---

# Security review 01: blazwiz

**Branch:** `security-review` at `2e63f07` (clean working tree, no changes from `main`)
**Question:** what can go wrong, for users and for the project, in the framework, the template app and the AWS stack
as they stand today?

**How this review was done:** by reading the source. The cryptography, key management, authentication and session
code (`Cryptography`, `Encryption`, `Auth`, `Auth.Cognito`, `WebAuthnService`) was read in full by the reviewer. Four
parallel read-only passes covered the sync engine and its stores, the UI and template, the AWS stack and scripts, and
the build, CI and dependency surface; every finding they reported that is in this document was then checked against
the code by the reviewer, and the few that did not hold were dropped or narrowed (noted where relevant). Nothing in
the repository was modified. No git command that changes anything, no AWS command and no deploy script was run.
Commands run, all read-only: `dotnet restore blazwiz.slnx`, `dotnet list blazwiz.slnx package --vulnerable
--include-transitive`, `dotnet list blazwiz.slnx package --deprecated`, a scratch console program against
`Microsoft.Extensions.FileSystemGlobbing` 10.0.12 to see how the glob matcher treats `..` segments, and a download of
the published `@simplewebauthn/browser@14.0.0` npm tarball into the scratchpad to hash it against the vendored copy
(never executed). The test suites were **not** run for this review; no dynamic testing against a browser or a
deployed stack was done, so anything marked "Needs testing" is reasoning from the code only.

**Fixes applied on 2026-11-08 (same day, after the review):** every finding whose fix needs no functional change was
fixed in the working tree, as the "Fixed" column of the summary table says; see "What was fixed" before the table for
how that was checked and what could not be. The findings themselves are left as they were found, so the report still
describes the code at `2e63f07`.

**Severity scale.** *Critical:* exploitable now by an unauthenticated party, or one user reaching another user's data.
*High:* a signed-in user or a plausible attacker (XSS, stolen device, hijacked session) can read keys or plaintext they
should not, with little effort. *Medium:* a stated security control does not hold, or a realistic attacker gains a
meaningful advantage (destroys data, keeps access they should have lost, bypasses a factor). *Low:* defence in depth,
hardening, or a weakness that needs an attacker who already has most of what they want. *Informational:* worth
knowing, documenting or tidying; not a weakness on its own.

## Answer

**No Critical or High findings.** The security-critical design is sound and implemented with care: the password never
leaves the browser (SRP through AWS's own library), each user's AWS access is scoped to their own S3 prefix and their
own secrets by IAM with no path from one user to another's data, files are encrypted with AES-256-GCM under per-profile
keys that are bound to the profile and the path, the passphrase key is Argon2id (512 MiB, 2 passes), every raw key is
zeroed after use and on lock, the refresh token never touches storage, Secrets Manager keys are created with
never-overwrite semantics, and there are no vulnerable or hand-rolled crypto dependencies.

What was found is eight **Medium** items and a longer tail of Low and Informational ones. The Medium items are:

1. The passkey ceremonies never require or check user verification, and the flags that would report it are always
   false, so a security key without a PIN (or a platform where verification is skipped) unlocks the remembered password,
   offline sign in and device unlock of profiles with possession alone (SR1).
2. The idle auto-lock, documented as locking every profile after 15 minutes, never starts for profiles defined in code,
   which is every app's main profile (SR2).
3. Changing a profile's passphrase keeps the same master and file keys, so anyone who captured the old keys document
   and old passphrase keeps access to every current and future file (SR3).
4. The data bucket has no versioning and keys secrets are force-deleted, so a one-hour credential leak or a bug can
   destroy a user's files and keys with no way back (SR4, SR5).
5. Password-only sign in, with MFA and threat protection off (SR6, already in the backlog).
6. On the project side, the release workflow's actions are pinned by tag, and the template test script can delete a
   folder it did not create (SR7, SR8).

None of these needs a redesign. SR1, SR2, SR5 and SR7 are small changes; SR3 and SR6 need functional work and are
best treated as features; SR4 is one stanza of CloudFormation.

## Threat model, as the code and docs define it

| Asset | Where it lives | Who can reach it | Design stance |
|---|---|---|---|
| Plaintext files | Memory, only while a profile is unlocked | The user, through the app | Never stored or sent in plaintext |
| File keys, master key | `IDerivedKeyStore` (memory) while unlocked | The app | Zeroed on lock, sign out, reload |
| Keys document (master key wrapped by Argon2id(passphrase), file keys wrapped by master key) | Secrets Manager `{sub}/keys/{profile}`; copy in localStorage `myapp_e2e_keys_*` | The user's AWS session; anything running in the origin | Useless without the passphrase; offline guessing is bounded by Argon2id |
| Master key wrapped by the passkey key (`e2e-kek`) | localStorage (device unlock on) | Anything in the origin, plus the PRF output | PRF needs the authenticator |
| Remembered password, encrypted with `rememberme` | localStorage `myapp_saved_users` | Anything in the origin, plus the PRF output | Same |
| ID and access tokens (60 min) | localStorage `myapp_cognito_session` | Anything in the origin | **Accepted risk**, documented in `authentication.md`: an XSS gets the tokens, hence AWS credentials, hence ciphertext and the ability to delete |
| Refresh token (12 h) | Memory, this tab only | The tab | Never stored |
| AWS temporary credentials | Memory | The tab | Cleared on sign out and offline mode |

Trust boundaries: the browser origin (XSS is the dominant threat, mitigated by a CSP the deploy script injects), the
AWS account (operators can delete or roll back but not read: encryption is the control), other users (IAM is the
control), and the supply chain (NuGet, the vendored JavaScript, GitHub Actions). The reviews below are organised along
those lines. Users are invited by an administrator; there is no self sign-up.

## Findings

Each finding states where it is, what the code does, what could result from it, what to do, **whether the fix needs a
functional change** (behaviour a user or an app developer would notice, as opposed to hardening with no visible
effect), and how confident the finding is. Line numbers refer to the working tree at `2e63f07`.

### Authentication, sessions and passkeys

#### SR1 (Medium). Passkey ceremonies do not require user verification, and the flags that report it are always false

- **Where:** `src/Devoctomy.Blazwiz.Browser/wwwroot/js/webauthn-bridge.js:109` (registration,
  `authenticatorSelection.userVerification: 'preferred'`), `:135` and `:204` (both assertions, `userVerification:
  'preferred'`), `:223-235` (result flags); `src/Devoctomy.Blazwiz.Browser/Services/WebAuthnService.cs:38-46`;
  `src/Devoctomy.Blazwiz.UI/ViewModels/LoginViewModel.cs:604-614`; `src/Devoctomy.Blazwiz.UI/Services/PasskeyKeysService.cs:52-56`.
- **What the code does:** the bridge asks for `preferred` verification everywhere, then reads
  `asseResp.response?.authenticatorDataFlags || {}`. The vendored `@simplewebauthn/browser` 14.0.0 bundle never sets
  `authenticatorDataFlags` (zero occurrences in the file), so `userPresent` and `userVerified` are always `false`.
  Nothing in C# reads them: `WebAuthnService` derives the keys whenever `Success` is true and a PRF output came back,
  and `LoginViewModel` decrypts the remembered password and, when "Sign in offline" is ticked, signs in offline on that
  alone. The code's own comment at `LoginViewModel.cs:606-608` says decrypting the password "proves the passkey (and the
  user's fingerprint or PIN)"; `docs/diagrams/prf-remember-me.md:40-42` says the same.
- **What could result:** with `preferred`, a roaming security key without a PIN, or a platform that satisfies
  `preferred` with presence only, completes the ceremony with a touch. The PRF output then derives `rememberme`,
  `e2e-kek` and `e2e-labels`, so **possession of the device or key alone** decrypts the saved Cognito password (an
  online sign in as the user, or an offline session with the cached data), and unlocks every profile with device unlock
  turned on. A later attempt to fix this by checking `UserVerified` in C# would lock everyone out, because the flag is
  always false. (Note: CTAP2's hmac-secret uses different secrets with and without verification, so a key registered
  with verification and later used without it would fail to decrypt. That only helps when the authenticator can verify
  at all.)
- **Recommendation:** set `userVerification: 'required'` in all three option objects. Derive the flags from
  `asseResp.response.authenticatorData` (base64url, flags byte at offset 32: UP is `0x01`, UV is `0x04`) and return
  `prfOutput` only when UV is set. In `WebAuthnService.AuthenticateAsync` and `RegisterAsync`, make `KeysDerived`
  depend on `UserVerified`. Add a unit test with a fake JavaScript result where `userVerified` is false and assert no
  keys are derived. Update the two docs.
- **Functional change needed:** small, yes. Authenticators that cannot verify the user stop being offered for
  "remember me" (they get the existing "doesn't support the required security features" message). That is the
  behaviour the docs already promise.
- **Confidence:** the always-false flags and the missing check are confirmed by reading. The practical bypass depends
  on the authenticator (Likely).

#### SR9 (Low). PRF-derived keys are only wiped when the framework's login page renders

- **Where:** `src/Devoctomy.Blazwiz.UI/ViewModels/LoginViewModel.cs:267` and `:664` are the only calls to
  `IDerivedKeyStore.Clear()` in `src`; `src/Devoctomy.Blazwiz.UI/ViewModels/MainLayoutViewModel.cs:273-295` (sign out),
  `src/Devoctomy.Blazwiz.UI.Sync/Services/SyncSessionHandler.cs:91-99`.
- **What the code does:** signing out locks every profile (`LockAll`, which removes the per-profile keys) but leaves
  `rememberme`, `e2e-kek` and `e2e-labels` in the store until the login page's `OnInitialized` clears them.
  `Program.cs` invites apps to replace the login page (`ReplacePage<MyLogin>(BlazwizPages.Login)`).
- **What could result:** an app with its own login page never clears the passkey-derived keys after sign out; they stay
  in WebAssembly memory for the tab's lifetime, where the next sign in on the same tab (or anything with code execution
  in the page) finds `PasskeyKeysService.EnsureKeyAsync` already satisfied and can unwrap device-unlock master keys and
  the saved password without a new passkey prompt.
- **Recommendation:** clear the store in the sign-out path itself, for example an Auth-level `ISessionLifecycleHandler`
  whose `OnSignedOutAsync` calls `Clear()`, and when `SessionEnded` fires. Keep the login-page clear as well.
- **Functional change needed:** no.
- **Confidence:** confirmed by reading.

#### SR10 (Low). The PRF output crosses the JavaScript boundary as a hex string and is never zeroed in JavaScript

- **Where:** `webauthn-bridge.js:157-158`, `:228-229`; `WebAuthnService.cs:49-66`, `:82`, `:95`.
- **What the code does:** the `Uint8Array` is hex-encoded into a string that is returned to .NET as an immutable
  `string`; only the `Convert.FromHexString` copy is zeroed. The JavaScript array and both strings are left to the
  garbage collector.
- **What could result:** the root secret of every derived key sits in process memory longer than it needs to.
  Same-process exposure only.
- **Recommendation:** return a `Uint8Array` (interop marshals it as `byte[]`), `fill(0)` the JavaScript copy after
  use, and zero the `byte[]` in a `finally`, as `DeriveKeys` already does for its copy.
- **Functional change needed:** no.
- **Confidence:** confirmed by reading.

#### SR24 (Low). Remembered passwords are encrypted through the legacy stream cipher service, with no binding and a key copied into a string

- **Where:** `src/Devoctomy.Blazwiz.Auth/Services/PasswordCryptoService.cs:41-56`,
  `src/Devoctomy.Blazwiz.Cryptography/BouncyCastleAesGcmCryptoService.cs`.
- **What the code does:** `ICryptoService` is an older stream API: it takes the key as a base64 `string`, uses no
  associated data, buffers the whole stream, and does not zero its key or its plaintext buffer on failure.
  `PasswordCryptoService.GetBase64Key` turns the zeroed key copy into a string that cannot be zeroed. The newer
  `IAeadCipher` (used by everything else) has none of these problems. `ICryptoService` has two users left:
  `PasswordCryptoService` and `LocalStorageE2eProfileLabelStore`.
- **What could result:** a key copy that outlives its use; an encrypted password that is not bound to the user it was
  saved for (moving a ciphertext between two users' entries only works if they share a passkey, so this is defence in
  depth).
- **Recommendation:** move both callers to `IAeadCipher` with the user's email (and, for labels, the profile id) as
  associated data, and remove `ICryptoService` and `BouncyCastleAesGcmCryptoService`.
- **Functional change needed:** no for users. Saved passwords and labels from before the change stop decrypting, which
  before 1.0 needs no migration (`AGENTS.md`, "No backward compatibility before 1.0"); the next sign in asks for the
  password and offers the passkey again.
- **Confidence:** confirmed by reading.

#### SR32 (Low). The WebAuthn user handle embeds the email address, and `RegisterAsync`'s second argument is not what the bridge expects

- **Where:** `WebAuthnService.cs:20-22` passes `userId` where `webauthn-bridge.js:74` takes `existingCredentialId`;
  `webauthn-bridge.js:85` builds `user.id` from `username + '-' + Date.now()`.
- **What could result:** authenticators store and return the user handle, so the email leaks to anything that can read
  it (WebAuthn §14.6.1 says it must not contain identifying information). The parameter mismatch is harmless today (the
  one caller passes `null`) but will misbehave the first time someone passes an id.
- **Recommendation:** random bytes for `user.id`; rename or remove the parameter.
- **Functional change needed:** no (new registrations only; existing passkeys keep working).
- **Confidence:** confirmed by reading.

#### SR34 (Low). A deployed app whose Cognito settings are missing silently falls back to the placeholder sign in

- **Where:** `src/Devoctomy.Blazwiz.Auth.Cognito/Extensions/CognitoServiceCollectionExtensions.cs:74-81`;
  `template/MyApp.Portal/wwwroot/appsettings.json:8-13` ships the section empty.
- **What the code does:** when region, pool or client id is blank, `LocalAuthenticationService` is used: any non-empty
  username and password sign in, and an 8 hour session is written. The login page shows a small hint.
- **What could result:** a publish that skipped `deploy.ps1`'s `appsettings.Production.json` step serves an app where
  anyone "signs in". There are no AWS credentials behind it, so nothing in S3 is reachable, but the app looks
  authenticated and local data for whatever username was typed is readable.
- **Recommendation:** fail fast when the build is not Development (or when `index.html` is served from anything but
  localhost) and `AwsCognito` is unconfigured, unless the app opts in explicitly. At least a prominent banner.
- **Functional change needed:** yes, small: local development keeps working; a misconfigured deployment stops
  working instead of pretending to.
- **Confidence:** confirmed by reading.

#### SR42 (Informational). Password change and passkey lookup match the saved user differently

- **Where:** `src/Devoctomy.Blazwiz.UI/ViewModels/ChangePasswordViewModel.cs:133-137` matches on `Email ==
  session.Username`; `src/Devoctomy.Blazwiz.UI/Services/PasskeyKeysService.cs:94-96` matches `Username` or `Email`.
- **What could result:** if a session's username is ever not the email, a password change leaves the old password
  encrypted in `saved_users` instead of re-encrypting or forgetting it. With `UsernameAttributes: [email]` in the
  stack this does not happen today.
- **Recommendation:** one shared predicate.
- **Functional change needed:** no.

### Key management and encryption

#### SR3 (Medium). Changing a passphrase does not rotate the master or file keys, and the old document stays in Secrets Manager

- **Where:** `src/Devoctomy.Blazwiz.Encryption/E2eProtectedProfileManager.cs:290-312` (`ChangePassphraseAsync`),
  `src/Devoctomy.Blazwiz.Encryption/E2eKeysDocumentService.cs:165-183` (`RewrapMasterKeyAsync`);
  `src/Devoctomy.Blazwiz.UI.Sync/Services/SecretsManagerUserKeysStore.cs` (`PutSecretValue`).
- **What the code does:** the master key is unwrapped with the current passphrase and wrapped again with the new one. The
  master key, the file keys and the active key id are unchanged (`IE2eKeysDocumentService.cs:50-53` says so). The new
  document is written with `PutSecretValue`, which keeps the previous version as `AWSPREVIOUS`; every device also keeps
  its copy in localStorage until it next unlocks online.
- **What could result:** a passphrase change does not revoke anyone. Whoever captured a keys document (a stolen
  device's localStorage, a backup, the `AWSPREVIOUS` version in the account) **and** learns the old passphrase, or who
  ever held the master key, can still unwrap the same master key, hence the same file keys, hence **every file,
  including those written after the change**. Users will reasonably assume that changing the passphrase after a
  suspected compromise locks the attacker out; it does not.
- **Recommendation:** on passphrase change, create a new master key, rewrap every file key under it, and add a new
  active file key (retiring the old one) so new writes use a key the attacker never held. Optionally offer to
  re-encrypt existing files with the new key (the `Status: retired` field and the key-id header already allow this).
  Say in the Settings UI what a passphrase change does and does not protect against until that exists.
- **Functional change needed:** yes. New behaviour (key rotation, possibly re-encryption with progress) and new text.
- **Confidence:** confirmed by reading.

#### SR23 (Low). The device-unlock wrapped master key is not bound to its profile

- **Where:** `E2eProtectedProfileManager.cs:171` (`keyWrapService.Wrap(kek, masterKey)` with no associated data) against
  file keys, which are bound to their id (`E2eKeysDocumentService.cs:72`, `:200`).
- **What could result:** a wrapped master key moved from profile A's localStorage entry to profile B's unwraps
  successfully; it then fails to unwrap B's file keys (`InvalidDataException`, not the expected
  `E2ePassphraseRequiredException`), so there is no bypass, only an unexpected error path. Defence in depth.
- **Recommendation:** pass `Encoding.UTF8.GetBytes(scope.Id)` as associated data on wrap and unwrap.
- **Functional change needed:** no. Existing device-unlock entries stop unwrapping and fall back to the passphrase
  (no migration before 1.0).
- **Confidence:** confirmed by reading.

#### SR30 (Low). Argon2id parameters read from a stored document are trusted without upper bounds, and a wrong key length escapes as an untyped exception

- **Where:** `E2eKeysDocumentService.cs:148-151` (`document.Kdf.ToParameters()`),
  `src/Devoctomy.Blazwiz.Cryptography/BouncyCastleArgon2idKeyDerivationService.cs:31-34` (lower bounds only).
- **What the code does:** memory, iterations and parallelism come from the document (correct: they must match what the
  key was derived with). Only minimums are checked. `KeyLength` other than 32 passes the KDF and then fails in
  `BouncyCastleAesGcmAeadCipher.CreateCipher` with `ArgumentException`, which `UnwrapMasterKeyAsync` does not map.
- **What could result:** a tampered or corrupted document (localStorage, or the secret) with `memory_kib` in the
  gigabytes hangs or crashes the tab on unlock (denial of service against that profile); a wrong key length surfaces
  as an unhandled exception rather than "this document is not valid". No confidentiality impact: the derived key would
  not unwrap anything.
- **Recommendation:** in `Parse`, reject `memory_kib` above a ceiling (say 4 GiB), `iterations` above a few hundred,
  and `key_len` other than the cipher's key size, with `InvalidDataException`.
- **Functional change needed:** no.
- **Confidence:** confirmed by reading.

#### SR37 (Informational). Random GCM nonces per file key with no rotation trigger

- **Where:** `src/Devoctomy.Blazwiz.Encryption/EncryptedFileFormat.cs:52`; `FileKeyEntry.RetiredStatus`.
- A fresh 96-bit random nonce per write is correct. NIST advises staying under about 2^32 encryptions per key with
  random nonces; nothing counts or rotates, although the format supports retired keys. Far from a problem for personal
  sync; document a rotation policy, and SR3's rotation work gives a place to hang it.
- **Functional change needed:** only if rotation is added.

#### SR38 (Informational). Passphrases and passwords live in .NET strings

- Blazor input binding gives strings, which cannot be zeroed; the view models set them to empty after use and the KDF
  zeroes its UTF-8 copy (`BouncyCastleArgon2idKeyDerivationService.cs:51-60`). Inherent and acceptable.

#### SR40 (Informational). The Argon2id default depends on how the *package* was built

- **Where:** `src/Devoctomy.Blazwiz.Cryptography/Configuration/Argon2idOptions.cs:26-31` (`#if DEBUG` picks 19 MiB);
  `template/MyApp.Portal/wwwroot/appsettings.Development.json` (19 MiB, excluded from publish).
- Published packages are packed with `-c Release` (`release.yml:133`, `test-template.ps1:149`), so generated apps get
  512 MiB. Only this repository's template, which builds the packages from source, would derive 19 MiB keys from a
  Debug publish. Parameters are stored with each document, so nothing breaks; it is only strength. Worth a sentence in
  the Aws README's deploy section.

### Sync engine, stores and profiles

#### SR2 (Medium). The idle auto-lock never starts for profiles defined in code

- **Where:** `src/Devoctomy.Blazwiz.Sync/Encryption/E2eIdleLock.cs:111` (and `Refresh`, `:61`);
  `src/Devoctomy.Blazwiz.Sync/SyncProfileRegistry.cs:53-58`.
- **What the code does:** `StartTimer(scope)` calls `registry.Find(scope.Id)`, but `Find` matches on the profile
  **name**. For profiles users create, name and key scope id are the same GUID, so it works. For a profile set up in
  code, such as the template's `files` (name `files`, key id `7c9e2a4b-…`), `Find` returns null and the method takes
  the "no idle timeout" branch. `E2eIdleLockTests` only use profiles whose name equals the scope id, so they pass.
- **What could result:** the documented control (`E2eProfileSettings.IdleTimeout`, 15 minutes by default;
  `docs/diagrams/sync.md`, `sync-framework.md`, `session-management.md`) does not hold for the profile every generated
  app ships with. An unlocked `files` profile keeps its keys in memory, and its plaintext readable, for as long as the
  tab is signed in, including on an unattended machine.
- **Recommendation:** look the profile up by scope (`registry.Profiles.FirstOrDefault(p => p.KeyScope == scope)`), or
  give the registry a `FindByScope`. Add a unit test where `Name != KeyScope.Id`, and a UI test that the `files`
  profile locks after the timeout.
- **Functional change needed:** no. It restores the documented behaviour.
- **Confidence:** confirmed by reading (the reviewer checked `Find` and the template profile directly).

#### SR18 (Low). Loading the index replaces a code-defined profile's security settings with the user-profile defaults

- **Where:** `src/Devoctomy.Blazwiz.Sync/Encryption/SyncProtectedProfileService.cs:100-117`, `:292-298`, `:319-321`;
  `E2eProtectedProfileManager.cs:97-100` (every scope, including code profiles, is added to the index when its keys are
  created).
- **What the code does:** when a code profile's `IdleTimeout` differs from the index entry's, the **whole**
  `E2eProfileSettings` object is replaced by one built from `options.ProfileDefaults`, so `AllowDeviceUnlock`,
  `MinimumPassphraseLength` and `Argon2` silently become the defaults.
- **What could result:** an app that turns device unlock off for `files` but leaves it on for created profiles finds,
  after the first load, that `files` allows passkey unlock, and that its passphrase rule has changed. The template sets
  neither, so it is unaffected today.
- **Recommendation:** apply index timeouts only to created profiles, and when applying, derive from the existing
  settings (`WithIdleTimeout(existing.Settings, …)`).
- **Functional change needed:** no (a bug fix).
- **Confidence:** confirmed by reading.

#### SR19 (Low). A zero-length stored file bypasses the encrypted-format check

- **Where:** `src/Devoctomy.Blazwiz.Sync/Encryption/EncryptingSyncFileStore.cs:39-42`.
- **What the code does:** empty content is passed through unchanged (meant for folder markers). Every other
  modification of ciphertext fails AES-GCM authentication; truncation to zero bytes does not.
- **What could result:** anyone who can write the user's S3 prefix (an operator, leaked credentials, a buggy device)
  can make a file read as empty with no error, where any other tamper is detected. Such an actor can already delete the
  file, so this is about *silent* corruption rather than new access.
- **Recommendation:** pass through only when the key ends with `/`; otherwise throw `InvalidDataException` as for other
  plaintext.
- **Functional change needed:** no for users; a check that a truncated file is reported, not shown empty.
- **Confidence:** confirmed by reading.

#### SR20 (Low). Lost updates to the profile index wipe a profile's local keys and files

- **Where:** `E2eProtectedProfileManager.cs:97-100`, `:322-326`, `:407-432`; `SyncProtectedProfileService.cs:85-98`.
  Partly acknowledged in `docs/plans/active/2026-11-10-protected-profile-concurrent-writes-plan.md`.
- **What the code does:** the index is one secret, updated read-modify-write with `PutSecretValue` (no version check).
  On load, a created profile that is not in the fetched index is treated as deleted elsewhere: its local keys copy and
  label are removed and its local files deleted. A missing index secret counts as "no profiles".
- **What could result:** two devices creating profiles at about the same time lose one entry; on the next load every
  device forgets that profile and deletes its local files. The keys secret and the S3 ciphertext survive, but no UI can
  re-attach them. Deleting the index secret by accident does the same for all created profiles.
- **Recommendation:** before forgetting, check `userKeysStore.GetAsync(scope)`; if the keys document still exists,
  re-add the entry instead of wiping. Longer term, derive the list from the per-profile secrets or compare secret
  versions before writing the index.
- **Functional change needed:** yes (recovery behaviour).
- **Confidence:** the race is reasoning; the "missing index wipes local copies" path is confirmed by reading.

#### SR21 (Low). Profile deletion goes ahead when S3 is unreachable for credential reasons

- **Where:** `SyncProtectedProfileService.cs:279-290`; `src/Devoctomy.Blazwiz.Sync/S3/S3SyncRemoteStore.cs:213-216`;
  `src/Devoctomy.Blazwiz.UI.Sync/Services/CognitoS3TargetProvider.cs:46-61`.
- **What the code does:** `ListRemoteAsync` swallows `RemoteNoCredentials`, intended for local development without
  Cognito. The target provider also returns null when the identity pool refuses the token or the session has ended, so
  the same code is thrown then, and deletion continues: keys deleted, S3 files left.
- **What could result:** permanently undecryptable orphan objects under the user's prefix, contrary to the documented
  contract of `DeleteAsync`.
- **Recommendation:** distinguish "not configured" from "no credentials right now" (a separate error code), and fail the
  delete in the second case.
- **Functional change needed:** small, yes: deletion refuses while the session cannot reach S3.
- **Confidence:** confirmed by reading.

#### SR22 (Low). Conflict resolution trusts the remote-supplied edit time, and files are not bound to a version

- **Where:** `S3SyncRemoteStore.cs:140`, `:193-199`; `src/Devoctomy.Blazwiz.Sync/NewestWinsConflictResolver.cs:28-30`;
  `src/Devoctomy.Blazwiz.Sync/Models/RemoteObjectInfo.cs:37`.
- **What the code does:** `x-amz-meta-modified-at` is parsed without bounds and preferred over S3's `LastModified`. The
  AEAD binds profile and path but no version or counter.
- **What could result:** an actor who can write the prefix can set a far-future time so their copy wins every conflict,
  or re-upload an older ciphertext of a path, which decrypts and is adopted as "newer" on every device (a silent
  rollback). Such an actor can already overwrite and delete, so the gain is choosing the outcome quietly.
- **Recommendation:** ignore metadata times later than `LastModified` plus a small skew (fall back to `LastModified`);
  state in the threat model that S3-side actors can roll back or delete, not read or forge.
- **Functional change needed:** no.
- **Confidence:** confirmed by reading; the attack is reasoning.

#### SR35 (Informational). Encryption covers content only

- S3 object keys (file names and folder structure), sizes, `x-amz-meta-modified-at`, and the sync event log's keys and
  error details (`SyncEngine.cs:60`, `:139`, `:395-409`) are plaintext in S3 and in IndexedDB. Standard for this kind of
  design; state it in the Sync README and the threat model so app developers do not put secrets in file names.

#### SR36 (Informational). The glob matcher accepts dot segments that later layers treat literally

- **Where:** `src/Devoctomy.Blazwiz.Sync/GlobSyncPathMatcher.cs:40-53`.
- Tested with FileSystemGlobbing 10.0.12 and pattern `files/**/*`: `files/a/../b.txt`, `files/./a.txt` and
  `files//a.txt` match (normalised inside the prefix); `files/../x.txt`, `files/../../profiles/p/f` and `/notes/a.md` do
  not. So nothing escapes a profile, but two literal keys can alias one path in IndexedDB, and the OPFS and folder
  stores percent-encode `.`/`..` and throw on empty segments (`filetree.js:8-15`). On Windows test hosts the matcher
  also accepted `files\a.txt`, which it will not in the browser, so a unit test can pass for an input that behaves
  differently at runtime. Reject `\`, empty, `.` and `..` segments explicitly before delegating.
- **Functional change needed:** no.

#### SR39 (Informational). Whole-object buffering

- `S3SyncRemoteStore.GetAsync` (`:75-86`) reads the whole object into memory with no cap; the verify pass hashes every
  local file. A very large object placed in the prefix can crash the tab. A size ceiling per profile would bound it.

### AWS stack and deployment

The IAM design was checked line by line and holds: `ListBucket` is conditioned on the identity's own prefix, object
actions are on `${bucket}/${cognito-identity.amazonaws.com:sub}/*`, Secrets Manager actions on
`${aws:PrincipalTag/userSub}/keys/*` (which also governs `CreateSecret` names), no unauthenticated role exists, the
trust policy checks `aud` and `amr`, and `ServerSideTokenCheck` is on. The findings below are about resilience and
hardening, not isolation.

#### SR4 (Medium). The data bucket has no versioning

- **Where:** `src/Devoctomy.Blazwiz.Aws/cloudformation/stack.yaml:162-198` (no `VersioningConfiguration`); `:338-344`
  grants `s3:DeleteObject` and `s3:PutObject`. Already noted in `docs/plans/backlog/v1-feature-ideas.md:38`.
- **What could result:** anything holding the session's credentials (in memory for up to an hour, renewable for
  12 hours while the tab lives), or a bug in the engine, can **delete or overwrite every object under the user's prefix
  with no way back**. Conditional writes protect against concurrent edits, not malice or bugs; encryption does not help
  availability.
- **Recommendation:** enable versioning with a lifecycle rule expiring noncurrent versions (30 days, say) and aborting
  incomplete multipart uploads; keep `s3:DeleteObjectVersion` and `s3:PutBucketVersioning` out of the role, which they
  already are, so user deletes become delete markers only an operator can purge. Listing and `GetObject` without a
  version id behave as before, so no engine change.
- **Functional change needed:** no. Infrastructure only (the stack's stateful-resource policy allows this update).
- **Confidence:** confirmed by reading.

#### SR5 (Medium). Keys secrets are force-deleted with no recovery window, and the policy allows it

- **Where:** `stack.yaml:351-361` (`secretsmanager:DeleteSecret` unconditional; `RestoreSecret` granted but moot);
  `src/Devoctomy.Blazwiz.UI.Sync/Services/SecretsManagerUserKeysStore.cs:75` (`ForceDeleteWithoutRecovery = true`);
  `src/Devoctomy.Blazwiz.Aws/cloudformation/test-auth.ps1:241`, `:259`.
- **What could result:** the keys document is the only thing that makes a profile's files readable (the device copies
  aside). Any code path or hijacked session that reaches `DeleteAsync` destroys them **immediately and irrecoverably**;
  Secrets Manager's 7 to 30 day window exists for exactly this.
- **Recommendation:** delete with `RecoveryWindowInDays = 7` in the app, and add to the role a `Deny` on
  `secretsmanager:DeleteSecret` with condition `Bool: secretsmanager:ForceDeleteWithoutRecovery: 'true'`. Profile ids
  are GUIDs, so the name being reserved during the window is harmless. `test-auth.ps1:258-259` creates and force-deletes
  the user's real `keys/index` when none exists; with a window that would block the app from creating the index for a
  week, so the check should use a throwaway name. `SecretsManagerUserKeysStoreTests.cs:315-331` needs updating.
- **Functional change needed:** small, yes: a deleted profile's keys linger (unreadable to the app) for the window, and
  the test script changes what it creates.
- **Confidence:** confirmed by reading.

#### SR6 (Medium). No MFA and no threat protection on the user pool

- **Where:** `stack.yaml:226-253` (no `MfaConfiguration`, `EnabledMfas` or `UserPoolAddOns`). In the backlog:
  `docs/reviews/2026-11-08-security-review/backlog/mfa-totp.md`.
- **What could result:** a phished or reused password gives the ID token, hence the AWS credentials, hence the wrapped
  keys and ciphertext and the power to destroy them (SR4, SR5). The profile passphrase is then the only barrier to
  reading files. Cognito's lockout on repeated failures still applies; there is no compromised-credential check.
- **Recommendation:** `MfaConfiguration: OPTIONAL` with `SOFTWARE_TOKEN_MFA` once the sign-in flow handles the
  challenge (today it is reported as `Auth.ChallengeUnsupported`); `UserPoolAddOns: { AdvancedSecurityMode: AUDIT }`
  as a cheap first step, noting in the README that `ENFORCED` has a per-user cost.
- **Functional change needed:** yes (a second sign-in step, Profile page set-up, decision on how the passkey and offline
  sign in interact with it).
- **Confidence:** confirmed by reading.

#### SR11 (Low). The public app client allows `USER_PASSWORD_AUTH`, contradicting the documented SRP guarantee

- **Where:** `stack.yaml:262-266`; used only by `test-auth.ps1:124-129`. Docs: `docs/diagrams/authentication.md:68`,
  `template/README.md:202-203`.
- **What could result:** any party holding the public client id can submit plaintext passwords to Cognito. A phishing
  page can validate a captured password against the real pool, and credential stuffing is simpler than through SRP.
  The portal itself only uses SRP and refresh.
- **Recommendation:** remove `ALLOW_USER_PASSWORD_AUTH` from the portal client. For `test-auth.ps1`, use
  `ADMIN_USER_PASSWORD_AUTH` with the deployer's IAM credentials (add `cognito-idp:AdminInitiateAuth` to
  `deploy-policy.json`), or a second app client for tests.
- **Functional change needed:** no for the app; the test script changes.
- **Confidence:** confirmed by reading.

#### SR12 (Low). Removing an environment, or a user, leaves their secrets behind

- **Where:** `src/Devoctomy.Blazwiz.Aws/cloudformation/remove.ps1:5-7` says it deletes "all per-user data";
  `:59-73` empties the buckets and deletes the stacks only. Users' secrets are created at run time, so `delete-stack`
  never sees them, and `deploy-policy.json` has no `secretsmanager:*` actions.
- **What could result:** wrapped master keys and profile indexes for every user remain in the account indefinitely
  (confidentiality holds; it is retention, cost and a false promise). Deleting a single user, or re-creating one with
  the same email (a new `sub` and identity id), orphans their secrets and prefix the same way.
- **Recommendation:** tag user secrets at creation (`secretsmanager:TagResource` scoped by tag) so scripts can find
  and delete them, or have `remove.ps1` enumerate `<sub>/keys/` for every pool user; fix the wording meanwhile.
- **Functional change needed:** scripts and a tag on created secrets; nothing users see.
- **Confidence:** confirmed by reading.

#### SR13 (Low). No Cognito-level deletion protection

- **Where:** `stack.yaml:226-230`, `:281-284` (`DeletionPolicy: Delete`, no `DeletionProtection`);
  `stack-policy.json` protects updates only.
- **What could result:** `delete-stack` (via `remove.ps1 -Force` or the console) or a direct `DeleteUserPool` removes
  every user and, with the identity ids, the mapping to their data.
- **Recommendation:** `DeletionProtection: ACTIVE` on the user pool, at least when `Environment` is `prod`, with
  `remove.ps1` turning it off first.
- **Functional change needed:** no (infrastructure and script).

#### SR14 (Low). Email changes need no verification, and the client may write any attribute

- **Where:** `stack.yaml:226-253` (no `UserAttributeUpdateSettings`), `:255-277` (no `ReadAttributes`/`WriteAttributes`).
- **What could result:** `UpdateUserAttributes` is a public API callable with the access token (in localStorage). A
  script holding it can change the email, which is the sign-in alias, locking the real user out with no verified address
  for recovery. The app never exposes this; the API does.
- **Recommendation:** `AttributesRequireVerificationBeforeUpdate: [email]`; `WriteAttributes: [name]` or empty.
- **Functional change needed:** no.
- **Confidence:** Likely (documented Cognito defaults, not exercised).

#### SR15 (Low). Test-user password on the command line and plaintext temp files

- **Where:** `deploy.ps1:381-389` (`admin-set-user-password … --password`), `test-auth.ps1:122`, `:248-250`,
  `_common.ps1:228-238` (temp JSON without a restrictive ACL).
- **What could result:** local exposure through process listings, shell transcripts or endpoint telemetry on the
  deployer's machine.
- **Recommendation:** `--cli-input-json file://` from a user-only-ACL file, overwritten before deletion; do not write the
  real index back in the test.
- **Functional change needed:** no (scripts).

#### SR16 (Low). Local development origins are in the data bucket's CORS list in every environment

- **Where:** `stack.yaml:31-34` (default `https://localhost:7090,http://localhost:5090`), `:183-189`; `deploy.ps1`
  never passes `LocalDevOrigins`.
- **What could result:** CORS is not the access control (requests are signed), but a page on localhost that obtains a
  user's credentials can have the browser call the production bucket. Hygiene.
- **Recommendation:** expose `-LocalDevOrigins` in `deploy.ps1` and default it to empty for `prod`. `POST` in
  `AllowedMethods` is unused.
- **Functional change needed:** no.

#### SR17 (Low). The deployer policy has a documented escalation path and account-wide Cognito admin actions

- **Where:** `template/cloudformation/deploy-policy.json:123-147` (`iam:CreateRole`, `PutRolePolicy`,
  `UpdateAssumeRolePolicy`, `PassRole` on `role/myapp-*`), `:83-103` (`AdminSetUserPassword`, `AdminCreateUser` on
  `userpool/*`). The README discloses the first (`template/cloudformation/README.md:50`).
- **What could result:** the deployer identity can create a `myapp-*` role trusting itself with any policy, and can set
  any user's password in any pool in the account.
- **Recommendation:** the permissions-boundary condition the README mentions, as text, with `PermissionsBoundary` on
  `AuthenticatedRole`; scope the Cognito admin actions by the pool's `app` tag (needs testing). `cloudformation:SetStackPolicy` is listed twice (`:12`, `:19`).
- **Functional change needed:** no.
- **Confidence:** escalation path confirmed; tag condition support needs testing.

#### SR48 (Informational). No audit logging

- No CloudTrail data events on the data bucket, no S3 access logs, no CloudFront logs. After an incident (SR4, SR5) there
  is no record of which credentials deleted what. A data-event trail on `DataBucket` is the minimum.

#### SR49 (Informational). Documentation drift and small observations in the stack

- `docs/plans/backlog/v1-feature-ideas.md:33,37` still says there is no CI pipeline and no CSP; both exist.
- `template/cloudformation/README.md:26` says `deploy.ps1` runs `aws cloudformation deploy`; the base stack goes
  through change sets.
- `test-auth.ps1:211-217` checks that a write to another prefix is denied, but not a read or a list; the policy denies
  them, and the end-to-end check would be stronger covering them.
- With `CloudFrontDefaultCertificate: true` (`stack.yaml:97`) the minimum TLS version is 1.0 and cannot be raised; only
  the custom-domain branch pins `TLSv1.2_2021`. Worth a sentence in the README.
- The managed `SecurityHeadersPolicy` sends HSTS without `includeSubDomains`; the meta CSP cannot set
  `frame-ancestors` (X-Frame-Options from the managed policy covers framing). A custom response-headers policy would
  allow a header CSP.
- An invited user can create unbounded objects and secrets: a cost exposure, mitigated by invite-only sign-up.

### UI, template and browser

#### SR31 (Low). The Content-Security-Policy exists only when the app is published through `deploy.ps1`

- **Where:** `src/Devoctomy.Blazwiz.Aws/cloudformation/_common.ps1:240-323` injects a meta CSP at publish time
  (`default-src 'self'`, hashed inline scripts, `wasm-unsafe-eval`, `connect-src` limited to the four regional AWS
  endpoints, `worker-src 'self' blob:` for the OPFS worker, `object-src 'none'`, `base-uri 'self'`);
  `template/MyApp.Portal/wwwroot/index.html` has none. (One review pass reported "no CSP"; the reviewer narrowed it to
  this after reading the deploy script.)
- **What could result:** an app hosted any other way (another CDN, a container, `dotnet run` behind a proxy) ships
  without the one control that limits what an XSS can do with the tokens in localStorage and the keys in memory.
  `style-src 'unsafe-inline'` is also wider than needed if Blazor's requirements allow.
- **Recommendation:** make the CSP part of the build (an MSBuild step or the template's `index.html` with a documented
  placeholder for the region and bucket) rather than of one deployment script, so every hosting path gets it; tighten
  `style-src` if the UI permits.
- **Functional change needed:** no for users; a build change.
- **Confidence:** confirmed by reading.

#### SR33 (Low). Note keys taken from the URL are only prefix-and-extension checked

- **Where:** `template/MyApp.Portal/Pages/NoteEditor.razor.cs:29-30`, `:53-57`;
  `template/MyApp.Portal.Application/Services/NoteService.cs:186-225`; `NoteTreeService.cs:89-97`.
- **What could result:** `?note=notes/../x.md` or `notes//x.md` is read and written; the stores stay inside the user's
  own profile and prefix (SR36), so this is self-affecting: odd keys in the user's own store, not access to anything
  else. Names created through the UI are validated; URL keys are not.
- **Recommendation:** validate every segment of a URL-supplied key with `NoteNameService`'s rules.
- **Functional change needed:** no.
- **Confidence:** confirmed by reading; the effect on each store needs testing.

#### SR41 (Informational). Framework text rendered as raw HTML

- `src/Devoctomy.Blazwiz.UI/Views/LoginView.razor:286` renders `Login.Passkey.Question` as `MarkupString` so the
  resource can hold `<strong>`. Resources are developer-controlled, but text overrides are an extension point and are
  not expected to be HTML, and pseudo-localisation accents the letters inside the tag. The only other `MarkupString` is
  the Markdown preview, which is sanitised (see below). Split the sentence around markup instead.
- **Functional change needed:** no.

#### SR43 (Informational). The sync log persists raw exception text

- `SyncView.razor:165-168` shows the first line of `Detail`, which is `ex.ToString()` (`SyncEngine.cs:395-409`) and
  can hold AWS request ids, bucket names and identity ids, kept in IndexedDB for 30 days. HTML-encoded and the user's
  own; acceptable, just be aware.

### Build, CI, dependencies and supply chain

`dotnet list package --vulnerable --include-transitive` reported **no vulnerable packages** in any of the 28 projects
(nuget.org and the local Visual Studio offline folder were the only sources). The vendored SimpleWebAuthn bundle is
byte-identical to the published `@simplewebauthn/browser@14.0.0` after line-ending normalisation (SHA-256
`29bb0699b45bc39a350d992a13bfc1a4d40d89531390581e1f57f1142c6e221d`). No scripts are loaded from a CDN. No secrets are
committed. Release publishing uses OIDC trusted publishing, no stored API key, and refuses commits CI has not passed.

#### SR7 (Medium). GitHub Actions are pinned by tag, including the one that receives the nuget.org publishing token

- **Where:** `.github/workflows/ci.yml:43,45,72,86,88,105`; `.github/workflows/release.yml:76,78,142,162,194,196`
  (`actions/checkout@v4`, `actions/setup-dotnet@v6`, `actions/upload-artifact@v4`, `actions/download-artifact@v4`,
  `NuGet/login@v1`).
- **What could result:** a compromised or moved tag runs attacker code with the job's permissions. In `publish` that is
  `id-token: write` and the short-lived nuget.org key from `NuGet/login`, so a malicious action could **publish packages
  under `Devoctomy.Blazwiz.*`** that every generated app would restore. This is the vector used against
  `tj-actions/changed-files` in 2025.
- **Recommendation:** pin every action to a full commit SHA with a version comment, and add `.github/dependabot.yml`
  for `github-actions` so the pins are kept current.
- **Functional change needed:** no (workflow files only).
- **Confidence:** confirmed by reading.

#### SR8 (Medium). `test-template.ps1` deletes a pre-existing `-WorkRoot` folder when anything fails

- **Where:** `templatepack/test-template.ps1:53`, `:57`, `:127`, `:278-289`.
- **What the code does:** `$ErrorActionPreference = 'Stop'`; `New-Item -ItemType Directory -Path $workRoot` throws if
  the folder exists; the `catch` records the failure and the `finally` runs `Remove-Item -Recurse -Force $workRoot`
  unless `-KeepOutput`. The throw on an existing folder was verified in a scratch folder; the deletion path was not run.
- **What could result:** a maintainer who points `-WorkRoot` at an existing folder loses its contents. CI passes a fresh
  `${{ runner.temp }}` folder and is unaffected. Local data loss, not a remote weakness; rated Medium because it is
  destructive and needs no attacker.
- **Recommendation:** refuse to run if `$workRoot` exists, or remove it in `finally` only when this run created it.
- **Functional change needed:** no (script behaviour on an edge case).
- **Confidence:** confirmed.

#### SR25 (Low). `${{ github.ref_name }}` is interpolated into PowerShell `run:` scripts

- **Where:** `release.yml:98` (single-quoted; a `'` in the branch name breaks out) and `:172` (double-quoted; `$(…)` is
  evaluated).
- **What could result:** script injection in the job holding the publishing token. Only someone who can create a
  branch and dispatch the release workflow can set the name, and they could edit the workflow anyway, so it is a pattern
  to remove rather than an open door.
- **Recommendation:** pass it through `env:` and read `$env:REF_NAME`. Every other interpolation in both workflows is
  safe (`sha`, `repository`, `run_number`, `runner.temp`, and a version validated by `^\d+\.\d+\.\d+$`).
- **Functional change needed:** no.

#### SR26 (Low). `actions/checkout` keeps the job token in `.git/config`

- **Where:** `ci.yml:43,86`; `release.yml:76,194`.
- **What could result:** later steps could use the token; it is `contents: read` except in `github-release`, where it
  is `contents: write`. Defence in depth.
- **Recommendation:** `persist-credentials: false` on every checkout.
- **Functional change needed:** no.

#### SR27 (Low). Security code analysis is at SDK defaults

- **Where:** `Directory.Build.props`, `template/Directory.Build.props` (no `AnalysisMode`/`AnalysisLevel`);
  `analyzers.globalconfig` disables only StyleCop rules and RS0041.
- **What could result:** CA3xxx (injection) and most CA5xxx (crypto, certificate) rules never run, so regressions in
  those areas are not caught at build time. Nothing security-relevant is suppressed today.
- **Recommendation:** `<AnalysisMode>Recommended</AnalysisMode>` (or at least `AnalysisModeSecurity` = `All`) in both
  files, and fix what it reports.
- **Functional change needed:** no.

#### SR28 (Low). `.gitignore` gaps for secret-bearing files

- **Where:** `.gitignore` and `template/.gitignore` cover `*.env`, `*.pfx`, `*.pubxml` and one specific
  `appsettings.Production.json` path, but not `.env.*`, `*.pem`, `*.p12`, `*.key`, `appsettings.*.local.json`,
  `secrets.json` or `**/appsettings.Production.json`.
- **What could result:** a future accidental commit of a key or a production settings file is not stopped by the
  patterns. Nothing of the kind is committed today.
- **Functional change needed:** no.

#### SR29 (Low). Template `Title` and `Description` are substituted into JSON, HTML and C# without escaping

- **Where:** `template/.template.config/template.json:20-35`, `:100-119`; sites in `appsettings.json`,
  `index.html`, `manifest.webmanifest`, `LoginPageTests.cs`, `README.md`.
- **What could result:** a `"` in the title gives invalid JSON and an uncompilable UI test; `<` or `</title>` corrupts
  `index.html`. The person creating the app chooses the value, so this is robustness rather than a trust boundary; the
  README only says "Avoid quotes".
- **Recommendation:** a `replace` form that strips `"<>&\`, or fail the parameter on them; a `test-template.ps1` case
  with such a title.
- **Functional change needed:** no for apps; template configuration.

#### SR44 (Informational). Vendored assets lack licence files and recorded provenance

- `src/Devoctomy.Blazwiz.Browser/wwwroot/lib/simplewebauthn/` has no licence text (MIT upstream); Bootstrap 5.3.0 and
  Bootstrap Icons 1.10.5 keep their MIT headers; Inter has its OFL file. There is no root `LICENSE` although
  `PackageLicenseExpression` is MIT. Add the licence files, a root `LICENSE`, and a `THIRD-PARTY-NOTICES` with version,
  source URL and SHA-256 for each vendored file; consider `*.min.js binary` in `.gitattributes` so on-disk hashes match
  upstream.

#### SR45 (Informational). No lock file, no `nuget.config`, no package source mapping

- Mitigated by Central Package Management with transitive pinning and exact versions, and NuGet audit on by default
  with warnings as errors. `test-template.ps1` writes a `nuget.config` into generated apps that adds the local feed
  without `<clear/>` or source mapping. Optional: a root `nuget.config` with `<clear/>` and mapping; `RestoreLockedMode`
  in CI.

#### SR46 (Informational). Deprecated test packages

- `xunit 2.9.3` (legacy; `xunit.v3` is the alternative) in every xunit project and `MSTest 3.10.4` (legacy) in the UI
  tests. Test-only.

#### SR47 (Informational). Release controls that live outside the repository

- `environment: nuget` (`release.yml:64`) is where required reviewers must be configured; `docs/releasing.md:66-67` says
  to add them, and the repository cannot show whether they are. The release jobs use `ubuntu-latest`/`windows-latest`
  while CI pins `ubuntu-24.04`. Confirm the reviewers in GitHub settings.

## Controls checked and found sound

- **Sign in:** SRP through `Amazon.Extensions.CognitoAuthentication` only; refresh token in memory only, revoked on
  sign out and when going offline; renewal only before expiry; `PreventUserExistenceErrors`; forgotten-password flow
  hides whether an address exists; session read is the one place sessions are checked, renewed and ended
  (`CognitoAuthenticationService.cs:203-279`).
- **Tenant isolation:** IAM scopes every S3 and Secrets Manager action to the identity's own prefix and the user's own
  `sub`; `ListBucket` has a prefix condition; no `*` actions; no unauthenticated identities; `ServerSideTokenCheck`;
  the client prefixes every key with the identity id and lists with the prefix (`S3SyncRemoteStore.cs:34-38`,
  `CognitoS3TargetProvider.cs:82`).
- **Encryption:** AES-256-GCM with random 96-bit nonces (`BouncyCastleAesGcmAeadCipher`); the file header (magic,
  version, key id, scope id, nonce) **and the path** are associated data, and the header scope is checked against the
  binding (`EncryptedFileFormat.cs:65-88`); unknown key id fails closed; plaintext in an encrypted profile is rejected;
  the engine never decrypts; file keys are bound to their id when wrapped; exactly one active key is enforced; the wrong
  passphrase is detected only through the GCM tag (no oracle beyond pass or fail). Argon2id 512 MiB, 2 passes, 16 byte
  salt, parameters stored with the document. HKDF-SHA-256 with the app id as salt and `app:keyname` as info for PRF
  keys. Key ids are 80 random bits.
- **Key hygiene:** every raw key is zeroed after use (`E2eKeysDocumentService`, `E2eProtectedProfileManager`,
  `EncryptingSyncFileStore`, `PrfKeyDerivator`, `WebAuthnService`); `InMemoryDerivedKeyStore` hands out copies and
  zeroes on replace, remove and clear; locking removes the profile's keys; sign out locks every profile.
- **Secrets Manager:** `CreateSecret` with `ResourceExistsException` treated as failure, so keys are never overwritten
  (`SecretsManagerUserKeysStore.cs:56-67`); offline mode never calls it; localStorage holds only wrapped material.
- **Profiles:** ids restricted to `[A-Za-z0-9_-]{1,64}` and GUIDs where they name secrets; `AddProfile` refuses patterns
  reaching `profiles/`, duplicate names and ids, and clashing stores; the glob matcher rejects keys that normalise
  outside the prefix (tested); OPFS and folder stores percent-encode `.`, `..`, `\` and `%` and throw on empty segments;
  folder migration refuses a non-empty target; every store is partitioned by owner.
- **Cross-tab:** BroadcastChannel and Web Locks are same-origin; only locks are broadcast, never unlocks; received ids
  are matched against registered profiles.
- **Engine integrity:** every S3 write and delete is conditional (`If-Match` / `If-None-Match: *`); 412 replans once;
  local revision is checked before a download overwrites or a dirty flag is cleared; per-file failures do not stop the
  pass; a log failure never fails a pass.
- **UI:** everything user- or server-supplied is rendered with Blazor's encoding; the Markdown renderer uses Markdig with
  `DisableHtml()`, individually listed extensions, and an allow-list of `http`, `https` and `mailto` link schemes with
  control-character stripping, tested against `javascript:`; no `innerHTML`, `eval`, `new Function`, dynamic import or
  CDN script anywhere; no `returnUrl`-style parameter and no `forceLoad` navigation; the shell renders nothing until a
  session is confirmed; password inputs are `type="password"` with correct `autocomplete`; passwords and passphrases
  are cleared after use and never logged, never placed in URLs, and framework errors show coded text, not exception
  messages; storage keys always go through `IAppStorageKeys`; theme and culture values are allow-listed; the service
  worker is the stock Blazor one and never caches AWS responses; `appsettings.json` holds only public identifiers.
- **Hosting:** private portal bucket behind CloudFront OAC; HTTPS redirect; managed security headers; `no-cache` on
  `index.html`, the service worker, the manifest and settings, with a full invalidation on each deploy; TLS-only bucket
  policies; public access blocked; SSE-S3; `BucketOwnerEnforced`; CORS origins limited to the distribution, the custom
  domain and the dev origins (SR16).
- **Workflows:** `contents: read` by default, `id-token: write` only on publish, `contents: write` only on the
  GitHub-release job; `workflow_dispatch` only for releases; no `pull_request_target`; no `${{ github.event.* }}` in
  `run:`; the release gate requires a successful CI run for the exact commit; artifacts stay within the run.
- **Code quality controls:** warnings as errors, nullable, StyleCop, PublicApiAnalyzers, package validation,
  `TreatWarningsAsErrors`; no `unsafe` code; no analyzer suppressions beyond style.

## What was fixed

On 2026-11-08, after the review, every finding marked as needing no functional change was fixed in the working tree
(uncommitted, for the maintainer to review). The changes are listed under "Security" in `CHANGELOG.md`'s Unreleased
section. How they were checked:

- **Code:** `dotnet build blazwiz.slnx` (warnings as errors, StyleCop, PublicApiAnalyzers, and from this change every
  .NET security analyzer rule), then all four suites: `--filter FullyQualifiedName~UnitTests`, `~IntTests` (the `@aws`
  scenarios skipped, as always without AWS credentials) and `~UITests` (Playwright, the portal started by the tests),
  and `./templatepack/test-template.ps1 -SkipRepositoryTests`, which packs the packages and template and creates,
  builds and tests apps with every option, now including one with quotes, angle brackets, an ampersand and a
  backslash in its title and description (SR29). Each fix has a unit test for the behaviour it changes.
- **AWS stack and scripts (SR4, SR11 to SR17, SR48):** changed in `stack.yaml`, `deploy.ps1`, `remove.ps1`,
  `test-auth.ps1`, `_common.ps1` and the template's `deploy-policy.json`. These **were not deployed or run**: nothing
  here can call AWS. The YAML and JSON were parsed for structure, every `Condition` reference was checked, and the
  scripts were parsed by PowerShell. The first deploy of an environment after this change needs the maintainer's
  attention: `deploy.ps1` lists the changes (versioning and a lifecycle rule on the data bucket, deletion protection
  and a CloudTrail trail with its log bucket in `prod`, attribute settings on the user pool and client, the admin auth
  flow in place of the password flow, the role's `secretsmanager:TagResource`), and `test-auth.ps1` then exercises the
  new permissions (tagging a secret, reading and listing another prefix). `deploy-policy.json` must be updated in the
  account first (it gained `cloudformation:UpdateStack`, the versioning, lifecycle, CloudTrail, `ListUsers`,
  `AdminInitiateAuth` and tagged-secret actions).
- **Stored data:** remembered passwords, profile labels and device-unlock keys from earlier builds stop decrypting
  (SR23, SR24); before 1.0 that needs no migration. Secrets created before this change carry no `app` tag, so
  `remove.ps1` reports them and leaves them for deletion by hand (SR12).
- **Second pass, later the same day:** the small functional changes SR1, SR5, SR20, SR21, SR34 and SR39 were made too,
  with the maintainer's agreement, and checked the same way, plus clypse (the one app on the framework) packed against
  the working tree and its unit and UI suites run. The stack change for SR5 (the `Deny` on forced deletion) is again not
  deployed.
- **Left for a decision, with the reason in the table:** SR17 (a permissions boundary needs the account's administrator
  to create the boundary policy first), SR31 (moving the CSP into the build is a design change, and `style-src` needs
  a browser check), SR45 (lock files) and SR46. SR3 and SR6 have backlog plans.

## Summary

| ID | Severity | Area | Description | What could result | Functional change needed? | Fixed |
|---|---|---|---|---|---|---|
| SR1 | Medium | Passkeys | User verification is `preferred`, never required or checked; the flags reporting it are always false | Possession of the device or security key alone decrypts the saved password, signs in offline, and unlocks device-unlock profiles | Small: authenticators that cannot verify stop being offered for remember-me | Yes (later the same day): the bridge reports the real UP and UV flags from the authenticator data, every ceremony requires verification, and keys are derived only when it was done (`WebAuthnRegisterResult.UserVerified`); unit tests with a fake JavaScript runtime |
| SR2 | Medium | Sync / E2E | Idle auto-lock looks profiles up by name but is given the key scope id, so it never starts for code-defined profiles (the template's `files`) | Unlocked profile keys and plaintext stay available for the whole session, including on an unattended machine | No (restores documented behaviour) | Yes: lookup by key scope; unit test with a profile whose name is not its scope id |
| SR3 | Medium | Key management | Passphrase change rewraps the same master key; file keys unchanged; old document kept as `AWSPREVIOUS` | Anyone holding an old keys document and old passphrase, or the master key, keeps reading all current and future files | Yes: key rotation on passphrase change, optional re-encryption, new text | No (functional) |
| SR4 | Medium | AWS | Data bucket not versioned; role may delete and overwrite | Leaked one-hour credentials or an engine bug permanently destroys a user's files | No (infrastructure) | Yes, not deployed: versioning, old versions expire after 30 days, incomplete uploads after 7; `remove.ps1` deletes every version |
| SR5 | Medium | AWS / UI.Sync | Keys secrets force-deleted with no recovery window; policy allows it | A hijacked session or a bug irrecoverably destroys a profile's keys, making all its files unreadable | Small: deleted keys linger unreadable for 7 days; test script changes | Yes (later the same day), not deployed: `RecoveryWindowInDays = 7` in the store, a `Deny` on forced deletion in the role, `test-auth.ps1` checks both. **Moved 2026-11-10** (`docs/plans/active/2026-11-10-protected-profile-concurrent-writes-plan.md`): the keys are now in the versioned data bucket; a delete only adds a marker, the role is denied `s3:DeleteObjectVersion` (`NoVersionPurge`), so a deleted keys document is restorable for 30 days; the deploy tests check it (`docs/plans/active/2026-11-10-deploy-tests-plan.md`) |
| SR6 | Medium | AWS / Auth | No MFA, no threat protection (in backlog) | A phished or reused password yields the tokens, AWS credentials, wrapped keys and ciphertext, and the power to destroy them | Yes: a second sign-in step and Profile page set-up | No (functional, in the backlog) |
| SR7 | Medium | CI | Actions pinned by tag, including `NuGet/login` in the publish job | A compromised action publishes malicious `Devoctomy.Blazwiz.*` packages that every app restores | No (workflow files) | Yes: every action pinned to the commit its tag pointed at (v4.4.0, v6.1.0, v4.6.2, v4.3.0, v1.2.0), with Dependabot for `github-actions` |
| SR8 | Medium | Scripts | `test-template.ps1` deletes a pre-existing `-WorkRoot` on failure | Loss of a maintainer's local folder contents | No | Yes: refuses an existing folder; deletes only one it created |
| SR9 | Low | Passkeys | PRF-derived keys cleared only by the framework login page, not on sign out | Apps with their own login page keep `rememberme` and `e2e-kek` in memory after sign out; next sign in needs no passkey prompt to unwrap | No | Yes: `DerivedKeySessionHandler` (Auth, runs last) wipes the store on sign out, including when the session ends |
| SR10 | Low | Passkeys | PRF output passed as a hex string, never zeroed in JS | Root secret of all derived keys lingers in process memory | No | Yes: bytes across interop, wiped in JavaScript after hand-over and in .NET after derivation |
| SR11 | Low | AWS | `ALLOW_USER_PASSWORD_AUTH` on the public client, for a test script | Plaintext-password path to Cognito for phishing validation and credential stuffing; docs' SRP claim is false for the client | No for the app; test script changes | Yes, not deployed: removed; `test-auth.ps1` uses `ADMIN_USER_PASSWORD_AUTH` as the deployer |
| SR12 | Low | AWS / scripts | `remove.ps1` and user deletion leave users' secrets and prefixes behind | Indefinite retention of wrapped keys and metadata; cost; a false promise in the script | No (scripts, a tag on secrets) | Yes, not deployed: secrets tagged `app=<name>` by the portal (unit test); `remove.ps1` lists users and deletes their tagged secrets; the deployer may delete tagged secrets only. Untagged earlier secrets are reported, not deleted. A single user's deletion is still by hand. **Moved 2026-11-10**: the keys are now in the data bucket, so `remove.ps1` deletes them with the bucket and the secret deletion is gone; a single user's deletion is still by hand |
| SR13 | Low | AWS | No Cognito deletion protection | `delete-stack` or a console action removes every user and their data mapping | No | Yes, not deployed: `UserPoolDeletionProtection` (`auto` = on for `prod`); `remove.ps1` turns it off first |
| SR14 | Low | AWS | Email changes unverified; client write attributes unrestricted | An access-token holder changes the sign-in email and locks the user out of sign in and recovery | No | Yes, not deployed: email verified before update; client reads `email`, `email_verified`, `name` and writes `name` only |
| SR15 | Low | Scripts | Test-user password on the CLI; plaintext temp files | Local exposure through process lists, transcripts, telemetry | No | Yes, not run: password via `--cli-input-json` from a user-only file, overwritten before deletion; the real index is no longer written to a file |
| SR16 | Low | AWS | Localhost origins in production CORS | A localhost page with stolen credentials can reach the production bucket from the browser | No | Yes, not deployed: `LocalDevOrigins` defaults to none for `prod`; `POST` dropped from the CORS methods |
| SR17 | Low | AWS | Deployer policy allows self-escalation; `AdminSetUserPassword` on every pool | Deployer identity gains admin on the account and can take over any user in any pool | No | Partly: duplicate action removed and new actions added with scoped resources or tag conditions. The permissions boundary needs the account administrator to create the boundary policy, planned in `docs/reviews/2026-11-08-security-review/backlog/deployer-permissions-boundary.md`; the tag condition on `cognito-idp:Admin*` needs testing |
| SR18 | Low | Sync / E2E | Index load replaces a code profile's whole settings object with user-profile defaults | `AllowDeviceUnlock` and passphrase rules silently change for the main profile | No (bug fix) | Yes: only an idle timeout chosen in the index is applied, over the profile's own settings; two unit tests |
| SR19 | Low | Sync / E2E | Zero-length content bypasses the encrypted-format check | S3-side truncation shows as an empty file with no error, where any other tamper is detected | No | Yes: empty non-folder content throws `InvalidDataException`; folder markers pass through |
| SR20 | Low | Sync / E2E | Index lost updates and "missing from index" wipe local keys and files | A profile's local files and keys copy are deleted on every device; keys secret and ciphertext survive but cannot be re-attached | Yes (recovery behaviour) | Yes (later the same day): before forgetting, `TryRestoreToIndexAsync` checks the keys document; if it exists the entry is put back, if the store is unreachable the profile is kept; unit tests in Encryption and Sync. The index write itself is guarded since 2026-11-10 (`docs/plans/active/2026-11-10-protected-profile-concurrent-writes-plan.md`): it is in the data bucket, written with `If-Match`, and the manager reapplies its change on a 412; unit tests in Encryption |
| SR21 | Low | Sync | Profile deletion proceeds when S3 is unreachable for credential reasons | Permanently undecryptable orphan objects in the user's prefix | Small: deletion refuses while S3 cannot be reached | Yes (later the same day): `IS3SyncTargetProvider.IsConfigured` separates "nothing set up" (`Sync.Remote.NotConfigured`, deletion proceeds) from "no credentials now" (deletion refuses); unit tests |
| SR22 | Low | Sync | Remote-supplied edit time trusted; no version binding | An S3-side actor forces its version to win or rolls files back silently on every device | No | Yes: an edit time more than 5 minutes after the upload falls back to the upload time (`RemoteObjectInfo.MaxClockSkew`); unit tests. Version binding is a design change, documented |
| SR23 | Low | Key management | Device-unlock wrapped master key has no associated data | A moved wrapped key fails late with the wrong exception; no bypass | No (existing device-unlock entries fall back to the passphrase) | Yes: the scope id is the associated data; unit test moves a wrapped key between profiles |
| SR24 | Low | Auth | Remembered passwords use the legacy stream cipher: no binding, key copied into a string | Key material outlives its use; ciphertext not bound to the user | No (saved passwords re-created at next sign in) | Yes: `IAeadCipher` with the email (passwords) or user id (labels) as associated data; `ICryptoService` and its implementation removed |
| SR25 | Low | CI | `github.ref_name` interpolated into `run:` scripts | Script injection in the job holding the publishing token, by someone who can already dispatch it | No | Yes: passed through `env:` |
| SR26 | Low | CI | Checkout persists the job token | A later step could push with `contents: write` in the release job | No | Yes: `persist-credentials: false` on every checkout |
| SR27 | Low | Build | Security analyzers at SDK defaults | Injection and crypto regressions are not caught at build time | No | Yes: `AnalysisModeSecurity` `All` in both `Directory.Build.props`; the one finding (CA5394, `Random` for test bytes) was fixed, not suppressed |
| SR28 | Low | Repo | `.gitignore` gaps for keys and production settings | A future accidental commit of a secret is not stopped | No | Yes, in both `.gitignore` files |
| SR29 | Low | Template | Title and description substituted without escaping | A quote or angle bracket breaks the generated app's JSON, HTML and tests | No | Yes: a `safeText` form strips `" < > & \`; `test-template.ps1` creates an app with all of them in its title and description |
| SR30 | Low | Key management | Stored Argon2id parameters have no upper bounds; wrong key length escapes untyped | A tampered document hangs or crashes the tab on unlock; no key exposure | No | Yes: `Parse` rejects memory over 4 GiB, more than 100 passes or 16 lanes, and a key length other than the cipher's (`IKeyWrapService.KeySize`); unit tests |
| SR31 | Low | Hosting | CSP added only by `deploy.ps1`, not by the build; `style-src 'unsafe-inline'` | Apps hosted another way ship with no CSP, so an XSS reaches tokens and keys unhindered | No (build change) | No: where the CSP is produced is a design decision, and tightening `style-src` needs a browser check against the deployed app |
| SR32 | Low | Passkeys | User handle embeds the email; `userId` parameter mismatch | Email leaks via the authenticator; a latent misuse of the register call | No | Yes: random 32-byte user handle; the parameter is gone from `IWebAuthnService.RegisterAsync` |
| SR33 | Low | Template | Note keys from the URL only prefix-checked | Odd keys inside the user's own store; no access to anything else | No | Yes: `IsNoteKey` rejects empty, `.`, `..` and control-character segments; unit tests |
| SR34 | Low | Auth | Missing Cognito settings silently select the placeholder sign in | A misdeployed app lets anyone "sign in" and read local data for the typed name; no AWS access | Small: fail fast outside development | Yes (later the same day): `AddBlazwizCognito(configuration, allowPlaceholderSignIn)` throws when Cognito is unconfigured and the placeholder is not allowed; the template and clypse pass `IsDevelopment()`; unit test |
| SR35 | Info | Sync | File names, sizes and times are plaintext in S3 and IndexedDB | Metadata visible to anyone with bucket or device access | Docs | Yes: stated in the Sync README |
| SR36 | Info | Sync | Glob matcher accepts `.`/`..`/empty segments inside the prefix; `\` differs between Windows and browser | Aliased keys; tests that pass on Windows for inputs that differ at runtime | No | Yes: such keys never match; unit tests |
| SR37 | Info | Encryption | Random nonces with no rotation trigger | Theoretical nonce-reuse bound far beyond realistic use | Only if rotation is added | Documented in the Sync README; rotation belongs with SR3 |
| SR38 | Info | Encryption | Passphrases in .NET strings | Cannot be zeroed; inherent | None | n/a |
| SR39 | Info | Sync | Whole-object buffering, no size cap | A huge object in the prefix crashes the tab | Small, if a ceiling is added | Yes (later the same day): `SyncOptions.MaxFileSize` (100 MiB, `UseMaxFileSize`); a larger remote file is reported (`Sync.Remote.TooLarge`) and not fetched; unit test |
| SR40 | Info | Encryption | Argon2id default depends on the package's build configuration | A Debug publish of this repository's template derives 19 MiB keys; published packages are Release | Docs | Yes: stated in the template README |
| SR41 | Info | UI | Framework text rendered as `MarkupString` | Overrides that are not HTML, or pseudo-localisation, break the tag | No | Yes: plain text, rendered with encoding |
| SR42 | Info | UI | Password change and passkey lookup match the saved user differently | An old password could stay encrypted in `saved_users` if username ever differs from email | No | Yes: `SavedUser.IsFor` used by both and by the login page; unit tests |
| SR43 | Info | UI.Sync | Sync log persists raw exception text for 30 days | AWS request ids and identity ids kept in IndexedDB; user's own | None | n/a |
| SR44 | Info | Supply chain | Licence files and provenance missing for vendored assets | Licence compliance; harder to verify integrity later | No | Yes: `LICENSE`, `THIRD-PARTY-NOTICES.md` (source, version, SHA-256), licence files beside each library; `*.min.js` kept byte for byte, and the SimpleWebAuthn bundle now matches upstream exactly |
| SR45 | Info | Supply chain | No lock file, `nuget.config` or source mapping | Reproducibility and substitution protection rely on CPM and defaults | No | Partly: a root `nuget.config` restores from nuget.org only. No lock file or source mapping (optional, not taken on) |
| SR46 | Info | Supply chain | Deprecated test packages | Test-only maintenance | No | No: moving to xunit v3 and MSTest 4 is churn in every test project, and Reqnroll depends on xunit 2 |
| SR47 | Info | Release | Required reviewers for the `nuget` environment live outside the repo | Cannot be verified here; release could run on one person's approval | None | n/a: GitHub settings, for the maintainer to confirm |
| SR48 | Info | AWS | No audit logging | No record of which credentials deleted or changed what after an incident | No | Yes, not deployed: `AuditLogging` (`auto` = on for `prod`) adds a CloudTrail trail of data bucket object events into an audit bucket kept a year |
| SR49 | Info | Docs / AWS | Backlog says no CI and no CSP; README mechanism drift; cross-prefix read not tested end to end; TLS 1.0 with the default certificate; HSTS without `includeSubDomains` | Misleading docs; weaker TLS floor without a custom domain | Docs; no | Yes: backlog and READMEs corrected, TLS and HSTS stated; `test-auth.ps1` also checks reading and listing another prefix and the whole bucket |

Counts: 0 Critical, 0 High, 8 Medium, 26 Low, 15 Informational. Fixed: 39 in full (33 in the first pass, then SR1,
SR5, SR20, SR21, SR34 and SR39 later the same day), 3 in part (SR17, SR37, SR45), 4 left for a decision or a plan
(SR3, SR6, SR31, SR46), 3 not applicable (SR38, SR43, SR47).

## Closing assessment

This is a well-built codebase from a security point of view. The things that are hardest to get right, and that
usually go wrong in projects of this kind, are right here: the authentication protocol is AWS's own SRP, the tenant
isolation is enforced by IAM rather than by client code, the end to end encryption uses a modern AEAD with the
profile and path authenticated so files cannot be moved or swapped, the passphrase KDF is strong and its parameters
travel with the data, keys are zeroed everywhere, the refresh token never touches storage, secrets are created with
never-overwrite semantics, the UI encodes everything it renders and sanitises the one place it renders HTML, and the
dependency set is current, pinned and free of known vulnerabilities. The documentation describes the security design
accurately and in detail, which made this review possible and will make the next one easier.

The eight Medium findings share a pattern: each is a control that is designed and documented but does not fully hold
in practice. The passkey flow assumes user verification that is never required (SR1); the idle lock is configured but
never scheduled for the main profile (SR2); a passphrase change looks like revocation but is not (SR3); the AWS
resources are well isolated but not protected from the user's own credentials being turned against their data (SR4,
SR5); and the release pipeline is carefully gated but trusts mutable tags (SR7). Four of them (SR1, SR2, SR5, SR7)
are an afternoon each, and SR4 is a few lines of YAML. SR3 and SR6 are features and belong in the plan; SR3 in
particular should be scheduled before 1.0, because users will expect a passphrase change to mean what it usually
means. The project is unreleased, so none of the fixes that invalidate stored formats (SR23, SR24) need a migration.

Suggested order: SR1 and SR2 first (both are the framework not doing what it says, and both are small), then SR4,
SR5 and SR7 (cheap, and they bound the damage from everything else), then SR9, SR11, SR18 and SR19 as a hardening
batch, then plan SR3 and SR6. The Informational items are for the README and the threat-model section this review
suggests adding.

**After the fixes of 2026-11-08** (see "What was fixed"), including the second pass that did the small functional
changes (SR1, SR5, SR20, SR21, SR34, SR39), what remains open is: SR3 (key rotation on passphrase change, planned in
`docs/plans/active/2026-11-10-key-rotation-plan.md`), SR6 (MFA, blocked by the session model as
`docs/reviews/2026-11-08-security-review/backlog/mfa-totp.md` explains), SR31 (where the CSP is produced, `docs/reviews/2026-11-08-security-review/backlog/csp-in-build.md`),
SR17's permissions boundary (an account administrator's task, `docs/reviews/2026-11-08-security-review/backlog/deployer-permissions-boundary.md`), SR46 (test package churn), and the deploy that puts the
stack changes into effect. The next review should start from this table.
