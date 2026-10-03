# Code review: blazwiz (branch `e2e2ncryption`)

**Date:** 2026-10-03
**Scope:** whole solution, with most attention on the new end to end encryption work and the code it relies on:
`blazwiz.Core` (Cryptography, Encryption, Sync), `blazwiz.Portal.Application` (auth, key stores, view models),
`blazwiz.Portal` (JS interop, `index.html`) and `cloudformation/stack.yaml`.

**How this review was done:** by reading the source only. No builds, tests or git commands were run, so nothing
here has been confirmed by running it. Where a finding needs a quick check first, it says so.

## Summary

| Severity | Count | Meaning |
|---|---|---|
| High | 2 | Fix before E2E encryption ships: leaks plaintext or allows script injection |
| Medium | 12 | Real defects or risks that can lose data or weaken security in plausible situations |
| Low | 14 | Hardening, edge cases, and tidying |
| Maintainability | 8 | Design and readability, no defect today |

The architecture is strong: clear layering, injectable services, AEAD everywhere with the header as associated data,
key ids bound into the wrapped keys, conditional S3 writes, zeroing of key material, and a large test suite. Most
findings are about **state changing on another device or tab** and **what happens when something fails part way**.
Those are the hardest cases in an offline-first sync engine with encryption.

---

## 1. Security

### H1 (High). A device that is not set up keeps uploading plaintext after encryption is turned on elsewhere

**Status: done (2026-10-03).** `ISyncGate` is now async (`CanSyncAsync`). Before every pass of an encrypted
profile, `E2eSyncGate` re-reads keys.json while the status is `Disabled`, `Unavailable` or `Unknown`. If encryption
was turned on elsewhere, the device moves to `NeedsSetup` or `Blocked`, refuses writes and skips the pass. Edits made
while it thought encryption was off stay on the device until it is set up, and setup encrypts them before they sync.
A two-device unit test covers this (`E2eSyncGateTests`). Caveats:
- A split-second race remains: encryption turned on elsewhere between this device's check and its upload in the same
  pass. With one user controlling every device this is accepted.
- It costs one Secrets Manager call per pass while encryption is off. This is not throttled (see I6).
- Not done: refusing plaintext over `Encrypted` index records (would miss this scenario and block a legitimate
  disable), re-checking on downloaded E2EE headers, and an `@aws` two-device scenario.
- Future: AppSync Events to tell open devices straight away when keys.json changes, instead of at their next pass.

**Where:** `blazwiz.Core/Sync/Encryption/E2eSyncGate.cs:23-26`, `blazwiz.Core/Encryption/E2eKeyManager.cs:76-143`,
`blazwiz.Core/Sync/Encryption/SyncEncryptionService.cs:330-345`

`E2eSyncGate.CanSync` lets encrypted profiles sync while the status is `Disabled`. The status is only worked out in
`InitializeAsync`: at sign in, on passkey unlock, and when coming back online while `Unknown`. `IsEnabledAsync` is only
called while enabling. Nothing tells **another device** that keys.json now exists, because `SyncStorageChannel` only
reaches tabs on the same browser.

Scenario:
1. Device B signs in with encryption off. Its status is `Disabled` and its write mode is `Plaintext`.
2. Device A enables encryption, encrypts every file and uploads the ciphertext.
3. B keeps running. New notes and edits on B are written in plaintext and uploaded. If B saves a note it already had
   open, the remote ETag has changed, so the save becomes an edit/edit conflict. Newest wins, so B's **plaintext can
   replace A's ciphertext in S3**.
4. A's `EncryptArrivalsAsync` encrypts those files again later, but the plaintext has already been stored in S3.

**Recommendation**
- Re-check keys.json before every pass of an encrypted profile while the status is `Disabled`. It is one Secrets
  Manager call, which could be throttled to once per N minutes.
- Also re-check when the engine sees encrypted content. `SyncEngine.SaveSyncedAsync` already reads the key id from
  the header. If a download carries an E2EE header while the status is `Disabled`, set the write mode to `Refuse` and
  call `InitializeAsync`.
- Defence in depth: in `SyncEngine.UploadAsync`, refuse to upload plaintext over a remote object whose index record
  says `Encrypted`.
- Add an integration scenario for this (two `SimulatedDevice`s, one enables, the other edits).

### H2 (High, verify first). Markdown preview may allow script injection through Markdig generic attributes

