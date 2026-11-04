# Devoctomy.Blazwiz.Templates

A `dotnet new` template for a Blazor WebAssembly progressive web app built on the blazwiz framework packages: it signs
in with AWS Cognito, syncs files with S3 with end to end encryption, works offline, and comes with scripts to host it on
S3 and CloudFront.

## Usage

```
dotnet new install Devoctomy.Blazwiz.Templates
dotnet new blazwiz -n MyApp
```

Options:

- `--Sync`: the sync module, with the Sync page and end to end encryption (default `true`).
- `--Notes`: the notes example, a Files page and markdown note editor synced by the sync module (default `true`;
  needs `--Sync`).
- `--IncludeShowcase`: a page of themed building blocks to copy from (default `true`).
- `--IncludeTests`: the unit and UI test projects (default `true`).

Run `dotnet new blazwiz --help` for all of them. The created app has a README on running, deploying and extending it.

## Upgrading

The app uses the `Devoctomy.Blazwiz.*` packages at the version in `BlazwizVersion` in its `Directory.Packages.props`.
To upgrade, change that version, build, and follow the [changelog](https://github.com/devoctomy/blazwiz/blob/main/CHANGELOG.md).
The template is released at the same version as the packages, so a new app uses the packages released with it.

Part of [blazwiz](https://github.com/devoctomy/blazwiz). Before 1.0 any release may break; see
[versioning](https://github.com/devoctomy/blazwiz/blob/main/docs/versioning.md).
