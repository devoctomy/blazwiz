---
title: Deploy tests, run against a deployed environment, in place of test-auth.ps1
type: active
status: in progress
added: 2026-11-10
urgency: high
before-1.0: yes
reason: test-auth.ps1 is an ad hoc script nothing runs, named after the first thing it checked; a deployed environment's IAM policies, conditional writes and isolation are otherwise never checked for real
areas: [tests, workflows, AWS stack scripts, template, docs]
effort: medium
functional-change: no (what apps run is unchanged; the test script is replaced)
blocked-by: none
related:
  - 2026-11-10-protected-profile-concurrent-writes-plan.md (its keys checks move here)
  - ../../deploying.md
---

# Deploy tests, run against a deployed environment, in place of test-auth.ps1

## The gap

The unit, integration and UI tests run before a deploy, against fakes, a local channel and the placeholder sign in.
Nothing checks the deployed environment: that the user pool, identity pool and role let a user reach their own data
and nothing else, that keys documents are written conditionally and cannot be purged, that the events channel is the
user's own. `test-auth.ps1` did some of this, but nothing runs it, its name says "auth" while it checks everything, and
it has to be run by hand with the deployer's credentials.

## Design

1. **A test project, `tests/Devoctomy.Blazwiz.DeployTests`** (Reqnroll, xUnit), in the solution. Its suffix keeps it
   out of CI's `UnitTests`, `IntTests` and `UITests` filters: it only runs when the Deploy tests workflow runs it.
2. **Its only inputs** are the portal's URL and the test user's email and password, in environment variables
   (`BLAZWIZ_DEPLOYTEST_PORTAL_URL`, `_EMAIL`, `_PASSWORD`). Without them every scenario is skipped. Everything else
   comes from the portal's public `appsettings.Production.json` (region, user pool, app client, identity pool, data
   bucket, events endpoints). The tests hold no AWS credentials of their own: they act only as the test user, signed
   in with SRP as the browser does, with the AWS credentials the identity pool gives that user.
3. **Features:**
   - **Portal:** the home page and a deep link are served (index.html), and the settings are there.
   - **Signing in:** SRP sign in, and AWS credentials from the identity pool.
   - **The user's data:** read, write, list and delete in their own prefix; refused another user's prefix and listing
     the whole bucket.
   - **The user's keys** (through `S3UserKeysStore`): created only once, replaced only at the version read, a delete
     only adds a delete marker, no version can be deleted, another user's keys are refused. The test document's old
     versions are left to the bucket's 30-day lifecycle rule: the user cannot delete them, by design.
   - **User events** (when the settings have them): publish to the user's own channel, refused another user's.
   - **Sign out everywhere** (`@signouteverywhere`, only with `BLAZWIZ_DEPLOYTEST_SIGN_OUT_EVERYWHERE=true`): global
     sign out revokes the access token. It signs the test user out on every device.
4. **A Deploy tests workflow (`deploy-tests.yml`),** run by hand or called by Deploy and Upgrade:
   - job `locate`: signs in with the deploy role (OIDC), reads `PortalUrl` from the stack, and fails with "deploy
     first" if there is no stack. Only the URL is passed on.
   - job `test`: no AWS access at all. Runs `dotnet test` on the project with the URL, `TEST_USER_EMAIL` (environment
     variable) and `TEST_USER_PASSWORD` (environment secret), failing at once if either is missing.
   - input `sign_out_everywhere` (off by default).
5. **Deploy and Upgrade:** the test user's email moves from Deploy's input to the environment variable
   `TEST_USER_EMAIL`; both workflows get the checkboxes "Create or update the deploy tests' user" and "Run the deploy
   tests afterwards".
6. **Removed:** `test-auth.ps1` (the base stack's and the template's), its mention in `MyApp.slnx`, `deploy.ps1`'s
   closing hint, and the docs about it. The app client's `ALLOW_ADMIN_USER_PASSWORD_AUTH` flow existed only for it, and
   goes too; the deployer keeps `AdminInitiateAuth` only if `deploy.ps1` uses it (it does not).

## Steps

- [x] The test project and its features.
- [x] `deploy-tests.yml`; the checkboxes in Deploy and Upgrade.
- [x] Remove `test-auth.ps1`, the admin auth flow, and their mentions.
- [x] Documentation: `docs/deploying.md`, the cloudformation READMEs, the template README, `AGENTS.md`, `CHANGELOG.md`.
- [x] Build, the four suites, `./templatepack/test-template.ps1 -SkipRepositoryTests` (the template loses a file).
- [ ] First run of the Deploy tests workflow against a deployed environment: the maintainer's.