**Status: done (2026-10-03).** Confirmed first: a test showed `{onerror=...}`, `{onclick=...}` and
`{onmouseover=...}` coming out as event handler attributes on images, links and headings. `MarkdownRenderer` now
builds an explicit pipeline (auto identifiers, pipe and grid tables, task lists, emphasis extras, list extras,
footnotes, definition lists, auto links) without generic attributes, and the test passes
(`MarkdownRendererTests.GivenGenericAttributes_WhenToHtml_ThenNoEventHandlerIsRendered`). Caveats:
- Notes is a demo feature. The renderer is behind `IMarkdownRenderer`, so a real notes app can register its own,
  e.g. one that also sanitises the HTML with HtmlSanitizer.
- The preview no longer supports `:::` custom containers or media link embeds.
- Remote images still load when a note is previewed, and there is still no CSP (M2).

**Where:** `blazwiz.Portal.Application/Services/MarkdownRenderer.cs:16-19`, `blazwiz.Portal/Pages/NoteEditor.razor:113`

The pipeline uses `UseAdvancedExtensions()`, which includes the **GenericAttributes** extension (`{#id .class key=value}`).
`DisableHtml()` turns off raw HTML blocks but does not limit generic attributes. Something like
`![x](https://x){onerror=alert(1)}` may come out as `<img ... onerror="alert(1)">`. The preview injects it with
`MarkupString`, so event handler attributes would run.

**Impact:** script in the app origin can read the Cognito tokens, including the refresh token, from `localStorage` (M5).
It can also call into the running app while keys are unlocked. Notes sync between devices, so a note planted by anyone
who can write to the user's S3 prefix runs on every device. With encryption on, an attacker cannot plant valid
ciphertext. Without it, they can.

**Recommendation**
- Write a unit test with the payload above. If it fails, switch to an explicit pipeline (`UsePipeTables()`,
  `UseTaskLists()`, `UseEmphasisExtras()`, `UseAutoLinks()`, …) without `UseGenericAttributes()`.
- Also consider whether remote images should load at all. A remote `![](https://…)` image fetches a third-party URL
  when the note is previewed.
- Add a CSP (M5) as a second barrier.

### M2 (Medium). Remembered passwords and refresh tokens are exposed to any script in the origin

**Where:** `blazwiz.Portal.Application/Services/CognitoAuthenticationService.cs:31,255`, `LoginViewModel.cs` (`blazwiz_saved_users`),
`blazwiz.Portal/wwwroot/index.html`, `cloudformation/stack.yaml:114`

The session, including a 30 day refresh token, sits in `localStorage`. The Cognito password is stored there encrypted
with the PRF-derived key. That is common for a static SPA, but it means XSS gives full account takeover. The CloudFront
managed `SecurityHeadersPolicy` sends **no Content-Security-Policy**, and `index.html` loads Google Fonts from a third
party.

**Recommendation**
- Add a custom CloudFront response headers policy with a CSP. Blazor WASM needs `'wasm-unsafe-eval'`. Restrict
  `connect-src` to Cognito, S3 and Secrets Manager endpoints, `script-src 'self'`, `img-src 'self' data:` (see H2),
  and `frame-ancestors 'none'`.
- Self-host the Inter font. That removes a third-party request on every load and tightens the CSP.
- Longer term: consider shorter refresh token validity, or a token handler (backend for frontend) if the threat model
  grows.

### M3 (Medium). KDF settings from keys.json are not bounds-checked

**Where:** `blazwiz.Core/Encryption/E2eKeyManager.cs:316-349` (`Parse`), `:411`; `BouncyCastleArgon2idKeyDerivationService.cs:31-34`

`Parse` checks the version, algorithm and active key count, but not `memory_kib`, `iterations`, `parallelism` or
`key_len`.
- No upper bound. A corrupted or tampered document (e.g. `memory_kib: 2000000000`) makes every device try a huge
  allocation in WASM and crash or hang at unlock.
- No lower bound beyond the BouncyCastle minimums. Documents made by **Debug builds (19 MiB)** stay that weak until the
  passphrase is changed. That is fine for development, but a dev stack's data is easy to forget about.
- `key_len` other than 32 fails later with an `ArgumentException` from the cipher, not a clear `InvalidDataException`.

**Recommendation:** validate in `Parse`: memory between, say, 19 MiB and 1 GiB, iterations 1–10, parallelism 1–4,
`key_len == 32`, and a salt of at least 16 bytes. Consider an "upgrade KDF" prompt when a document is below the
current `Argon2idOptions`.

