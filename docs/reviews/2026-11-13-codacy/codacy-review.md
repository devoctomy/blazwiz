---
title: Codacy issues on the coverage branch
type: review
status: open
added: 2026-11-13
---

# Codacy review: the coverage branch

Codacy Cloud analysed commit `6943d59` on `coverage` (PR #31) and reported 627 issues. PR #31 fails the quality gate
with 2 new issues (the gate allows 0). Coverage shows as "Not set up" on Codacy.

Each issue was checked against the code. 14 were real and are fixed in the working tree. Almost all of the rest are
false positives or style rules that don't fit this project. This review suggests which patterns to turn off, which
paths to exclude, and which single issues to ignore.

## Fixed (14 issues)

| Pattern | Where | Fix |
|---|---|---|
| SonarCSharp_S1854, S1226 | `src/Devoctomy.Blazwiz.Events/Sessions/UserEventsSessionHandler.cs:163` | The timer callback's parameter was named `_` and then assigned to (`_ => _ = ...`). Renamed the parameter to `state`, so `_` is a real discard. |
| SonarCSharp_S1481 | `src/Devoctomy.Blazwiz.UI.Sync/ViewModels/E2eProfilesViewModel.cs:514` | Removed the unused local `current`. |
| SonarCSharp_S1481 | `template/MyApp.Portal.Application.UnitTests/ViewModels/FilesViewModelTests.cs:497` | Removed the unused `sut` local; the call is still awaited. |
| SonarCSharp_S1117 | `template/MyApp.Portal.Application/ViewModels/NoteEditorViewModel.cs:552` | The local `note` hid the `note` field. Renamed it to `open`. |
| SonarCSharp_S1117 | `tests/Devoctomy.Blazwiz.Core.UnitTests/Text/TextPluralsTests.cs:112` | The local `options` hid the `options` field. Renamed it to `registered`. |
| SonarCSharp_S3872 | `src/Devoctomy.Blazwiz.Sync/Configuration/SyncConfigurationReader.cs:84, 108` | Renamed the `apply` and `parse` parameters to `setting` and `parser`. |
| SonarCSharp_S3872 | `template/MyApp.Portal.Application.UnitTests/Services/MarkdownFormatServiceTests.cs:233` | Renamed the `format` parameter to `style`. |
| SonarCSharp_S1994 | `template/MyApp.Portal.UITests/AppUpdateTests.cs:48` | The retry limit was hidden in the `if`; it is now the loop condition (`attempt <= 20`). Behaviour is unchanged. |
| psscriptanalyzer_psavoidusingpositionalparameters | `eng/bump-version.ps1:152, 157, 195, 196` | `Write-Text` calls use named parameters (`-Path`, `-Text`, `-Original`). The unflagged call at line 161 is changed too, for consistency. |

Checked with `dotnet build blazwiz.slnx`, then the unit, integration and UI test runs from `AGENTS.md`: all passed,
with the 10 `@aws` integration scenarios skipped as expected. The `./templatepack/test-template.ps1
-SkipRepositoryTests` result is given with the change.

No user-visible behaviour changed, so `CHANGELOG.md` has no entry.

## The 2 issues failing PR #31's gate

1. `SonarCSharp_S1172` on `tests/Devoctomy.Blazwiz.UI.UnitTests/Services/AppBuildInfoTests.cs:82`: `inherit` is
   unused, but the method overrides `GetCustomAttributes(Type, bool)`, so the parameter has to stay. False positive.
2. `Lizard_file-nloc-medium` on `template/MyApp.Portal.Application.UnitTests/ViewModels/NoteEditorViewModelTests.cs`
   (594 lines). A test file for one view model. See Lizard below.

The pattern changes below clear both.

## Compared with clypse

clypse (`E:\clypse\docs\reviews\2026-11-13-codacy-noise.md`) was tuned on Codacy the same day. These suggestions
follow it, so the two repositories are set up the same way:

- **Same as clypse (turned off there):** `SonarCSharp_S2360`, `S2339`, `S2333`, `psavoidusingwritehost`,
  `Stylelint_color-no-hex`, `color-hex-length`, `media-feature-range-notation`, `markdownlint_MD032`, `MD029`,
  `MD024`, and the whole Agentlinter tool.
- **Different from clypse:** clypse keeps `SonarCSharp_S1172`, `S3264`, `S3453` and `S3241` on, and ignored its few
  hits one by one (3, 1, 2 and 1). Here they are 42, 23, 6 and 1, all false positives, so turning them off is
  suggested. To keep the two repositories the same instead, ignore the issues one by one as clypse did.
- **Only here:** `SonarCSharp_S131`, the Lizard limits, `psuseshouldprocessforstatechangingfunctions`,
  `markdownlint_MD022` and `MD025`, the other Stylelint formatting rules, PMD on JavaScript, and the exclusions.
## Suggested: turn off

The commands use the Codacy Cloud CLI (`npx --no-install @codacy/codacy-cloud-cli`, shown as `codacy`).

### SonarC# (about 360 issues)

| Pattern | Issues | Why it doesn't fit |
|---|---|---|
| `SonarCSharp_S2339` Avoid public constant members | 125 | The constants are error codes, storage names and defaults (`AuthErrorCodes`, `SyncDefaults`, `E2eKeyNames`). They are used in `switch` cases and attributes, which need `const`. |
| `SonarCSharp_S2360` Avoid optional parameters | 111 | Mostly `CancellationToken ct = default` on interfaces, the normal .NET pattern. Overloads would double the public API. |
| `SonarCSharp_S2333` 'partial' is gratuitous | 45 | Every one is needed: Razor code-behind (`*.razor.cs`) and source generators (`[ObservableProperty]` view models, `[GeneratedRegex]`, `[LoggerMessage]`). Sonar runs without the generators, so it sees only one half. |
| `SonarCSharp_S1172` Unused method parameters | 42 | All are event handlers (`object? sender`), overrides, or test fakes that implement an interface. The signatures are fixed. |
| `SonarCSharp_S3264` Unused events | 23 | Wrong: the events are raised (for example `SyncService.StatusChanged?.Invoke` at `SyncService.cs:128`, `SyncProfileRegistry.ProfileAdded` at `:97`). The rest are interface events on test fakes. |
| `SonarCSharp_S3453` Class can't be instantiated | 6 | Resource marker types for `IStringLocalizer<T>` (`SyncUiText`, `NotesText`) and test types built another way. |
| `SonarCSharp_S131` Switch without `default` | 4 (High) | The switches handle some cases and then fall through to shared code after the `switch` (`SyncEventPresenter.Describe`). A `default: break;` adds nothing. |

```bash
codacy pattern "SonarC#" SonarCSharp_S2339 --disable
codacy pattern "SonarC#" SonarCSharp_S2360 --disable
codacy pattern "SonarC#" SonarCSharp_S2333 --disable
codacy pattern "SonarC#" SonarCSharp_S1172 --disable
codacy pattern "SonarC#" SonarCSharp_S3264 --disable
codacy pattern "SonarC#" SonarCSharp_S3453 --disable
codacy pattern "SonarC#" SonarCSharp_S131 --disable
```

The compiler and StyleCop already cover unused parameters and locals (IDE0060, CS0168), and warnings are errors here.

### Lizard (44 issues)

| Pattern | Issues | Suggestion |
|---|---|---|
| `Lizard_parameter-count-medium` (limit 8) | 8 | All are constructors that take services by dependency injection. Turn off. |
| `Lizard_file-nloc-medium` (limit 500) | 14 | 9 are test files for one class each. Its limit is fixed on Codacy, so it is turned off. The 5 source files (`E2eProtectedProfileManager`, `E2eProfilesViewModel`, `LoginViewModel`, `FilesViewModel`) are worth splitting some day, but not to pass a gate. |
| `Lizard_ccn-minor` (limit 10) | 22 | Info only. Keep it as a pointer, or raise it to 15. The largest are `SyncPlanner.PlanItem` (18) and `LoginViewModel`'s constructor (17). |

### PowerShell (43 issues)

| Pattern | Issues | Why |
|---|---|---|
| `psavoidusingwritehost` | 40 | `test-template.ps1`, `bump-version.ps1` and the CloudFormation scripts are interactive console tools. `Write-Host` is what they should use. |
| `psuseshouldprocessforstatechangingfunctions` | 3 | Private helpers inside a script (`New-TempJsonFile`, `Remove-TempFile`, `Set-StackPolicy`), not cmdlets. |

### Markdown (55 issues)

| Pattern | Issues | Why |
|---|---|---|
| `markdownlint_MD022`, `MD032` (blank lines round headings and lists) | 37 | `CHANGELOG.md` puts a list straight under each `###` heading, and `eng/bump-version.ps1` writes it that way. The other hits are in two backlog plans. Turn off, or exclude `CHANGELOG.md`. |
| `markdownlint_MD024` (repeated headings) | 4 | Keep a Changelog repeats `### Added` and the others in every version. Turn off. |
| `markdownlint_MD025` (several top-level headings) | 12 | The plans and reviews have a `title` in the front matter and a `#` heading, as `AGENTS.md` asks. Turn off. |
| `markdownlint_MD029` (ordered list prefix) | 1 | `template/README.md:99` is numbered across a template `#if`. Turn off, as clypse did. |
| `markdownlint_MD049` | 1 | `WelcomeNote.md` shows `_italic_` on purpose. Ignore this one. |

### CSS (about 80 issues)

The Stylelint hits are style choices, not errors. clypse turned off `color-no-hex` (the hex colours are the theme's
tokens, defined once as custom properties), `color-hex-length` and `media-feature-range-notation` (Bootstrap's
breakpoint style). Here those are 23, 2 and 4. The others only appear here: `color-function-notation` (12),
`alpha-value-notation` (12), `custom-property-empty-line-before` (4), `comment-empty-line-before` (2), and
`declaration-empty-line-before`, `declaration-no-important`, `value-keyword-case` and `font-family-name-quotes` (1
each). Turn these off pattern by pattern, as clypse did, and keep Stylelint's real error checks on.

8 of them are in `src/Devoctomy.Blazwiz.UI/wwwroot/lib/inter/inter.css`, a vendored font file. Exclude `**/wwwroot/lib/**`.

### JavaScript (9 issues)

`PMD_category_ecmascript_codestyle_UnnecessaryBlock` reads object literals (`return { ... }`) as blocks. All 9 are
false positives. Turn it off.

### Agentlinter (27 issues)

It lints `AGENTS.md` as an agent prompt. It wants escape hatches on absolute rules (the rules are absolute on purpose),
a persona and a tool list. It also says `UPGRADING.md` doesn't exist, but it does. Turn off the tool.

### Single issues to ignore as false positives

| Issue | Why |
|---|---|
| `jackson_parse-error`, `tests/Devoctomy.Blazwiz.Sync.UnitTests/Configuration/Resources/sync.invalid.json` | Invalid on purpose: a test reads it. Better: exclude `**/*.invalid.json`. |
| `SonarCSharp_S1994`, `E2eProtectedProfileManager.cs:354`, `SyncKeyRotationService.cs:233` | Retry loops that leave by `break`, `return` or `throw`, with the counter used in those checks. |
| `SonarCSharp_S1854`, `SyncService.cs:73` (`current = null`) | Needed: the `catch` blocks read `current`, and `CanSyncAsync` can throw in the next pass of the loop. |
| `SonarCSharp_S3626`, `SyncScheduler.cs:172` | The `return` in the `catch` skips `RunProfileAsync` after the `finally`. Not redundant. |
| `SonarCSharp_S3440`, `E2eProfileIndexEntry.cs:39` | `null or 0 => null` maps 0 to "never". Not useless. |
| `SonarCSharp_S2696`, `DeployHooks.cs:26` (High) | A static cache of the portal settings, shared by all scenarios on purpose. |
| `SonarCSharp_S3898`, `EncryptedFileFormat.cs:165` | A private struct that is never compared. |
| `SonarCSharp_S3241`, `NotebookServiceTests.cs:216` | Several tests use the returned id (lines 48, 86, 101). |

## Suggested exclusions

A `.codacy.yml` at the root, if you want these kept in the repository:

```yaml
exclude_paths:
  - "**/wwwroot/lib/**"
  - "**/*.invalid.json"
  - ".github/instructions/**"
```

`.github/instructions/codacy.instructions.md` is written by the Codacy plugin, which adds it back whenever it is
removed. It is excluded so its markdownlint issues (45 on the first analysis) don't count.

## What's left

With these changes the issue list drops from 627 to roughly the 22 `Lizard_ccn-minor` notes, if you keep them, and the
5 long source files. Neither should be part of the gate.

Coverage still shows "Not set up". If this branch is meant to add coverage reporting, Codacy hasn't received a report
for `6943d59`.
