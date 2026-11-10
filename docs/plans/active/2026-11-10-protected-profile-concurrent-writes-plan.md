---
title: Move the keys documents and profile index from Secrets Manager to S3, with conditional writes
type: active
status: in progress
added: 2026-11-05
updated: 2026-11-10
urgency: high
before-1.0: yes
reason: two devices writing the keys document or the index at once lose one write; Secrets Manager has no conditional write, S3 does, and once keys rotate a lost write can orphan files under a key nobody holds
areas: [Encryption, Sync, UI.Sync, AWS stack, deploy scripts, template]
effort: medium
functional-change: yes (storage location; no change to what users see)
blocked-by: none
decision: move to S3 (2026-11-09); keep IUserKeysStore as the contract
related:
  - ../../reviews/2026-11-08-security-review/backlog/passphrase-change-key-rotation.md (makes the lost write dangerous)
  - ../../diagrams/user-events.md (user events: shrink the race window; do not close it)
  - ../../reviews/2026-11-08-security-review/security-review.md (SR5, SR12, SR20)
---

# Move the keys documents and profile index from Secrets Manager to S3, with conditional writes

## The gap

A user's protected-profile keys documents and the index of their profiles are kept in Secrets Manager, one secret per
profile (`<sub>/keys/<scope>`) and one for the index (`<sub>/keys/index`), through `IUserKeysStore` and
`IE2eProfileIndexStore` (`Encryption`), implemented by `SecretsManagerUserKeysStore` (`UI.Sync`). Secrets Manager has
no conditional write: `PutSecretValue` always creates a new version and makes it current, whatever the current version
is, and its client request token is an idempotency key, not a precondition. So every read-modify-write of these
documents is a race:

- **Passphrase change on two devices at once:** both write the keys document; the second replaces the first, and only
  the second passphrase works. After key rotation (the related plan) the first device's new file key is gone with its
  document, and any file it wrote under that key is unreadable everywhere.
- **Creating or deleting profiles on two devices at once:** both rewrite the index; the second write drops the first
  device's entry. A new profile's keys document exists but no device can find it. SR20's `TryRestoreToIndexAsync`
  recovers that case by checking for the document, but it is a repair, not a prevention.
- **Changing an idle timeout while another device writes the index:** the timeout lives in the index
  (`E2eProfileIndexEntry.IdleTimeoutSeconds`), so it races with everything above.

The synced files do not have this problem: `S3SyncRemoteStore` sends `If-Match` with the last ETag on every put and
delete, and `If-None-Match: *` when creating, and a 412 makes the engine replan. The documents that protect those
files are the only unguarded writes left.

## What Secrets Manager gives us, and what S3 gives us instead

Secrets Manager was chosen to keep the keys documents apart from the synced files, with create-only semantics and a
recovery window. Each of those now has an S3 equivalent in the stack, and S3 has the one thing Secrets Manager lacks:

| Need | Secrets Manager today | S3, after SR4 and this change |
|---|---|---|
| Compare-and-swap write | none | `If-Match: <ETag>` on `PutObject` (412 on mismatch) |
| Create only, never overwrite | `CreateSecret` fails with `ResourceExistsException` | `If-None-Match: *` on `PutObject` |
| Previous versions | one, `AWSPREVIOUS` | every version, kept 30 days after replacement (`DataBucket` versioning and lifecycle) |
| Recovery after deletion | 7-day window; `Deny` on forced deletion (SR5) | a delete only adds a delete marker; `Deny` `s3:DeleteObjectVersion` to the role, so a user's credentials can never purge a version; versions expire after 30 days |
| Who did what | management events (free) | CloudTrail data events on the bucket in `prod` (SR48) |
| Per-user isolation | IAM on the secret name, keyed by the user pool `sub` | IAM on the prefix, keyed by the identity id, as for the files |
| Clean-up with the environment | secrets tagged `app=<name>`, listed and deleted by `remove.ps1` (SR12) | the bucket is a stack resource; nothing to find |
| Cost | $0.40 per secret per month plus $0.05 per 10,000 calls: for every user, one secret per profile plus the index | rounds to nothing |
| In the browser | a second AWS SDK package, a second endpoint in the CSP, a second IAM policy | the S3 client the app already has |

The keys document is wrapped under an Argon2id passphrase key (and, for device unlock, a device key), so sitting beside
the ciphertext in the same bucket adds nothing for an attacker who already has the bucket: they had the ciphertext,
and the document is useless without the passphrase. Secrets Manager is left giving us a worse version of what S3
already does, for money. Removing it also removes a service, an SDK package, a policy, a CSP entry and a tag-based
clean-up from the project.

## Design

1. **Contract.** `IUserKeysStore` and `IE2eProfileIndexStore` learn about versions: `GetAsync` returns the value with
   its ETag (a `StoredDocument` record, or null); `SetAsync` takes `ifMatch` and throws a precondition exception
   (reuse `SyncPreconditionFailedException`, moved to `Core`, or a sibling in `Encryption`) when the ETag no longer
   matches; `TryCreateAsync` keeps its meaning with `If-None-Match: *`. The `LocalStorageUserKeysStore` used in
   development does the same with a counter as its ETag.
2. **Store.** `S3UserKeysStore` in `Sync` (where `S3SyncRemoteStore` and `IS3SyncTargetProvider` live), at
   `<identity id>/keys/<scope>` and `<identity id>/keys/index` in the data bucket. `keys/` becomes a reserved name at
   the root of the user's prefix: `SyncBuilder` rejects a profile whose prefix starts with it. The documents are
   written with the same `modified-at` metadata as files, no content encryption (they are already wrapped), and the
   folder-marker and encrypted-format checks in the engine never see them because no profile matches them.