### M4 (Medium). Changing the passphrase can silently lose an update

**Where:** `E2eKeyManager.cs:236-268`, `SecretsManagerUserKeysStore.cs:41-66`

`ChangePassphraseAsync` reads keys.json, re-wraps it and writes it back with an unconditional `PutSecretValue`. If two
devices change the passphrase at the same time, or a later key rotation edits `keys`, the last write wins. The other
user ends up believing a passphrase that no longer works. There is no recovery by design, so this means losing data.

**Recommendation:** use optimistic concurrency. Read the `VersionId`, and before putting, check with `DescribeSecret`
that `AWSCURRENT` is still that version, then put with a `ClientRequestToken`. This narrows the window but does not
close it fully. Alternatively, store a `revision` in the document and check it again just before writing. Once
saved, read it back and confirm.

### M5 (Medium). Logging out does not explicitly wipe the unlocked keys

**Where:** `blazwiz.Portal.Application/ViewModels/MainLayoutViewModel.cs:425-434`, `LoginViewModel.cs:240`

`LogoutAsync` stops sync, signs out and clears AWS credentials. It does **not** clear `IDerivedKeyStore` or
`IE2eKeyRing`. The keys are only wiped because `/login`'s `OnInitializedAsync` calls `keyStore.Clear()`. If the
login page fails to initialise, or someone adds another route out, the master and file keys stay in memory. The
key ring's `WriteMode` and `activeKeyId`, and `E2eKeyManager.status` (`Unlocked`), also carry over to the next user
in the same tab until `InitializeAsync` runs.

**Recommendation:** add an explicit "sign out" step to `ISyncEncryptionService` or `IE2eKeyManager`:
`keyRing.Clear(Plaintext)`, `derivedKeyStore.Clear()` and status reset to `Unknown`. Call it from `LogoutAsync`.

### M6 (Medium, was H3 High). A cancelled Web Lock request can hold the sync lock until the tab closes

**Status: done (2026-10-03).** Dropped to Medium: it is a bug that needs two tabs, one waiting for the other's pass,
and the waiting one cancelling (e.g. sign out). `WebLockSyncLockService` now gives each request a GUID id up front.
When `ct` cancels the wait, it calls a new `cancelLock(requestId)` in `network.js`, which aborts the request through
an `AbortSignal` if it is still queued, or releases the lock straight away if it was granted after .NET gave up. GUIDs
are used because the service is scoped while the JS module (and its locks) is shared by the whole page.
Tests: three new `WebLockSyncLockServiceTests` (unique ids, cancel reaches the browser, page gone while cancelling).
The JS was also checked in Playwright's Chromium with a scratch page (not in the repo): a cancelled queued request
rejects with `AbortError`, a late grant is released, and the lock is free afterwards in both cases. Caveats:
- Not done: a maximum hold time for a pass, so a hung S3 call cannot block other tabs. That is a separate issue.
- The JS has no automated test in the repo. The Playwright UI suite only covers the normal lock path.

**Where:** `blazwiz.Core/Sync/WebLockSyncLockService.cs:18-25`, `blazwiz.Core/wwwroot/js/network.js:47-60`

`AcquireAsync` passes `ct` to `InvokeAsync`. Cancelling it only abandons the .NET await. The `navigator.locks.request`
call in JS stays queued. When it is later granted, `acquireLock` stores a release function in `heldLocks` and resolves
an id that no .NET code will ever release. The lock then stays held until the tab closes. **Every other tab's sync for
that profile waits forever**, and the scheduler's `Running` flag in this tab is never cleared for the next try.
Cancellation happens on sign out (`SyncScheduler.StopAsync`) and on any cancelled `SyncEncryptionService` operation.

**Recommendation**
- Pass an `AbortSignal` to `navigator.locks.request(name, { signal }, …)`, and add a `cancelLock(requestId)` JS
  function that .NET calls when `ct` fires (`ct.Register`).
- Alternatively, have JS take a request id up front. If the lock is granted after .NET has given up, release it
  straight away.
- Consider a maximum hold time for a pass, so a hung S3 call cannot block other tabs for good.

### L1 (Low). Ciphertext is not bound to its file path

**Where:** `blazwiz.Core/Encryption/EncryptedFileFormat.cs:52-61`

