# Releasing

Releases are made by the maintainer. Every `Devoctomy.Blazwiz.*` package and the template package are published
together, at one version (`docs/versioning.md`), by `.github/workflows/release.yml`. It runs no tests: it only
publishes a commit the CI workflow has passed, and waits for CI if it is still running for that commit.

- **Release**: push a tag `v<version>` on a commit on main.
- **Beta of a branch**: run the Release workflow by hand (Run workflow) on the branch. It publishes
  `<version>-beta.<run number>`, e.g. `0.1.0-preview.1-beta.12`, and new apps from that template use those packages.
  (NuGet versions cannot contain `_`, so it is `-beta`.)

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
6. **Checks.** CI has passed for the commit to be released (the workflow checks), including the upgrade tests that
   came with this release's changes (AGENTS.md, "Upgrade tests").

## The tag

Push a tag `v<version>`, e.g. `v1.2.0` or `v0.2.0-preview.1`, on a commit on main. The release workflow:

1. Checks CI passed for that commit (waiting for it if it is still running), and stops if not.
2. Checks the commit is on main and the tag matches the version.
3. Packs every package and the template, gets a short-lived nuget.org API key through Trusted Publishing, and pushes
   them to nuget.org, skipping any already published.

Packages cannot be deleted from nuget.org, only unlisted, so a broken release is fixed with a new version.

## Setting up (once)

Publishing uses nuget.org's [Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing):
GitHub gives the workflow a short-lived token, which nuget.org exchanges for an API key valid for an hour. No API key
is stored anywhere.

- **nuget.org**: under your user name, Trusted Publishing, add a policy: repository owner `devoctomy`, repository
  `blazwiz`, workflow file `release.yml` (the file name only), environment `nuget`. Scopes: Push, "Push new packages
  and package versions" (the first release creates every package ID; "only new package versions" would refuse them),
  Unlist / relist off. Glob pattern: `Devoctomy.Blazwiz.*`, which covers every package and the template package and
  nothing else. A policy for a private repository starts out active for 7 days, and becomes permanent with the first
  successful publish.
- **GitHub**: an environment named `nuget`, with the secret `NUGET_USER` set to your nuget.org profile name (not your
  email address). Add required reviewers to the environment for a manual approval before each publish.
- The package IDs reserved under the Devoctomy prefix on nuget.org, if wanted.
