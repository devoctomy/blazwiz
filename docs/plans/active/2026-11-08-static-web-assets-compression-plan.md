# Plan: static web assets compression failures and their workarounds

**Date:** 2026-11-08
**Status:** not started
**From:** the techdebt note `docs/plans/techdebt/static-web-assets-compression.md` (removed, found 2026-11-03), which this plan
replaces

## Goal

Find out whether the intermittent static web assets compression failure still happens, what causes it, and whether
apps built from the template can hit it. Then keep only the workarounds that are still needed, each with a reason
written next to it, and report the bug to the .NET SDK if it is one.

## What happens

Building the template app (`template/MyApp.Portal`) sometimes failed in the .NET SDK's static web assets compression
targets (`Microsoft.NET.Sdk.StaticWebAssets.Compression.targets`), with one of:

- `ApplyCompressionNegotiation` failed: "An item with the same key has already been added", where the key is the
  compressed (`.gz`) copy of a package's JS initializer, e.g. `Devoctomy.Blazwiz.Browser.lib.module.js` or
  `Devoctomy.Blazwiz.UI.Sync.lib.module.js`.
- "The process cannot access the file ... because it is being used by another process" on a compressed `.gz` file.

History (2026-11-03, Phases 2 and 3 of the upgradeability plan):

- Seen only in Release builds made by `dotnet run` (the UI tests started the portal that way), after earlier builds of
  the same output in other ways (single-threaded `-m:1` builds, Debug builds of the solution). Clean builds, no-change
  rebuilds and incremental rebuilds after a source change did not reproduce it on their own.
- Seen again on `Devoctomy.Blazwiz.UI.Sync.lib.module.js`: the UI tests' `dotnet run -c Release` failed, and straight
  afterwards `dotnet build template/MyApp.Portal -c Release` on the same `obj` folders succeeded without cleaning. So
  the stale state was not permanent.
- **Cause found: MSBuild node reuse.** `dotnet test` builds the test project and leaves its MSBuild worker processes
  running; the portal's `dotnet run` then built in those reused processes, and the static web assets tasks failed.
  Switching `MSBUILDDISABLENODEREUSE=1` on and off made the same runs pass and fail, each time. `dotnet run` or
  `dotnet build` on their own worked.

Workaround then: delete `src/*/obj/Release`, `src/*/bin/Release`, `template/*/obj/Release` and
`template/*/bin/Release`, and build again. The UI tests report it as "Portal process exited early"; the cause is in
`template/MyApp.Portal.UITests/bin/Debug/net10.0/TestResults/portal.log`.

## The workarounds in place today

| # | Where | What | Why, as written |
|---|---|---|---|
| W1 | `template/MyApp.Portal.UITests/TestBase.cs` (`StartDotnet`) | `MSBUILDDISABLENODEREUSE=1` for the portal's `dotnet build` and `dotnet run` | This failure. **Ships in every generated app** with `--tests` |
| W2 | `.github/workflows/ci.yml` (job `env`) | `MSBUILDDISABLENODEREUSE: 1` for every build and test step | This failure |
| W3 | `.github/workflows/release.yml` (job `env`) | `MSBUILDDISABLENODEREUSE: 1` | No comment |
| W4 | `templatepack/test-template.ps1` | `-m:1` on each generated app's build and on `dotnet pack` | No comment. Added in Phase 2 (`eb5c537`), the same day as the note, which mentions `-m:1` builds |
| W5 | `AGENTS.md` (Build and test) | "build again with `MSBUILDDISABLENODEREUSE=1`" | This failure |

Not part of this plan, but close to it: `test-template.ps1` runs `dotnet build-server shutdown` before each app,
because on Linux an app built with the compiler server left by the apps before it intermittently failed in the Razor
source generator. `TestBase` building the portal before running it with `--no-build` (`04af57d`) is for build time,
not this failure.

## What was found before planning (2026-11-08)

**Upstream.** No report matches this failure exactly.

