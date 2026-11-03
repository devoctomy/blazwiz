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

## Workaround

Delete `src/*/obj/Release`, `src/*/bin/Release`, `template/*/obj/Release` and `template/*/bin/Release`, then build
again. The UI tests report it as "Portal process exited early"; the cause is in
`template/MyApp.Portal.UITests/bin/Debug/net10.0/TestResults/portal.log`.

## To do

- Find a reliable reproduction (SDK 10.0.401), and check whether apps building against the packages (rather than the
  packages' source) can hit it.
- If it is an SDK bug with JS initializers in Razor class libraries, report it to dotnet/sdk with the reproduction.
- If apps can hit it, document the workaround for them, or ship the initializers so they are not compressed twice.
