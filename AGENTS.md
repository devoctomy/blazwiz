# AGENTS.md

Instructions for AI coding agents working in this repository.

## Rules

1. **Never run git commands that change anything, unless the user explicitly asks for that exact action in their
   current message.** This includes `add`, `commit`, `amend`, `reset`, `restore`, `checkout`/`switch`, `stash`,
   `rebase`, `merge`, `cherry-pick`, `rm`, `mv`, `tag`, `fetch`, `pull`, `push`, and creating or deleting branches. It
   also covers anything that changes GitHub: `gh pr create/merge/close`, `gh api` writes, releases and repository
   settings.
   - Read-only commands are fine: `git status`, `git log`, `git diff`, `git show`, `git ls-files`,
     `git check-ignore`, `git branch --list`, `gh pr view`.
   - A question about the release flow ("then we commit and push?") is a question, not an instruction.
   - Leave changes uncommitted in the working tree. The maintainer reviews, commits and pushes.
   - To delete or move tracked files, use plain file operations, not `git rm` or `git mv`.
2. **No AI attribution.** Never add `Co-Authored-By`, "Generated with…" or similar lines to commits, PRs, tags or
   files.
3. **Never create or push tags, or publish packages.** Tags and NuGet pushes publish releases. They belong to the
   maintainer.
4. If an action is blocked or fails, stop and report it. Don't substitute a different or more destructive action.
5. **Report honestly how things were checked.** Say exactly what was run and how: which suites, which
   configuration, what was skipped and why. A pass with rules or tests switched off is not a pass. If something
   failed and you could not find out why, say so.
6. **When reviewing the maintainer's own edits, list the fixes.** Don't hand back a rewritten copy of their work.

## Project

blazwiz is a framework for Blazor WebAssembly PWAs that sign in with AWS Cognito, sync files to S3 with end
to end encryption, and work offline. It ships as NuGet packages (`Devoctomy.Blazwiz.*`) and a `dotnet new` template
that creates an app built on them.

The restructure from "a template you fork" into "a framework you upgrade" is planned in
`docs/reviews/2026-11-03-upgradeability-review.md`, with the findings (U*, T*) and decisions (D*). Read it before
structural work, and tick off its checkboxes as items are completed.

- `src/Devoctomy.Blazwiz.<Package>`: the packages. `Core` (app identity, storage names, offline mode, text, coded
  errors), `Cryptography`, `Encryption` (E2E keys), `Browser` (storage, theme, navigation, WebAuthn and their JS),
  `Auth` (sign-in contract, sessions, lifecycle), `Auth.Cognito`, `Sync` (the engine and its JS modules), `UI` (the
  app shell, its pages and extension points), `UI.Sync` (the sync module's UI and wiring) and `Testing` (helpers for
  apps' tests). The package table in the plan says which depends on which; keep it that way.
- In `UI` and `UI.Sync`, Razor files are markup only: each view inherits `MvvmComponentBase<TViewModel>`, and the
  logic lives in the view model.
- `tests/Devoctomy.Blazwiz.<Package>.UnitTests`: one unit test project per package, referencing only that package
  (and `tests/Devoctomy.Blazwiz.TestSupport` for shared fakes). `tests/Devoctomy.Blazwiz.Sync.IntTests`: Reqnroll
  integration tests of sync.
- `template/`: the template app (`MyApp.Portal`, `.Application`, `.Models`, their tests), `.template.config/` and
  the AWS stack (`template/cloudformation`). In this repository it builds against the packages' source
  (`template/Directory.Build.targets`); generated apps use the packages.
- `templatepack/`: packs the template; `test-template.ps1` checks packages and template together.
- `docs/versioning.md`: the SemVer policy and what counts as public API.

The project is in development and unreleased. Nobody uses it yet.

## Design rules

- **No backward compatibility before 1.0.** Never add or keep code for earlier builds, generated apps or stored
  data: no migrations, no legacy keys, no shims. Change formats and names freely, and remove old compatibility code
  when you find it.
- **No static helper classes.** Logic goes in injectable services, as an interface plus an implementation,
  registered through an `Add*` extension method. Each package registers its own services, view models and
  configuration section.
- **No hand-rolled cryptography or protocols.** Use official libraries, for example BouncyCastle, and
  `Amazon.Extensions.CognitoAuthentication` for SRP and Cognito flows.
- **Notes is an example, not the framework.** The notes feature (Files page, note editor, markdown services) lives in
  the template app and shows how to use sync and storage. Packages must not mention notes in code, names or
  user-facing text.
- **Name browser storage with short names only.** The storage clients add the app's prefix
  (`IAppStorageKeys`, set up with `AddBlazwizApp`). Never write a literal like `"myapp_session"`.
- **Use `TimeProvider`** for time, and `FakeTimeProvider` in tests.
- **Unit tests** are named `Given<State>_When<Action>_Then<Outcome>`, and every test method marks its parts with
  `// Arrange`, `// Act` and `// Assert` comments (`// Act & Assert` where the assert does the acting, e.g.
  `Assert.Throws`). Unit test projects end in `.UnitTests`, one per package.
- **Packages bring their own browser assets.** JS goes in ES modules imported by the service that uses them, or in
  the package's JS initializer (`wwwroot/<PackageId>.lib.module.js`), never in the app's `index.html` or `wwwroot`.
  Modules plug into the shell's extension points (shell regions, Settings sections, sign-in options, session
  lifecycle handlers, navigation items, page assemblies, page replacement) instead of `#if` blocks in shared classes.
