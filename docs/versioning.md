# Versioning and public API

This is the policy for the `Devoctomy.Blazwiz.*` packages. It says what an app can rely on across upgrades, and what
counts as a breaking change. Finding U12 in `docs/reviews/2026-11-03-upgradeability-review.md` explains why it exists.

## Versions

- All packages, and the `Devoctomy.Blazwiz.Templates` template package, share **one version number**. A release
  publishes every package, even those that did not change. An app sets that version once, as `BlazwizVersion` in its
  `Directory.Packages.props`.
- Versions follow [Semantic Versioning](https://semver.org/): `MAJOR.MINOR.PATCH`.
  - **Patch:** fixes only. No change to the public API.
  - **Minor:** new features. Anything added is optional, and nothing public is removed or changed incompatibly.
  - **Major:** may break. Every break is listed in `UPGRADING.md`, with what to change.
- **Before 1.0** (`0.x` previews), any release may break, and nothing is kept for earlier builds: no data migrations
  and no compatibility code. Breaks are still listed in `CHANGELOG.md`.

## What is public

An app can depend on everything below. Changing any of it incompatibly needs a major version.

| Surface | Breaking | Not breaking |
|---|---|---|
| **C# API**: public types and members in every package (checked by PublicApiAnalyzers and package validation) | Removing or renaming a type or member. Changing a signature. Adding a member to a public interface that apps implement. Adding a required constructor parameter to a type apps create | Adding types, members, optional parameters or overloads. Adding constructor parameters to types only resolved through DI |
| **Component parameters**: `[Parameter]` properties of packaged Razor components | Removing or renaming one. Changing its type. Making it `[EditorRequired]` | Adding optional parameters |
| **Routes**: paths of packaged pages, such as `/login`, `/settings`, `/sync` | Changing or removing a path | Adding pages |
| **Extension points**: shell regions, navigation, settings sections, sign-in options, session lifecycle handlers, page replacement | Removing a region or hook, or changing when it is called | Adding regions or hooks |
| **Configuration**: section names and keys in `appsettings.json`, and builder methods such as `AddBlazwiz(...)` | Renaming or removing a key. Changing what a value means. Making an optional setting required | Adding optional settings with a default |
| **Theme tokens**: CSS custom properties named `--bw-*` | Removing or renaming a token, or changing what it styles | Adding tokens. Changing default colours or spacing |
| **Text resources**: keys and placeholders in each package's `.resx` | Removing or renaming a key. Changing its placeholders (their number, order or meaning) | Adding keys. Changing the default English wording |
| **Stored data**, from 1.0: browser storage names and formats, and what is kept in S3 and Secrets Manager | Any change an app or user must act on | Changes that a packaged data migration handles at sign-in |
| **AWS infrastructure**: outputs and requirements of the base CloudFormation stack | Removing an output. Needing a new resource or permission without a matching base stack release | Adding outputs or optional resources |

## What is not public

These can change in any release:

- Anything `internal`, and types in namespaces ending in `.Internal`.
- JS module paths and functions under `_content/Devoctomy.Blazwiz.*/`. Use the C# services instead.
- CSS class names inside packaged components. Theme through the `--bw-*` tokens, or replace the component (see
  "Replacing a page" in the README).
- Element ids, other than those documented as test hooks.
- Log messages and the text of exceptions that are not shown to users.

## Deprecation

- Before removing something public in a major version, mark it `[Obsolete]` in a minor version, with a message
  saying what to use instead.
- It stays for at least one minor release before it is removed.
- Obsolete text resource keys, routes and configuration keys keep working, and are listed in `CHANGELOG.md`.

## How it is enforced

- `Microsoft.CodeAnalysis.PublicApiAnalyzers` on every package: public API changes must be recorded in
  `PublicAPI.Unshipped.txt`, so they show up in review. They move to `PublicAPI.Shipped.txt` at release.
- `EnablePackageValidation`, from 1.0 against the latest release (`eng/get-validation-baseline.ps1`, passed as
  `PackageValidationBaselineVersion` when packing): `dotnet pack` fails on any binary breaking change, whatever the
  version. A deliberate break in a major version is recorded in the package's `CompatibilitySuppressions.xml`
  (generate it with `dotnet pack -p:GenerateCompatibilitySuppressionFile=true`); delete those files once that version
  is released. Before 1.0 there is no baseline, as any release may break.
- `FrameworkText.UnknownOverrideKeys()` and `MismatchedOverridePlaceholders()` (in `Devoctomy.Blazwiz.Testing`): an
  app's tests fail when one of its text overrides names a key that no longer exists, or uses placeholders that do not
  match (the template's `NotesTextTests` show how).
- `templatepack/test-template.ps1`: creates apps from the template with the packages built from the repository, and
  checks they build and pass their tests. With `-UpgradeFrom <folder>` it simulates upgrading an app from another
  copy of the framework, for testing a change.
- Upgrade tests: every change that affects apps comes with a test of upgrading across it, as part of its acceptance
  (AGENTS.md, "Upgrade tests").

## Stored data

From 1.0, a release that changes stored data (browser storage, IndexedDB, files the framework keeps) ships an
`IDataMigration` for it, added with `AddDataMigration`. Migrations run at sign in, before modules start, in version
order per area; the version reached is recorded on the device, per app or per user. An app never migrates
framework data by hand.
