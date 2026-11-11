---
title: v1 feature ideas
type: backlog
status: ideas
added: 2026-10-05
reason: gaps found in a read of the repository, for choosing what goes into v1
areas: [template, packages]
---

# v1 feature ideas

Suggestions for what the blazwiz template could add before v1, from a read of the repository on 2026-10-05
(README, project layout, manifest, service worker, update script, `.csproj` and the code review in
`docs/reviews/2026-10-03-code-review.md` (removed)). Nothing here has been built or run.

The foundations are already strong: SRP sign-in, an offline-first sync engine, E2E encryption with passkey unlock,
an update banner, persistent storage and a CloudFormation deploy. These items are the gaps.

## Suggested v1 scope

1. Password flows plus MFA
2. CI
3. CSP and bucket versioning
4. Install prompt
5. AOT profile
6. Logging and an error boundary
7. Remaining review items M8 to M10

Share target, push notifications, localization and a small second sample feature could follow in v1.x.

## Must-haves for v1

### Self-service password flows
No forgot-password, change-password or account-recovery UI was found. Cognito supports all three and every real app
needs them. Related:
- An optional self-sign-up toggle in `stack.yaml` (the pool is invite-only today).
- MFA (TOTP) on the user pool (review item I4).

Use the official `Amazon.Extensions.CognitoAuthentication` library, never hand-rolled crypto.

### CI and release pipeline
Done: `.github/workflows/ci.yml` builds and runs the four test suites and `templatepack/test-template.ps1` on every
push; `release.yml` publishes (see `docs/releasing.md`). Deploying is still by hand.

### Security headers and CSP
Done: `deploy.ps1` adds a Content-Security-Policy to the published `index.html` (I1), and the data bucket is versioned
(I2), both from the security review of 2026-11-08 (`docs/reviews/2026-11-08-security-review/security-review.md`). What is
left from that review is in its summary table.

### Real performance work
AOT is commented out in `blazwiz.Portal.csproj`. For a "high performance" template:
- A documented AOT / trimming profile, or an `-Aot` switch in `deploy.ps1`.
- Brotli precompression served through CloudFront.
- A lightweight loading splash shown before the WASM runtime boots.
- Argon2id moved off the UI thread (review item P1).

### Syncing without end to end encryption: the app developer's choice (not implemented yet)
Decided 2026-11-03. Whether synced files are end to end encrypted is decided by the developer of the app, never by its
users. Syncing without encryption may be allowed later, as an explicit, warned-about developer choice; until then it is
not supported.

**Why it is not advisable.** Without E2E, files sit in S3 as plain text. Anyone who can read the data bucket can read
them: whoever runs the AWS account, anyone holding leaked or over-broad credentials, and every backup, replica or
version of the bucket, long after the files are deleted. Access control (IAM, the per-user prefix) is the only
protection, and one misconfiguration exposes every user's files. With E2E only the user's own devices can read them.

**Where it stands today (conflicts with the decision).**
- Encryption is opt-in per user: a profile marked `UseEncryption()` syncs plain text until a user turns E2E on in
  Settings (`E2eSettingsViewModel`), and users can turn it off again. `E2eSyncGate` lets plain text through while E2E
  is `Disabled`.
- There is no template option to leave encryption out (`--Encryption` from D6 is not offered). The template docs say
  why.

**To build.**
1. Encrypted profiles always encrypt: E2E is set up on first sign in (passphrase, passkey), nothing syncs until it
   is, and Settings has no "turn off". `E2eSyncGate` holds back encrypted profiles while E2E is `Disabled`.
2. A profile that is not encrypted is a developer opt-out with a clear name (e.g. `UnencryptedRemote()` rather than
   leaving out `UseEncryption()`), a build-time warning or analyzer, a warning in the docs, and a startup log
   message.
3. Then the template option: `--Encryption false` creates an app whose profiles opt out, with the warning in its
   README, and the encryption UI (banner, settings, Argon2id panel, passkey key) left out. The sync module must then
   work without the E2E services registered.
4. Tests: the sync module with and without encrypted profiles; the template combination in `test-template.ps1`.

## Native-like capabilities

None of these appear to be present.

- **Install prompt**: capture `beforeinstallprompt` and show an "Install app" button in Settings, with iOS "Add to
  Home Screen" instructions. The app already detects the installed state for storage persistence.
- **Web Share and Share Target**: `share_target` in the manifest so users can share text or files into the app (for
  example into a new note), and `navigator.share` for sharing out.
- **Push notifications and Badging API**: Cognito identity plus SNS or Web Push. Larger, so could be an optional module.
- **Background Sync / Periodic Sync**: sync when the app is not open, where the browser supports it. The README roadmap
  already lists timer based sync.
- **Manifest extras**: app shortcuts (long-press icon), `screenshots` for the richer install UI, `display_override`
  (for example `window-controls-overlay`), and a `file_handlers` entry for `.md` files.
- **Small native conveniences**: Clipboard service, Wake Lock, haptics via `vibrate`, safe-area insets with
  `viewport-fit=cover`.
- **Biometric app lock**: WebAuthn / passkeys are already used for E2E unlock, so an "app lock" after inactivity is a
  natural extension.

## Template ergonomics and developer experience

- **Observability**: one `ILogger` backed logging abstraction with a pluggable sink (console, optionally CloudWatch
  RUM or Sentry), and a global error boundary page. Review item M11 notes many failures are swallowed with no record.
- **Localization**: `IStringLocalizer` and culture switching. Painful to retrofit later.
- **Feature flags**: a small typed mechanism so template users can switch modules on and off at runtime, alongside the
  existing `--Include*` template options.
- **A non-Notes sample feature**: a tiny CRUD example (a todo list, say) with a single sync profile, showing how to
  build on the sync engine without the markdown baggage.
- **Accessibility pass**: skip link, focus management and ARIA check on the layout, plus axe checks in the Playwright
  tests.
- **Multi-environment support**: `-Environment` already exists. Document a dev / staging / prod flow with
  per-environment `appsettings`.

## Hardening items already identified

From `docs/reviews/2026-10-03-code-review.md` (removed), worth finishing before v1:

- **M8**: an offline token refresh signs the user out.
- **M9**: edit conflicts silently drop the losing version and depend on device clocks. Custom time sync (NTP) is not
  the fix: browsers cannot speak NTP, offline devices cannot reach a time source, and an accurate clock still leaves
  "newest wins" discarding the older edit. Instead:
  - Keep the loser as a conflict copy (`Note (conflict 2026-10-03 device).md`), so nothing is lost whichever clock is
    right. Once conflicts are never destructive, clock accuracy only decides which copy is the main one.
  - Clamp a remote `modifiedAt` to no later than S3's `LastModified` plus a small skew, so a future-dated clock cannot
    win every conflict. Optionally estimate the device's offset from S3's `Date` header on each sync.
  - With E2E on, compare plaintext hashes (or a keyed plaintext hash in object metadata) so the same content is not
    treated as a conflict.
  - Per-file version vectors would give true causality, but are probably not v1.
- **M10**: a local edit can be overwritten during a download.
- **L11**: S3 objects are read whole into memory.
