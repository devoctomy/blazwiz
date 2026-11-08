# Request: recovering a protected profile after a forgotten passphrase

**Date:** 2026-11-05
**Status:** not planned
**Related:** `docs/plans/2026-11-05-profile-passphrases-plan.md` (removed) (D6)

## What

A way for a user to regain access to a protected profile (a sync profile with its own passphrase) when they have
forgotten its passphrase.

## Why it is not in the plan

The plan has no recovery: a forgotten passphrase means the profile's files cannot be decrypted. The app warns about
this when a protected profile is created (R9).

## One way to do it

When a protected profile is created, generate a one-time recovery key and show it to the user once. Wrap the
profile's master key with it a second time and keep that copy in the profile's keys document. Entering the recovery
key unwraps the master key and lets the user set a new passphrase.

Questions to answer before planning it:

- Whether recovery is offered for every protected profile, or only when the user chooses it at creation.
- How the recovery key is shown and saved (displayed once, downloaded, printed), and what happens if it is lost too.
- Whether using the recovery key replaces it with a new one.
- How it changes the warnings shown when a protected profile is created.