- [dotnet/aspnetcore#63064](https://github.com/dotnet/aspnetcore/issues/63064) (same task and message, SDK 9.0.300):
  closed as a duplicate of [dotnet/sdk#49039](https://github.com/dotnet/sdk/issues/49039), fixed in 9.0.302 (a stale
  `obj/*.dswa.cache.json` that `dotnet clean` does not remove).
- [dotnet/aspnetcore#64512](https://github.com/dotnet/aspnetcore/issues/64512) (same message from
  `DiscoverPrecompressedAssets`, SDK 10.0.100, a Blazor project referencing another): closed 2025-12-01 without a fix
  in the thread; later comments say it still happens.
- [dotnet/sdk#56235](https://github.com/dotnet/sdk/issues/56235) (open, SDK 10.0.302): `ApplyCompressionNegotiation`
  fails on publish of a WebAssembly app with Razor class libraries, with a different message ("Endpoints not found
  for compressed asset"). The suggested workaround is `<StaticWebAssetBuildCompressAllAssets>false</...>`.
- [dotnet/sdk#54749](https://github.com/dotnet/sdk/pull/54749) (merged to `main` 2026-06-18, **not** in
  `release/10.0.4xx`): the endpoints-by-asset dictionary used a case-sensitive comparer, so asset paths that differ
  only in casing between MSBuild producers were missed on Windows. A silent miss, not our exception, but it shows
  path casing diverging between producers is a known source of trouble. This repository has seen casing differences
  before (`E:\Source` against `E:\source`, see `analyzers.globalconfig` in `AGENTS.md`).
- [dotnet/sdk#55907](https://github.com/dotnet/sdk/pull/55907) (merged to `main` 2026-09-03): other
  `ApplyCompressionNegotiation` failures, hosted WebAssembly publish only. Not ours.

**Where the exception comes from.** In `release/10.0.4xx`, `ApplyCompressionNegotiation.Execute` starts with
`StaticWebAsset.ToAssetDictionary(CandidateAssets)`, which calls `Dictionary.Add(asset.Identity, ...)` with the default
(ordinal, case-sensitive) comparer. `CandidateAssets` is `@(_CompressionCurrentProjectBuildAssets)`. So the failing
build passed the same compressed asset (same `Identity`, same casing) twice. `main` is the same there.

**Where the compressed files are.** In the repository build the template references the packages' projects, and the
`.gz` of each package's JS initializer is written into that package's own `obj`
(`src/Devoctomy.Blazwiz.Browser/obj/Release/net10.0/compressed/...`, per the portal's
`obj/Release/net10.0/staticwebassets.build.json`). Every project that builds the package in Release shares that file,
which would explain "being used by another process" when two builds overlap.

**Apps built from the template are built differently.** The packages ship their assets uncompressed: the `.nupkg`
checked, `Devoctomy.Blazwiz.Browser.0.3.0-multiprofile.1`, holds
`staticwebassets/Devoctomy.Blazwiz.Browser.<fingerprint>.lib.module.js` and no `.gz`. So a generated app compresses
them in its own `obj`. Whether that path can fail is not known: in CI `test-template.ps1` runs with W2
(the job's `env`) and W4, so generated apps have never been built there with node reuse on.

**Local tries (Windows, SDK 10.0.401, the current tree, node reuse on, `MSBUILDDISABLENODEREUSE` unset):**

- `dotnet build blazwiz.slnx`, then three times: touch `Devoctomy.Blazwiz.Browser.lib.module.js`,
  `dotnet build template/MyApp.Portal -c Release`, `dotnet build blazwiz.slnx`. All passed.
- Twice: touch both packages' initializers, `dotnet test template/MyApp.Portal.Application.UnitTests`, then
  `dotnet run --project template/MyApp.Portal -c Release --no-launch-profile`. Both built and started.
- Not tried: the UI test suite itself without W1 (it needs a code change).

**CI.** Of the last 40 runs of `ci.yml`, one failed, on a UI test (`GivenSyncPage_WhenOpened_...`), not a build.
CI runs with W2, so it says nothing about the failure.

**SDK.** The plan uses the latest .NET 10 SDK. On 2026-11-03 this machine had 10.0.401, while 10.0.402 (2026-10-13)
and 10.0.403 (2026-10-25) were already out: updating was not tried then. 10.0.403 was installed here on 2026-11-08, and
the build and every test suite in `AGENTS.md` passed on it (with W1 on). `global.json` still pins 10.0.401 with
`rollForward: latestPatch`.

## Requirements

- **R1. Know whether it still happens.** Run the sequences that failed before, with the workarounds off, enough times
  to say (at least 5 runs each), here, on the latest .NET 10 SDK. CI builds and tests on Linux on every push, so
  once the workarounds are gone it covers Linux.
- **R2. Know whether apps can hit it.** Build and test an app made by `test-template.ps1`, against the packages, with
  node reuse on and without `-m:1`, the way an app's developer would (UI tests included).
- **R3. Every workaround has a reason.** Each of W1 to W5 is either removed, or kept because a test run showed it is
  needed. None stays only because nobody checked.
- **R6. Docs.** `AGENTS.md` and `CHANGELOG.md` (if the template's
  tests change) match what is decided.

Changing `TestBase` changes template content only. Existing apps do not take template files on upgrade, so no
upgrade test is needed for it (`AGENTS.md`, Upgrade tests).

## Work

### Phase 1: does it still happen (R1)

- [ ] Use the latest .NET 10 SDK everywhere: `global.json` names `10.0.100` with `rollForward: latestFeature`, and both
  workflows use `actions/setup-dotnet@v6`, which installs the newest SDK that rule allows. Done first, so every run
  below uses it.
- [ ] Switch W1 off in the working tree only (not committed), and run the UI tests as CI does: build, unit tests,
  integration tests, UI tests, with node reuse on. 5 runs, with a binlog of the portal's build (`-bl`) and
  `portal.log` kept for any failure.
- [ ] The same with the repository at a different path casing (`E:\source\...`), to test the casing lead.
- [ ] Record the results here, then restore W1.

### Phase 2: apps (R2)

- [ ] Make an app with `test-template.ps1` (keep its work folder), then build and run its UI tests in reused nodes,
  without `-m:1` and with `MSBUILDDISABLENODEREUSE` unset. 5 runs.
- [ ] Find out why `-m:1` (W4) was added: run `test-template.ps1` without it and see what fails.

### Phase 3: the workarounds (R3)

A workaround for a failure that no longer happens is removed. Each of these is removed unless a run in phase 1 or 2
failed without it:

- [ ] W1 (`TestBase`, shipped to apps).
- [ ] W2 and W3 (workflows).
- [ ] W4 (`-m:1`).
- [ ] W5 (`AGENTS.md`).

### Phase 4: proving it and docs (R6)

- [ ] Everything in `AGENTS.md`'s Build and test passes, including the UI tests and
  `./templatepack/test-template.ps1 -SkipRepositoryTests`, with the workarounds as decided.
- [ ] `CHANGELOG.md` (Unreleased) if the template's tests changed.

## Out of scope

- The Razor source generator failure that `dotnet build-server shutdown` in `test-template.ps1` works around.
- Turning compression off (`CompressionEnabled`, `StaticWebAssetBuildCompressAllAssets`): apps should get
  compressed assets, so this is only a last resort, and would need its own decision.
