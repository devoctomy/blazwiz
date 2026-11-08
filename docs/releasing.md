# Releasing

Releases are made by the maintainer. Every `Devoctomy.Blazwiz.*` package and the template package are published
together, at one version (`docs/versioning.md`), by `.github/workflows/release.yml`. It runs no tests: it only
publishes a commit the CI workflow has passed, and waits for CI if it is still running for that commit.

The version is set in one place: `VersionPrefix` in `Directory.Build.props`. Every package takes it from there, and the
template package writes it into the apps it creates (`BlazwizVersion` in their `Directory.Packages.props`) when it is
packed. Published versions are `MAJOR.MINOR.PATCH` for a release and `MAJOR.MINOR.PATCH-beta.<run>` for a beta.

## A release

1. On a branch, run:

   ```powershell
   ./eng/bump-version.ps1                  # the next minor version, e.g. 0.1.0 -> 0.2.0
   ./eng/bump-version.ps1 -Version 1.0.0   # or a version of your choice
   ```

   It sets the version, turns the `Unreleased` section of `CHANGELOG.md` into the new version's section (and starts
   a new `Unreleased`), and moves each package's public API from `PublicAPI.Unshipped.txt` to
   `PublicAPI.Shipped.txt`. Review the changes, and for a major version add its breaking changes to `UPGRADING.md`.

   If the current version has not been released yet (it is not on nuget.org; betas don't count), the script says no
   bump is needed and offers to add the `Unreleased` changes to it instead: the version stays, the notes join its
   section, and the public API is moved. Answer `n` to bump anyway; `-Version` always bumps.
2. Push, let CI pass, and merge to main.
3. Run the Release workflow (Run workflow) on main.

The workflow refuses to release a version that already has a GitHub release, or that has no section in
`CHANGELOG.md`: run the bump script first. Then it:

1. Checks CI passed for the commit (waiting for it if it is still running).
2. Packs every package and the template at the version. From 1.0, packing fails on a binary breaking change against
   the latest release (`eng/get-validation-baseline.ps1`) unless it is recorded in the package's
   `CompatibilitySuppressions.xml`.
3. Pushes them to nuget.org with a short-lived API key from Trusted Publishing, skipping any already published (so a
   failed run can be run again).
4. Creates the tag `v<version>` on the commit and the GitHub release, with the version's changelog section as its notes
   and the packages attached.

If the base stack (`src/Devoctomy.Blazwiz.Aws/cloudformation`) changed, say so in the changelog, so apps deploy after
upgrading. Never remove an exported output or change its value in a minor version: apps' own stacks import them.

Packages cannot be deleted from nuget.org, only unlisted, so a broken release is fixed with a new version.

## A beta

Run the Release workflow on any other branch. It publishes `<version>-beta.<run number>`, e.g. `0.2.0-beta.12`, to
nuget.org only, and the run's summary gives the command to install it (`dotnet new install
Devoctomy.Blazwiz.Templates@<version>`). New apps from that template use those packages. If the version in
`Directory.Build.props` is already released, the beta is of the next minor version, so it sorts above the release:
NuGet orders `0.1.0-beta.12` below `0.1.0`.

A beta gets no tag and no GitHub release. The branch is deleted when it is merged, and a tag or release pointing at its
commits would be left behind, its links to the branch broken.

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