The associated data is the header (magic, version, key id, nonce), but not the S3 key. Someone who can write to the
user's prefix can swap two encrypted notes, or put back an older ciphertext of the same note. Both decrypt fine. The
WIP doc already accepts that paths leak; this is about **integrity**, not confidentiality. Renames already rewrite
the content (`NoteService.MoveKeysAsync`), so binding the key would cost almost nothing.

**Recommendation:** at format version 2, add the profile name and file key to the associated data, keeping v1
readable.

### L2 (Low). WebAuthn details

**Where:** `webauthn-bridge.js:84,146,215`
- `user.id` is `username + '-' + Date.now()`, which puts the email address in the authenticator's user handle. The spec
  advises random bytes with no personal data. Use 32 random bytes.
- The PRF salt is a generic constant (`'WebAuthn PRF Registration Salt'`). HKDF already separates keys per app, so this
  is safe, but an app-specific salt would make the separation clear at the authenticator level too.
- Note for template users, not a defect in the target setup (a FIDO2 card that asks for touch and PIN every time, and
  phone passkeys behind biometrics): ceremonies use `userVerification: 'preferred'` (`:108,134,203`). A security key
  enrolled **without** a FIDO2 PIN would unlock with a touch alone. `'required'` would turn such keys away, if that
  matters to whoever uses the template.

### L3 (Low). Minimum passphrase length is 8

**Where:** `blazwiz.Portal.Application/ViewModels/E2eSettingsViewModel.cs:20`

