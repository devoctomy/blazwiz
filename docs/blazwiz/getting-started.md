# Getting started

This page takes you from nothing to a running blazwiz app on your machine.

## What you need

- The **.NET 10 SDK**.
- For deploying later: the AWS CLI v2 and credentials for an AWS account (see [Deploying](deploying.md)). You do not
  need AWS to build and run the app locally.

## Install the template

```powershell
dotnet new install Devoctomy.Blazwiz.Templates
```

To install a pre-release (beta), name its version: `dotnet new install Devoctomy.Blazwiz.Templates@<version>`.

## Create an app

```powershell
dotnet new blazwiz -n AcmePortal --title "Acme Portal"
```

This creates a folder `AcmePortal` holding a solution (`AcmePortal.slnx`) and its projects. Everything named `MyApp`
in the template becomes your app's name:

- `MyApp` becomes `AcmePortal` in projects, namespaces and identifiers (your casing is kept).
- `myapp` becomes `acmeportal` in AWS names and browser storage names (lower case letters and numbers only).
- `MYAPP_` becomes `ACMEPORTAL_` in environment variable names.

### Template options

| Option | Default | What it does |
|---|---|---|
| `-n`, `--name` | the output folder's name | The app's name. |
| `--title` | the name | Shown in the browser tab, on the sign-in page, in the PWA manifest and in the copyright. Quotes, angle brackets, ampersands and backslashes are removed. |
| `--description` | `Sign in to continue.` | Shown under the title on the sign-in page. The same characters are removed. |
| `--tests` | `true` | Include the unit test and UI test projects. |
| `--showcase` | `true` | Include the Showcase page: themed buttons, alerts, forms and tables to copy from. |
| `--sync` | `true` | Include the sync module: files kept on the device, synced with S3, end to end encrypted, and the Sync page. |
| `--notes` | `true` | Include the notes example: notebooks of markdown pages, each with its own passphrase. It needs sync, so `--sync false` leaves it out too. |
| `--events` | `true` | Include user events: "Sign out everywhere" on the Profile page, and the user's other open devices told at once. This adds an AWS AppSync Events API to your stack, which is charged per use. |
| `--no-restore` | `false` | Don't restore the app's packages after creating it. |

The options are only read when the app is created. To change your mind later, add or remove the parts by hand. The
generated README explains how for user events.

Some examples:

```powershell
# The smallest app: sign in, profile and settings, no sync, no example pages.
dotnet new blazwiz -n Acme --sync false --showcase false --events false

# Sync, but without the notes example.
dotnet new blazwiz -n Acme --notes false
```

> There is no option to sync without end to end encryption. Every sync profile is encrypted.

## Run it

```powershell
cd AcmePortal
dotnet run --project AcmePortal.Portal
```

Open <https://localhost:7090> (or <http://localhost:5090>).

Until you deploy to AWS, the app uses a **placeholder sign in**: any username and password will do. This only happens
in the Development environment (when you `dotnet run`). A published build without Cognito settings refuses to start
instead, so nobody can sign in to a real deployment with any password. See [Sign in and sessions](sign-in-and-sessions.md).

To sign in against a real Cognito user pool while running locally, copy the deployed stack's ids into the
`AwsCognito` section of `wwwroot/appsettings.Development.json`.

## Run the tests

If you kept `--tests`:

```powershell
dotnet test AcmePortal.Portal.Application.UnitTests
dotnet test AcmePortal.Portal.UITests
```

The UI tests drive a real browser with Playwright. Install its browser once first:

```powershell
dotnet build AcmePortal.Portal.UITests
pwsh AcmePortal.Portal.UITests/bin/Debug/net10.0/playwright.ps1 install chromium
```

See [Testing your app](testing.md) for more.

## What you get

When you sign in you see:

- A **sidebar** with Dashboard, the template's example pages (Notebooks, Showcase, if included), Sync (with
  `--sync`) and Settings. Your name at the bottom opens the Profile page.
- A **top bar** with the light/dark theme toggle.
- A **Settings** page with Appearance and Offline mode sections, an About section, and with `--sync` the Sync, End to
  end encryption and Argon2id benchmark sections. A Language section appears once the app offers more than one
  language, and a This device section where the user names the device when the app can sign out everywhere.
- A **Profile** page where the user changes their password (and signs out on every device, when the app offers it: see
  [Sign in and sessions](sign-in-and-sessions.md#signing-out-everywhere)).

The next step is to understand [how the app is put together](app-structure.md), then to
[add a page of your own](views-and-view-models.md#adding-a-page).

## Upgrading the framework later

Change `BlazwizVersion` in your app's `Directory.Packages.props`, then build and run your tests. All
`Devoctomy.Blazwiz.*` packages share that one version. A major version lists what changed, and how to update, in the
framework's `UPGRADING.md`.

Don't edit the framework's files (`_content/Devoctomy.Blazwiz.*`). Change things through the
[extension points](shell-and-extension-points.md) instead, so an upgrade replaces the framework cleanly.
