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

blazwiz is a Blazor WebAssembly PWA that signs in with AWS Cognito, syncs files to S3 with optional end to end
encryption, and works offline. It is also a `dotnet new` template (`.template.config/`, `templatepack/`).

It is being restructured from "a template you fork" into "a framework you upgrade": NuGet packages
(`Devoctomy.Blazwiz.*`) plus a small template app. The plan, the findings (U*, T*) and the decisions (D*) are in
`docs/reviews/2026-11-03-upgradeability-review.md`. Read it before structural work, and tick off its checkboxes as
items are completed.

- `blazwiz.Core`: framework logic (cryptography, E2E encryption, sync engine, offline mode, browser storage clients
  and their JS modules in `wwwroot/js`). No UI.
- `blazwiz.Portal.Application`: view models (Blazing.Mvvm) and services. Unit testable, no Razor.
- `blazwiz.Portal.Models`: DTOs and settings. No logic.
- `blazwiz.Portal`: Razor pages, layouts, components and `wwwroot`. Markup only: each page inherits
  `MvvmComponentBase<TViewModel>`, and the logic lives in the view model.
- `cloudformation/`: the AWS stack (CloudFront, S3, Cognito, IAM) and deploy scripts.
- `docs/versioning.md`: the SemVer policy and what counts as public API.

The project is in development and unreleased. Nobody uses it yet.

## Design rules

- **No backward compatibility before 1.0.** Never add or keep code for earlier builds, generated apps or stored
  data: no migrations, no legacy keys, no shims. Change formats and names freely, and remove old compatibility code
  when you find it.
- **No static helper classes.** Logic goes in injectable services, as an interface plus an implementation,
  registered through an `Add*` extension method.
- **No hand-rolled cryptography or protocols.** Use official libraries, for example BouncyCastle, and
  `Amazon.Extensions.CognitoAuthentication` for SRP and Cognito flows.
- **Notes is an example, not the framework.** The notes feature (Files page, note editor, markdown services) shows
  how to use sync and storage. Framework code must not mention notes in code, names or user-facing text.
- **Name browser storage with short names only.** The storage clients add the app's prefix
  (`IAppStorageKeys`, set up with `AddBlazwizApp`). Never write a literal like `"blazwiz_session"`.
- **Use `TimeProvider`** for time, and `FakeTimeProvider` in tests.
- Optional template content uses comment markers such as `//#if (IncludeNotes)`. Keep the repository building with
  everything included. While restructuring, prefer extension points over new `#if` blocks in shared classes.

## Keeping the repository a valid template

- Write the app name as `blazwiz`. The only other forms allowed are `Blazwiz` in identifiers and `BLAZWIZ_` in
  environment variable names. The template replaces all of them.
- Where the title is displayed (rather than the name), add the text just before it to the `appTitle` symbol's
  `onlyIf` list in `.template.config/template.json`.
- When adding or removing files that belong to an optional feature, update the matching `exclude` list in
  `.template.config/template.json`.
- Files that are only for this repository must not end up in generated apps: add them to the `exclude` list in
  `.template.config/template.json`, as this file is.

## Build and test

Prerequisites: .NET 10 SDK. The first time, install the Playwright browser:

```powershell
dotnet build blazwiz.Portal.UITests
pwsh blazwiz.Portal.UITests/bin/Debug/net10.0/playwright.ps1 install chromium
```

All four suites must pass before work is considered done, **including the UI tests**:

```powershell
dotnet build blazwiz.slnx
dotnet test blazwiz.Core.UnitTests
dotnet test blazwiz.Core.IntTests
dotnet test blazwiz.Portal.Application.UnitTests
dotnet test blazwiz.Portal.UITests
```

- The UI tests start the portal themselves (`dotnet run -c Release` on a free port). They take about two minutes.
  The portal's output goes to `blazwiz.Portal.UITests/bin/Debug/net10.0/TestResults/portal.log`. Don't build while
  they run.
- Integration scenarios tagged `@aws` are skipped unless the `BLAZWIZ_INTTEST_AWS_*` environment variables are set.
  Skipping them locally is expected. The maintainer tests real S3 sync in the deployed app.
- Warnings are errors (`Directory.Build.props`), and StyleCop runs on every project. Disabled rules are listed in
  `.editorconfig`, and SA1633 (file header) is also suppressed in `Directory.Build.props`. Fix warnings. Don't
  suppress them without the maintainer's agreement.
- `./templatepack/test-template.ps1` packs the template, creates apps with awkward names and options, and checks that
  each one builds, passes its tests and has no `blazwiz` left in it. Run it after changing template content or
  `.template.config/`.

## Deploying

`cloudformation/deploy.ps1` creates or updates real AWS resources. Never run `deploy.ps1`, `remove.ps1` or
`test-auth.ps1` unless the maintainer asks for that exact action.