- User-facing text comes from `.resx` resources (`UiText`, `SyncUiText`), never literals, so apps can reword and
  translate it. Sidebar items use `NavigationItem.TextResource`. `PseudoLocalizationTests` (template UI tests) fails
  on visible framework text that does not come from resources.

## Keeping the template valid

- In `template/`, write the app name as `MyApp` (projects, namespaces, identifiers), `myapp` (AWS names, browser
  storage) or `MYAPP_` (environment variables). The template replaces all of them. `blazwiz` and `Devoctomy.Blazwiz`
  name the framework and are left alone.
- Where the title is displayed (rather than the name), add the text just before it to the `appTitle` symbol's
  `onlyIf` list in `template/.template.config/template.json`.
- Optional content uses comment markers: `//#if (IncludeSync)`, `<!--#if (IncludeNotes)-->`, `@*#if (IncludeSync)*@`.
  Where both branches are code, use C# `#if (IncludeNotes)` / `#else`: `template/Directory.Build.targets` defines
  every option for the repository build, and `dotnet new` resolves the directives in generated apps. Keep markers out
  of projects that use StyleCop; put them in `Program.cs`, Razor files or project files.
- When adding or removing files that belong to an optional feature, update the matching `exclude` list in
  `template.json`. Files only for this repository must be excluded there too (as `Directory.Build.targets` is).

## Build and test

Prerequisites: .NET 10 SDK. The first time, install the Playwright browser:

```powershell
dotnet build template/MyApp.Portal.UITests
pwsh template/MyApp.Portal.UITests/bin/Debug/net10.0/playwright.ps1 install chromium
```

Everything must pass before work is considered done, **including the UI tests**:

```powershell
dotnet build blazwiz.slnx
dotnet test blazwiz.slnx --no-build --filter "FullyQualifiedName~UnitTests"
dotnet test blazwiz.slnx --no-build --filter "FullyQualifiedName~IntTests"
dotnet test blazwiz.slnx --no-build --filter "FullyQualifiedName~UITests"
./templatepack/test-template.ps1 -SkipRepositoryTests   # the packages and template: generated apps built and tested
```

These are the steps CI runs (`.github/workflows/ci.yml`). Test namespaces must contain the project's suffix
(`.UnitTests`, `.IntTests`, `.UITests`), or the filters miss them.

- The UI tests start the portal themselves (`dotnet run -c Release` on a free port). They take about two minutes.
  The portal's output goes to `template/MyApp.Portal.UITests/bin/Debug/net10.0/TestResults/portal.log`. Don't build
  while they run.
