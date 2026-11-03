# Syncing without end to end encryption: the app developer's choice (not implemented yet)

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
