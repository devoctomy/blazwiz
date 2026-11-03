# Releasing

Releases are made by the maintainer. Every `Devoctomy.Blazwiz.*` package and the template package are published
together, at one version (`docs/versioning.md`), by `.github/workflows/release.yml` when a `v*` tag is pushed.

## Before the tag

1. **Version.** Set `VersionPrefix` and `VersionSuffix` in `Directory.Build.props` (no suffix for a stable release),
   and the same version as `BlazwizVersion` in `template/Directory.Packages.props`. `./eng/check-versions.ps1`
   checks they match.
2. **Changelog.** Move the `Unreleased` section of `CHANGELOG.md` under the new version. For a major version, add its
   breaking changes, with what to do, to `UPGRADING.md`.
3. **Public API.** For each package, move the lines of `PublicAPI.Unshipped.txt` to `PublicAPI.Shipped.txt`, leaving
   only `#nullable enable` in `PublicAPI.Unshipped.txt`.
4. **Package validation.** From the second release, set `PackageValidationBaselineVersion` in
   `src/Directory.Build.props` to the previous release, so packing fails on a binary-breaking change in a minor or
   patch version.
5. **AWS stack.** If the base stack (`src/Devoctomy.Blazwiz.Aws/cloudformation`) changed, say so in the release
   notes, so apps deploy after upgrading. Never remove an exported output or change its value in a minor version:
   apps' own stacks import them (`docs/versioning.md`).
6. **Checks.** `./templatepack/test-template.ps1` passes locally (CI runs it too), including the upgrade tests that
   came with this release's changes (AGENTS.md, "Upgrade tests").

## The tag

Push a tag `v<version>`, e.g. `v1.2.0` or `v0.2.0-preview.1`. The release workflow:

1. Checks the tag matches the version.
2. Runs every check.
3. Packs every package and the template, and pushes them to nuget.org (the `nuget` environment's `NUGET_API_KEY`
   secret), skipping any already published.

Packages cannot be deleted from nuget.org, only unlisted, so a broken release is fixed with a new version.

## Setting up (once)

- A nuget.org API key allowed to push `Devoctomy.Blazwiz.*`, stored as the `NUGET_API_KEY` secret of a GitHub
  environment named `nuget`. Add required reviewers to the environment for a manual approval before each publish.
- The package IDs reserved under the Devoctomy prefix on nuget.org, if wanted.