Anyone who gets keys.json (AWS admins, or anyone with the user's credentials) can attack the passphrase offline. The
Cognito password policy already asks for 12 characters. Recommend at least 12, or a strength estimate (zxcvbn-style),
with the existing no-recovery warning.

### L4 (Low). Turning encryption on leaves earlier plaintext behind

When encryption is enabled, files that are only remote are encrypted "when they arrive". Until then, their plaintext
stays in S3. If bucket versioning is turned on (see I2), all earlier plaintext versions stay too. Document this in the
enable dialog, or delete non-current versions after a successful enable.

---

## 2. Correctness and data integrity

### M7 (Medium). A corrupt keys.json fails silently and leaves writes in plaintext

**Where:** `E2eKeyManager.cs:115`, `MainLayoutViewModel.cs:485-493`

`Parse` throws `InvalidDataException` from `InitializeAsync` before the key ring mode is set. The caller swallows it
("status stays unknown"). The gate does hold back sync for `Unknown`, which is right. But the key ring keeps its
default `Plaintext` mode, so new local edits are stored unencrypted. Nothing tells the user that their keys document
is unreadable. `OnConnectivityChanged` only retries when connectivity changes.

**Recommendation:** in `InitializeAsync`, catch `InvalidDataException`, call `keyRing.Clear(E2eWriteMode.Refuse)`,
and set a distinct status (e.g. `Corrupt`) that the banner can explain.

### M8 (Medium). An offline token refresh signs the user out

**Where:** `blazwiz.Portal.Application/Services/CognitoAuthenticationService.cs:203-207`

The comment says "keep the stored session", but `GetCurrentUserAsync` returns `null`, which callers treat as signed
out. Offline mode is handled (`offlineModeStore`). But a real network outage once the ID token has expired (60 minutes)
sends the user to `/login` in an offline-first app, and `/login` then wipes the PRF keys.

**Recommendation:** return the stale session, perhaps marked as expired, and let AWS calls fail and retry through
the connectivity service. Also make refresh single-flight: several callers can currently refresh at once.

### M9 (Medium). Edit conflicts silently drop the losing version and depend on device clocks

**Where:** `blazwiz.Core/Sync/NewestWinsConflictResolver.cs:28`, `SyncEngine.cs:213-252`

- The winner is chosen by comparing `modifiedAt` values from **two different device clocks**. A device whose clock
  runs ahead always wins. Anything in `x-amz-meta-modified-at` set in the future wins every conflict.
- The losing side is overwritten with no copy kept. For notes, an edit/edit conflict means silently losing the
  older edit.
- **With E2E on**, `CompareContent` compares ciphertext hashes. Two devices that encrypt the same plaintext produce
  different ciphertext (random nonce), so "same on both sides, adopt" never happens. Every lost index or first sync of
  an existing file becomes a conflict.

**Recommendation**
- Keep the loser as a conflict copy (`Note (conflict 2026-10-03 device).md`) instead of discarding it.
- For encrypted files in `CompareContentAsync`, compare **plaintext** hashes when the keys are available. Or store a
  keyed plaintext hash (HMAC with a key derived from the file key) in object metadata.
- Clamp remote `ModifiedAt` to no later than the S3 `LastModified` plus a small skew.

### M10 (Medium). A local edit can be overwritten during a download

**Where:** `blazwiz.Core/Sync/SyncEngine.cs:198-206`

`DownloadAsync` checks `LocalChangedSince(action.Record, current)` and then awaits `FileStore.WriteAsync`. Blazor WASM
interleaves at every `await`, so an editor save between the check and the write is overwritten by the download, and
`SaveSyncedAsync` then sees the changed revision and leaves the record dirty with the remote content on disk.
`DeleteLocalAsync` (`:276-290`) has the same gap.

**Recommendation:** serialise local writes and engine writes per key. A per-profile `SemaphoreSlim`, or an async
lock that `TrackingSyncFileStore` and the engine both take, is enough. Or check the revision again after the write
and restore the local content if it changed.

### L5 (Low). Turning encryption off from a state that is not unlocked fails with a misleading error

**Where:** `SyncEncryptionService.cs:145-180, 265-289`

The UI only allows this when `CanManage` (unlocked), so it cannot happen today. But `DisableAsync` itself does not check:
- `SyncEncryptedProfilesAsync(requireComplete: true)` treats a profile that the gate **skipped** as complete
  (`SyncService` returns an empty result with no errors).
- If decryption then fails, `keyRing.SetWriteMode(Encrypt)` at `:170` throws `InvalidOperationException` when there
  is no active key, which hides the original exception.

**Recommendation:** guard with `keyManager.Status == Unlocked` at the top. Make `SyncService.SyncAsync` report profiles
it skipped (e.g. `SyncResult.Skipped`), so `requireComplete` can fail on them.

### L6 (Low). `SyncService` reports Idle when every profile was skipped

**Where:** `blazwiz.Core/Sync/SyncService.cs:60-66, 98-109`

When gates skip every profile, the status becomes `Idle` with no error. The user cannot tell "up to date" from
"waiting on encryption". The E2E banner covers most of this, but the sync pill and Sync page are misleading.

### L7 (Low). Edge cases in the folder store

**Where:** `blazwiz.Core/wwwroot/js/filetree.js:8-15`, `FolderSyncFileStore.cs`
- On Windows and macOS file systems, `notes/a.md` and `notes/A.md` are one file but two S3 keys, so one overwrites
  the other.
- Windows-reserved names (`CON`, `NUL`, trailing dots or spaces, `<>:"|?*`) coming from another device fail on write
  and keep failing every pass.
- With encryption on, the user's chosen folder holds **ciphertext**, which may surprise people who picked a folder to
  see their notes. Say so in Settings.

**Recommendation:** detect case-only collisions in `ListAsync`/planning and report them. Percent-encode reserved
characters in `encode()`, as is already done for `%` and `\`.

### L8 (Low). Renaming or moving a folder is not atomic

**Where:** `blazwiz.Portal.Application/Services/NoteService.cs:265-280`

Each file is written to its new key and then deleted from the old one. A failure part way (quota, `E2eLockedException`
in `Refuse` mode) leaves some files in both places with no way to resume. Write everything first, then delete, and
report a partial move to the user.

---

## 3. Concurrency and reliability

### M11 (Medium). Many failures are swallowed with no record

**Where (examples):** `SyncEncryptionService.cs:303` (`EncryptArrivalsAsync`), `SyncScheduler.cs:208`,
`MainLayoutViewModel.cs:478-493`, `SyncEngine.WriteLogAsync`, `LoginViewModel.SaveUserAsync` (`catch { }`)

There is no `ILogger` anywhere in the non-test code. Unlocking, re-encrypting arrivals, recovering a storage migration
and starting the scheduler all fail silently. For encryption especially, "files that arrived as plaintext could not be
re-encrypted" should not vanish.

**Recommendation:** inject `ILogger<T>` (the browser console logger is built in for WASM). Log at Warning in each
swallowing `catch`. Also write E2E failures to the sync event log so they show on the Sync page.

### M12 (Medium). The data bucket and identity resources are deleted with the stack

**Where:** `cloudformation/stack.yaml:165-166, 229-230, 282-283`

`DataBucket`, `UserPool` and `IdentityPool` have `DeletionPolicy: Delete`. `stack-policy.json` only protects against
**updates**, not stack deletion. Deleting the stack (e.g. the wrong environment in `remove.ps1`) removes every user,
and the identity ids that name every user's data prefix. CloudFormation will not delete a non-empty bucket, but it will
delete the pools.

**Recommendation:** use `DeletionPolicy: Retain` on all three, plus `DeletionProtection: ACTIVE` on the user pool, at
least when `Environment` is `prod`.

### L9 (Low). `isBusy` only guards the current tab

**Where:** `SyncEncryptionService.cs:223-239`

Two tabs can both start disable or enable at once. Enabling is protected by `TryCreateAsync`. Disabling in two tabs
runs two decrypt sweeps and two `RemoveAsync` calls at the same time. Take a Web Lock (e.g. `blazwiz-e2e`) around
`RunExclusiveAsync`.

### L10 (Low). IndexedDB version upgrades can hang

**Where:** `blazwiz.Core/wwwroot/js/indexeddb.js:13-34, 76-96`

`openDatabase` has no `onblocked` handler. A tab running an **older build** without the `onversionchange` close
(possible right after a deploy, before the update banner is clicked) blocks the upgrade forever, along with every
queued operation after it. Handle `onblocked` by rejecting after a timeout and showing "close other tabs".
`getLastByIndex` also has no `onabort`.

### L11 (Low). S3 objects are read whole into memory

**Where:** `blazwiz.Core/Sync/S3/S3SyncRemoteStore.cs:75-86`

`GetAsync` copies the whole object into a `MemoryStream`, then `ToArray()` doubles it. The matcher limits this to
`*.md` today, but the sync engine is meant to be reusable "for any file type". Add a maximum size check (from
`RemoteObjectInfo.Size`) before downloading.

---

## 4. Infrastructure (CloudFormation)

### I1 (Medium). No CSP
See M2.

### I2 (Medium). The data bucket has no versioning

There is nothing to recover from a sync bug, a newest-wins overwrite (M9), or deletion by stolen credentials. Turn on
versioning with a lifecycle rule that expires non-current versions after 30 days, and see L4 for the trade-off with
encryption.

### I3 (Low). Development settings in every environment

- `ALLOW_USER_PASSWORD_AUTH` (`stack.yaml:267`) is enabled for every environment, but only `test-auth.ps1` needs it.
  Add a condition that keeps it out of prod.
- `LocalDevOrigins` defaults to the localhost origins and applies to prod too. CORS is not an auth boundary for SigV4,
  but default it to empty for prod.

### I4 (Low). No MFA on the user pool

`MfaConfiguration` is not set. The pool is invite-only and E2E protects file contents, but account takeover still
exposes metadata and allows deleting data. Consider `OPTIONAL` with TOTP.

### I5 (Low). Least privilege on the keys secret

The role grants `UpdateSecret` and `RestoreSecret`. `RestoreSecret` is never needed, because deletes are forced.
`UpdateSecret` lets the client change the secret's KMS key or description. Remove both if unused.

### I6 (Information). Secrets Manager cost grows with users

Each user's keys.json is a secret at about USD 0.40 a month, plus API calls. That is fine for an invite-only app,
but worth writing in the README for anyone using the template. If H1 adds a check every pass, cache or throttle it to
keep the API call count down.

---

## 5. Performance

### P1 (Low). Argon2id at 512 MiB on the WASM UI thread

**Where:** `blazwiz.Core/Cryptography/Configuration/Argon2idOptions.cs:14`, `BouncyCastleArgon2idKeyDerivationService.cs:36-56`

512 MiB is well above the OWASP baseline (19–46 MiB). It needs a WASM heap that large, which can fail on low-memory
phones and iOS Safari tabs. It also runs as one blocking call: `Task.Yield()` only lets the UI paint once before the
work starts. Measure on a low-end Android phone and an iPhone. 64–256 MiB with more iterations may give similar
strength more reliably. Consider running it in a Web Worker, or with multithreaded WASM once that is available.

### P2 (Low). `HasKeyEncryptionKey` and `CanDecrypt` copy keys just to check they exist

**Where:** `E2eKeyManager.cs:61-73`, `SyncEncryptionService.cs:197-208`

Each call clones the key and then zeroes the copy. Add `bool HasKey(string name)` to `IDerivedKeyStore`.

---

## 6. Maintainability

| # | Where | Recommendation |
|---|---|---|
| D1 | `ICryptoService` / `BouncyCastleAesGcmCryptoService` vs `IAeadCipher` / `BouncyCastleAesGcmAeadCipher` | Two AES-GCM implementations. The older one takes base64 key **strings** (which cannot be zeroed), copies the ciphertext and tag back and forth for no reason, and its doc mentions a `NativeAesGcmCryptoService` that does not exist (`BouncyCastleAesGcmCryptoService.cs:13`). Rebuild `PasswordCryptoService` on `IAeadCipher` with a version byte, and remove `ICryptoService`. |
| D2 | `E2eKeyManager`, `E2eKeyRing`, `SyncEncryptionService` | `status` and `isBusy` are unsynchronised fields. That is fine while WASM is single threaded. Note the assumption in a comment, or use `Interlocked`/`Lock` as `E2eKeyRing` already does, before multithreaded WASM arrives. |
| D3 | `async void` handlers (`SyncScheduler`, `SyncEncryptionService`, `ConnectivityService`, several view models) | All are wrapped in try/catch, which is good. A small shared pattern (fire and forget with logging, via an injectable service rather than a static helper) would remove the repetition and give a single place to log (M11). |
| D4 | `E2eKeyManager.TryUnlock` (`:427-452`) | Only catches `CryptographicException`. A corrupt `WrappedMasterKey` in local storage (bad base64) throws `FormatException` from `Unwrap` and fails `InitializeAsync`. Catch `FormatException` as well, and treat it as `NeedsSetup`. |
| D5 | `SecretsManagerUserKeysStore.WithClientAsync` | Signals "not supported" with `InvalidOperationException`, which `E2eKeyManager.InitializeAsync` maps to `Unsupported`. Any other `InvalidOperationException` from the AWS SDK path would be misread the same way. Use a dedicated exception type. |
| D6 | `S3SyncRemoteStore.CallAsync` (`:201-239`) | Only `HttpRequestException` from the target provider is mapped. `CognitoException` (e.g. refresh token revoked) escapes as a per-file failure, not `SyncAccessDeniedException`. |
| D7 | `blazwiz.Portal.UITests/TestResults` | Holds a 181 MB hang dump from 2026-09-29. It is ignored by git but worth deleting locally. |
| D8 | Repository | There is no CI workflow. With four test suites, a GitHub Actions workflow that builds with `TreatWarningsAsErrors`, runs unit and integration tests (non-`@aws`) and the Playwright suite would catch regressions in the template and its packed variants (`templatepack/test-template.ps1`). |

---

## 7. What is working well

- **Clear layers and testable design.** Core/Application/Models/Portal separation, interface-first services and
  view models with no browser dependency. Almost every class has a matching test file.
- **Sound crypto building blocks.** AES-256-GCM with a random 96-bit nonce for every encryption. The whole file header
  is the associated data. File keys are wrapped with their id as associated data, so entries cannot be swapped.
  Argon2id settings are stored with the document. Key material is zeroed promptly, and the key store hands out copies.
- **Careful sync.** Conditional `If-Match`/`If-None-Match` writes, a retry and replan on precondition failures, a
  revision counter that guards against edits during a pass, tombstones, and a verify pass after sign in.
- **Defensive E2E rollout.** `TryCreateAsync` never replaces another device's keys. `Refuse` write mode stops plaintext
  when the state is unclear. Enabling and disabling run in offline mode, and sweeps can resume.
- **Infrastructure.** OAC-only portal bucket, TLS enforced, per-identity IAM scoping, principal tags for secret names,
  token revocation, and invite-only sign up.

---

## 8. Suggested order of work

1. ~~**H1**: re-check keys.json while `Disabled`, and stop plaintext uploads over encrypted records.~~ Done (see H1 status).
2. ~~**H2**: test the Markdig generic attributes payload, and change the pipeline if it fails.~~ Done (see H2 status).
3. ~~**M6** (was H3): make Web Lock acquisition cancellable.~~ Done (see M6 status).
4. **M5, M7**: clear keys on logout, and handle a corrupt keys.json.
5. **M2 / I1**: CSP via a CloudFront response headers policy, and self-host fonts.
6. **M12, I2**: retain the data bucket and pools, and turn on versioning.
7. **M3, M4**: bounds-check KDF settings, and add a concurrency check for keys.json writes.
8. **M9, M10**: conflict copies, plaintext-aware comparison, and per-key write serialisation.
9. **M11, D1–D8**: logging, consolidate the AES-GCM services, and CI.