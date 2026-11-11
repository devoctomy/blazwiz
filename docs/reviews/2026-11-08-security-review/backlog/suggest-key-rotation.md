---
title: Suggest a passphrase change when a profile's keys are old
type: backlog
status: needs a decision
added: 2026-11-11
review: ../security-review.md
finding: SR37 (follow-up of SR3)
severity: low
urgency: low
before-1.0: no
reason: a profile whose passphrase never changes keeps one file key for every write, for as long as it exists
areas: [Encryption, Sync, UI.Sync]
effort: medium
functional-change: yes
blocked-by: none (key rotation on a passphrase change is done)
---

# Suggest a passphrase change when a profile's keys are old

Left for later by the key rotation plan (completed and removed on 2026-11-11), which rotates a profile's keys when its
passphrase changes. SR37 of `../security-review.md` notes that nothing
starts a rotation otherwise.

## The gap

A new random nonce is used for every write, and the file key is replaced only when the passphrase changes. A profile
whose passphrase never changes keeps the same file key for all its writes, for as long as it exists. SR37's bound
(about 2^32 encryptions per key with random nonces) is far beyond personal use, so this is about key hygiene and limiting
how much one leaked key exposes, not about nonce reuse.

## The idea

After a set amount of time, the app **suggests** that the user changes the passphrase, which rotates the keys. It is
never forced and never automatic: the user can do it now, later, or not at all. Rotation always comes with a
passphrase change (the maintainer's decision of 2026-11-10: the security rests on the passphrase being secret), so the
suggestion is to change the passphrase, and it runs the existing flow (sync, estimate and warning, re-encrypt, sign
out everywhere).

- **The age.** `FileKeyEntry.Created` of the active key in the keys document says when the keys were last rotated (or
  first made). No new stored field is needed for that.
- **The interval.** An app setting with a sensible default (for example a year), which an app can change or turn off.
  Whether users can also turn the reminder off is to decide.
- **Where it shows.** A notice on the profile in Settings (`E2eProfilesViewModel`) and possibly a dismissible banner,
  with a button that starts the passphrase change. New `SyncUiText` entries.
- **Dismissing it.** "Remind me later" hides it for a while. Where that is remembered (on the device in browser
  storage, or in the keys document so every device agrees) is to decide.

## To decide and check

- The default interval, and whether the app or the user can turn the reminder off.
- Whether a count of writes should also trigger the suggestion (SR37 mentions it). Counting writes needs a counter that
  every device updates, so time alone is simpler.
- Where the notice shows, and how "remind me later" is remembered.
- Tests: no suggestion before the interval, a suggestion after it (`FakeTimeProvider`), gone after a passphrase change,
  hidden by "remind me later" until it expires; the setting off shows nothing.
- Docs: the Sync README's note on rotation ("What encryption does and does not hide"), the UI.Sync README, SR37 in the
  review, `CHANGELOG.md`.