3. **Manager.** `E2eProtectedProfileManager` wraps every read-modify-write of the index and of a keys document in a
   loop: read with ETag, apply the change, write with `If-Match`; on 412, read again and apply the change to the new
   version, a few times at most, then surface the failure. Creation stays `TryCreateAsync`. The device's cached copy of
   the keys document keeps the ETag too, so an unlock can tell whether the document changed since (the "other
   devices" check the key rotation plan needs).
4. **Stack.** `AuthenticatedRole`: drop the `per-user-keys` policy and the `NoForcedSecretDeletion` statement; add a
   `Deny` on `s3:DeleteObjectVersion` (and `s3:PutLifecycleConfiguration` is not granted anyway) so user credentials
   can only add delete markers. Nothing else changes: the bucket, its versioning, lifecycle and CloudTrail trail exist.
5. **Scripts and policy.** `remove.ps1` loses the tagged-secret deletion; `test-auth.ps1` replaces its secret checks
   with: create a keys document with `If-None-Match: *`, create it again (expect 412), overwrite with a stale `If-Match`
   (expect 412), delete it and list versions (expect a delete marker and the version still present), attempt
   `DeleteObjectVersion` (expect denied). `deploy-policy.json` loses the `secretsmanager:*` actions. `_common.ps1`
   drops the Secrets Manager endpoint from the CSP `connect-src`.
6. **Packages.** `UI.Sync` loses `SecretsManagerUserKeysStore`, `ISecretsManagerClientFactory` and the
   `AWSSDK.SecretsManager` reference; `Sync` registers the S3 store from `AddBlazwizSync`. `PublicAPI.Unshipped.txt`
   in `Encryption`, `Sync` and `UI.Sync` change. The package table stays the same.
7. **Documentation.** `docs/diagrams/authentication.md`, `overview.md`, `sync-framework.md` and `sync.md` (every
   mention of Secrets Manager, the secret names and the role policy), the `UI.Sync` README, the template README,
   `template/cloudformation/README.md` (the cost note goes), `docs/reviews/2026-11-08-security-review/security-review.md`
   rows SR5, SR12 and SR20 gain a note that the control moved, `CHANGELOG.md`.
8. **Tests.** Unit tests for the retry loop (a fake store that returns 412 once, then succeeds; and one that always
   fails), for the reserved prefix, and for the cached ETag; the existing manager tests move to the new contract. An
   `@aws` integration scenario for the conditional writes, skipped without credentials as always.
9. **Upgrade.** Before 1.0 no migration is written. The maintainer's own deployment (clypse) has keys documents in
   Secrets Manager today; they are re-created by setting the profiles up again, or copied by hand once with the AWS
   CLI into `<identity id>/keys/`. Say so in the changelog. The stack still deletes nothing in Secrets Manager, so
   existing secrets stay restorable until the maintainer removes them.

## Steps

- [x] Contract changes in `Encryption` and the development store, with tests.
- [x] `S3UserKeysStore` and the reserved `keys/` prefix in `Sync`, with tests.
- [x] The retry loop and cached ETag in `E2eProtectedProfileManager`, with tests.
- [x] Remove the Secrets Manager store, factory and package from `UI.Sync`.
- [x] Stack, `remove.ps1`, `test-auth.ps1`, `deploy-policy.json`, CSP; parse-check here, deploy and run is the
      maintainer's.
- [x] Documentation and changelog as listed; review rows SR5, SR12, SR20 annotated.
- [x] All four suites and `./templatepack/test-template.ps1 -SkipRepositoryTests`; then
      `./templatepack/test-template.ps1 -UpgradeFrom latest`, since the stack and role policy change.
- [ ] Deploy (`deploy.ps1`), run `test-auth.ps1` and the `@aws` scenarios (`UserKeys.feature`), and move or set up
      again the existing profiles: the maintainer's.

## Decisions taken while building it

- **Local cache kept**, with its ETag (`E2eLocalKeys.CachedDocumentETag`): offline unlock needs it.
- **`Encryption` has its own exception**, `E2eDocumentChangedException` (code `E2e.DocumentChanged`, with text in
  `SyncUiText`), rather than moving `SyncPreconditionFailedException` to `Core`: the stores' callers are in
  `Encryption`, and the sync engine's exception stays as it is.
- **`StoredDocument` is a class**, not a positional record: StyleCop (SA1313) refuses the record's parameter names.
- **The index's write takes `ifMatch: null` to mean "create only"** (`If-None-Match: *`), so a first index written by
  two devices at once is guarded too. A keys document's `SetAsync` always takes an ETag: it is created only with
  `TryCreateAsync`, which now returns the new ETag (null when keys exist).
- **The manager's loop** reads the index from the store (never the device's copy) before each change, and gives up
  after `MaxWriteAttempts` (3). A passphrase change is retried from the new version too, so when another device changed
  the passphrase in between, the current passphrase no longer unwraps it and the user is told it is wrong.
- **Which store is used** depends on whether a data bucket is configured (`IS3SyncTargetProvider.IsConfigured`), no
  longer on whether a user pool is: without a bucket there is nothing to sync the keys with.
- **The `userSub` principal tag and `sts:TagSession` went too.** They existed only for the Secrets Manager policy. While
  CloudFormation updates the stack, the role loses `sts:TagSession` before the tag mapping is removed (deletions come
  last), so sign-ins may fail for that minute or so. `deploy-policy.json` keeps the `cognito-identity` principal tag
  actions, which deleting the mapping needs.
- **Version clean-up in `test-auth.ps1`** runs as the deployer: users cannot list or delete versions, so the script
  checks the test keys document's two versions and delete marker with your credentials, then removes them.
