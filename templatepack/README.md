# Devoctomy.Blazwiz.Templates

A `dotnet new` template for a Blazor WebAssembly progressive web app built on the blazwiz framework packages: it signs
in with AWS Cognito, syncs files with S3 with end to end encryption, works offline, and comes with scripts to host it on
S3 and CloudFront.

## Usage

```
dotnet new install Devoctomy.Blazwiz.Templates
dotnet new blazwiz -n MyApp
```

To install a pre-release (beta), give its version:

```
dotnet new install Devoctomy.Blazwiz.Templates@0.2.0-beta.8
```

Installing again replaces the installed version; `dotnet new update` updates to the latest release.

Options:

- `--title`: shown in the browser tab, on the login page and in the PWA manifest (default: the name).
- `--description`: shown under the title on the login page (default `Sign in to continue.`).
- `--sync`: the sync module, with the Sync page and end to end encryption (default `true`).
- `--notes`: the notes example, a Files page and markdown note editor synced by the sync module (default `true`;
  needs the sync module, and is left out with `--sync false`).
- `--showcase`: a page of themed building blocks to copy from (default `true`).
- `--tests`: the unit and UI test projects (default `true`).
- `--no-restore`: don't restore the app's packages after creating it.

For example: `dotnet new blazwiz -n AcmePortal --title "Acme Portal" --showcase false`. Run
`dotnet new blazwiz --help` for all of them. The created app has a README on running, deploying and extending it.

## Upgrading

The app uses the `Devoctomy.Blazwiz.*` packages at the version in `BlazwizVersion` in its `Directory.Packages.props`.
To upgrade, change that version, build, and follow the [changelog](https://github.com/devoctomy/blazwiz/blob/main/CHANGELOG.md).
The template is released at the same version as the packages, so a new app uses the packages released with it.

Part of [blazwiz](https://github.com/devoctomy/blazwiz). Before 1.0 any release may break; see
[versioning](https://github.com/devoctomy/blazwiz/blob/main/docs/versioning.md).
