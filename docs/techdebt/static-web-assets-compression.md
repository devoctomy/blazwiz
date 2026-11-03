# Intermittent static web assets compression failures

Found 2026-11-03, during Phase 2 of the upgradeability plan.

## What happens

Building the template app (`template/MyApp.Portal`) sometimes fails in the .NET SDK's static web assets compression
targets (`Microsoft.NET.Sdk.StaticWebAssets.Compression.targets`), with one of:

- `ApplyCompressionNegotiation` failed: "An item with the same key has already been added", where the key is the
  compressed (`.gz`) copy of a package's JS initializer, e.g. `Devoctomy.Blazwiz.Browser.lib.module.js` or
  `Devoctomy.Blazwiz.UI.Sync.lib.module.js`.
- "The process cannot access the file ... because it is being used by another process" on a compressed `.gz` file.

It has only been seen in Release builds made by `dotnet run` (the UI tests start the portal that way), after earlier
builds of the same output in other ways (single-threaded `-m:1` builds, Debug builds of the solution). Clean builds,
no-change rebuilds and incremental rebuilds after a source change did not reproduce it when tried on their own.

Seen again 2026-11-03 (Phase 3), on `Devoctomy.Blazwiz.UI.Sync.lib.module.js`: the UI tests' `dotnet run -c Release`
failed, and straight afterwards `dotnet build template/MyApp.Portal -c Release` on the same `obj` folders succeeded,
without cleaning. So the stale state is not permanent, and the failure is tied to the build `dotnet run` does (the
UI tests start the portal that way, with `ASPNETCORE_ENVIRONMENT=Development` and `--no-launch-profile`).

**Cause found** (same day): MSBuild node reuse. The UI tests run `dotnet test`, which builds the test project and
leaves its MSBuild worker processes running; the portal's `dotnet run` then builds in those reused processes, and
the static web assets tasks fail. Confirmed by switching it on and off: with `MSBUILDDISABLENODEREUSE=1` the same
runs pass, without it they fail, each time. Running `dotnet run` or `dotnet build` on their own works.

**Fix:** `TestBase` starts the portal with `MSBUILDDISABLENODEREUSE=1`. Release pipelines build once in fresh
processes, so they are not affected.

## Known reports (searched 2026-11-03)

No report matches exactly (`ApplyCompressionNegotiation`, duplicate key on a Razor class library's JS initializer,
SDK 10.0.401). The closest are the same family of static web assets failures, all involving stale incremental state:

- [dotnet/aspnetcore#63064](https://github.com/dotnet/aspnetcore/issues/63064): the same task and message
  (`ApplyCompressionNegotiation`, "An item with the same key has already been added") on SDK 9.0.300, after repeated
  debug restarts. Closed as a duplicate of dotnet/sdk#49039, "addressed as part of 9.0.302" (not confirmed by the
  reporter). Workaround: delete the `.vs` folder.
- [dotnet/sdk#49039](https://github.com/dotnet/sdk/issues/49039): MSB4018 in the static web assets tasks on 9.0.300,
  fixed in 9.0.302. Users found a stale `obj/*.dswa.cache.json` that `dotnet clean` does not remove; deleting `obj`
  fixed it.
- [dotnet/aspnetcore#64512](https://github.com/dotnet/aspnetcore/issues/64512): the same duplicate-key message from
  `DiscoverPrecompressedAssets` on SDK 10.0.100, when a Blazor project references another Blazor project. Closed
  without a fix; later comments say it still happens.
- [dotnet/sdk#55907](https://github.com/dotnet/sdk/pull/55907): fixes other `ApplyCompressionNegotiation` failures
  in build compression (12.0 previews); not the same assets.

So the likely cause is the SDK's cached static web assets state (`obj/**/*.dswa.cache.json`, `staticwebassets.*.json`)
going stale between builds made in different ways, which matches only interleaved builds failing here.

## Workaround (before the fix)

Delete `src/*/obj/Release`, `src/*/bin/Release`, `template/*/obj/Release` and `template/*/bin/Release`, then build
again. The UI tests report it as "Portal process exited early"; the cause is in
`template/MyApp.Portal.UITests/bin/Debug/net10.0/TestResults/portal.log`.

## To do

- Reproduce it outside our tests (a test project's build followed by an app's `dotnet run` reusing its nodes), and
  report it to dotnet/sdk if it reproduces on a minimal project.
- Find a reliable reproduction (SDK 10.0.401), and check whether apps building against the packages (rather than the
  packages' source) can hit it.
- If it is an SDK bug with JS initializers in Razor class libraries, report it to dotnet/sdk with the reproduction.
- If apps can hit it, document the workaround for them, or ship the initializers so they are not compressed twice.