- Integration scenarios tagged `@aws` are skipped unless the `BLAZWIZ_INTTEST_AWS_*` environment variables are set.
  Skipping them locally is expected. The maintainer tests real S3 sync in the deployed app.
- Warnings are errors (`Directory.Build.props`), and StyleCop runs on the packages. Rules turned off for the whole
  solution (including SA1633, the file header) are in `analyzers.globalconfig`, not `.editorconfig`: sections in
  `.editorconfig` stop applying when a build sees the folder with different casing (`E:\Source` against
  `E:\source`). Fix warnings. Don't suppress them without the maintainer's agreement.
- Each package lists its public API in `PublicAPI.Unshipped.txt` (PublicApiAnalyzers). A public API change fails the
  build until the list is updated, so it is reviewed: add or remove the lines the analyzer reports (RS0016, RS0017).
  Never edit `PublicAPI.Shipped.txt`; the maintainer moves entries there at a release.
- If a build fails in the static web assets compression step ("An item with the same key has already been added"),
  the cause is usually MSBuild node reuse: build again with `MSBUILDDISABLENODEREUSE=1`. The UI tests already start
  the portal that way. See `docs/techdebt/static-web-assets-compression.md`.
- `./templatepack/test-template.ps1` packs the packages (with a version unique to the run) and the template, creates
  apps with awkward names and every option, and checks each one has no `MyApp` left, builds against the packages and
  passes its tests. Run it after changing packages' public surface, template content or `.template.config/`.

## Upgrade tests

Every change that affects apps comes with a test of upgrading across it, as part of its acceptance. Bumping the
package version is ordinary .NET and needs no test; the change does. Before 1.0 there is nothing to upgrade from
(stored data and earlier builds are not kept), so this is about the change, not about earlier development snapshots.

- **Stored data** (browser storage, IndexedDB, files the framework keeps): an `IDataMigration`, with a unit test that
  runs it on data in the old format (from 1.0).
- **Text**: a renamed or removed key, or changed placeholders, is caught by `FrameworkText.UnknownOverrideKeys` and
  `MismatchedOverridePlaceholders`; add an override test for the change and say what to do in `UPGRADING.md`.
- **Public API**: PublicApiAnalyzers and package validation catch breaks; a deliberate one needs a major version and
  an `UPGRADING.md` entry.
- **Anything else apps see** (configuration keys, routes, extension points, theme tokens, the AWS base stack):
  simulate it with `./templatepack/test-template.ps1 -UpgradeFrom <copy of the repository before the change>`, and
  test what changed.

## CI and releases

- `.github/workflows/ci.yml` runs on every push and pull request: build, then unit, integration and UI tests as
  separate `dotnet test` steps, and in a parallel job `templatepack/test-template.ps1 -SkipRepositoryTests` (packages
  and template). Keep them passing locally first: CI cannot be run from here.
- `.github/workflows/release.yml` publishes to nuget.org only a commit CI has passed: a `v*` tag on main publishes the
  version, a manual run on another branch publishes `<version>-beta.<run>` (`docs/releasing.md`). Never push tags or
  run the release yourself (rule 3).
- `eng/check-versions.ps1` checks the version in `Directory.Build.props` matches `BlazwizVersion` in
  `template/Directory.Packages.props`. Change both together.
- Every user-visible change goes in the `Unreleased` section of `CHANGELOG.md`; breaking ones (from 1.0) also in
  `UPGRADING.md`. A change to stored data (from 1.0) ships an `IDataMigration`.

## Deploying

The AWS base stack and its scripts are in `src/Devoctomy.Blazwiz.Aws/cloudformation`; apps run them through
`template/cloudformation/deploy.ps1`, `remove.ps1` and `test-auth.ps1`. They create, change or delete real AWS
resources. Never run any of them, or any AWS command, unless the maintainer asks for that exact action: no
credentials are kept here, and the maintainer tests deployments by hand.
