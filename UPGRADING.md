# Upgrading an app

Apps created from the `blazwiz` template use the `Devoctomy.Blazwiz.*` packages, all at one version.

## How to upgrade

1. Change `BlazwizVersion` in the app's `Directory.Packages.props` to the new version.
2. Read the sections below for every major version between the old one and the new one, and do what they say.
3. Build, and run the app's tests. Its text tests (`FrameworkText.UnknownOverrideKeys` and
   `MismatchedOverridePlaceholders`) fail if one of its text overrides no longer matches the framework's text.
4. Deploy (`./cloudformation/deploy.ps1`): the AWS base stack comes with the framework (`Devoctomy.Blazwiz.Aws`),
   so a new version may bring infrastructure changes; the release notes say when. The app's own stack
   (`cloudformation/app.yaml`) is untouched.

Minor and patch versions need nothing more than step 1: they add, never break (`docs/versioning.md`). Stored data
is migrated by the packages at sign in, from 1.0 on.

Don't edit the framework's files (`_content/Devoctomy.Blazwiz.*`). Change what you need through the extension
points, text overrides and `--bw-*` theme tokens, so upgrades replace the framework cleanly.

## Before 1.0

Previews (`0.x`) may break in any release, and data kept in the browser by an earlier preview is not migrated: sign
in again, and let sync download the files. `CHANGELOG.md` lists every break.

## Breaking changes by version

None yet: 1.0 has not been released.
