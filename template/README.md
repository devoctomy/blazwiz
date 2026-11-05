# MyApp

A **Blazor WebAssembly static PWA** that signs in with **AWS Cognito**, built on the
[blazwiz](https://github.com/devoctomy/blazwiz) framework packages (`Devoctomy.Blazwiz.*`).

> **Status:** deployed builds sign in with AWS Cognito. Running locally without Cognito settings uses a
> placeholder login (any username / password). See [Authentication](#authentication).

## Solution structure

| Project | Purpose |
|---|---|
| `MyApp.Portal` | The Blazor WASM PWA: `Program.cs`, your pages and `wwwroot`. Views only. |
| `MyApp.Portal.Application` | Your view models (Blazing.Mvvm) and services. Unit testable. |
| `MyApp.Portal.Models` | Your DTOs and settings. No logic. |
<!--#if (IncludeTests)-->
| `MyApp.Portal.Application.UnitTests` | xUnit + Moq. Mirrors the `Portal.Application` folder structure. |
| `MyApp.Portal.UITests` | Playwright (MSTest) end-to-end tests. Starts the portal automatically. |
<!--#endif-->

The framework (the app shell, sign in, settings, theme, sync and encryption) comes from NuGet packages. Your code
plugs into it, so it can be upgraded without touching your code:

| Package | What it gives you |
|---|---|
| `Devoctomy.Blazwiz.UI` | The app shell: layout, sign-in, profile and settings pages, theme, and the extension points below. |
| `Devoctomy.Blazwiz.Auth.Cognito` | Signing in with AWS Cognito, and the session's AWS credentials. |
<!--#if (IncludeSync)-->
| `Devoctomy.Blazwiz.UI.Sync` | The sync module: files kept in the browser or a folder, synced with S3, end to end encryption, the Sync page. |
<!--#endif-->
| `Devoctomy.Blazwiz.Testing` | Test helpers: the framework's real text, and checks that your text overrides still match it. |

They bring in the rest (`Core`, `Cryptography`, `Encryption`, `Browser`, `Auth`, `Sync`). Package
versions are managed centrally in `Directory.Packages.props`.

### Upgrading the framework

Change `BlazwizVersion` in `Directory.Packages.props`, then build and run the tests. Every `Devoctomy.Blazwiz`
package has the same version. Minor and patch versions don't break your code; a major version lists what changed and
how to update in the framework's `UPGRADING.md`. Don't edit the framework's files (`_content/Devoctomy.Blazwiz.*`):
change things through the extension points instead, so upgrades replace them cleanly.

## Getting started

Prerequisites: .NET 10 SDK.

```powershell
dotnet run --project MyApp.Portal
```

Open <https://localhost:7090> (or <http://localhost:5090>) and sign in with any username and password.

To sign in against a real Cognito user pool locally instead, copy the deployed stack's ids into the
`AwsCognito` section of `wwwroot/appsettings.Development.json` (the data bucket already allows the local
origins for CORS).

<!--#if (IncludeTests)-->
## Running the tests

```powershell
dotnet test MyApp.Portal.Application.UnitTests
dotnet test MyApp.Portal.UITests
```

**UI tests** start the portal themselves on a random free port (its output goes to
`TestResults/portal.log`), and save screenshots to `TestResults/Screenshots`. The first time, install
the Playwright browser:

```powershell
dotnet build MyApp.Portal.UITests
pwsh MyApp.Portal.UITests/bin/Debug/net10.0/playwright.ps1 install chromium
```

To test a portal that is already running (or deployed), set `MYAPP_UITEST_URL`, e.g.
`$env:MYAPP_UITEST_URL = "http://localhost:5090"`. Timeouts are deliberately short (10s per step,
30s per test, see `TestBase.cs` and `.runsettings`).
<!--#endif-->

## Building out

`MyApp.Portal/Program.cs` composes the app: `AddBlazwizApp` (its name and ID), `AddBlazwizUi` (the shell),
`AddBlazwizCognito` (sign in)<!--#if (IncludeSync)-->, `AddBlazwizSyncModule` (sync and its profiles)<!--#endif-->, and
your own services in `AddAppServices`.

### Add a page

1. **View model**: `MyApp.Portal.Application/ViewModels/ThingsViewModel.cs`, deriving from `ViewModelBase`.
   It is registered automatically.
2. **Page**: `MyApp.Portal/Pages/Things.razor` with `@page "/things"` and
   `@inherits MvvmComponentBase<ThingsViewModel>`. Use `<PageHeader Title="Things" Icon="bi-box" />`
   for a consistent heading and browser tab title.
3. **Navigation**: add a `NavigationItem` with `services.AddNavigationItem(...)` in
   `AppServiceCollectionExtensions.AddAppServices`. Its `Order` sets its place in the sidebar, and it also appears as
   a quick link on the dashboard.
<!--#if (IncludeTests)-->
4. **Tests**: `MyApp.Portal.Application.UnitTests/ViewModels/ThingsViewModelTests.cs`.
<!--#endif-->

The framework's own parts plug into the app the same way, so you can add to them or change them in `Program.cs`:
`AddShellComponent` (banners, top bar buttons, floating items, overlays), `AddSettingsSection`, `AddSignInOption`,
`AddSessionLifecycleHandler`, `AddPageAssembly`, and `ReplacePage` or `DisablePage` for the framework's pages
(`BlazwizPages`). Text comes from resources you can reword or translate with
`AddBlazwizText(text => text.AddOverrides<YourText>())`.
<!--#if (IncludeShowcase)-->

The `Showcase` page contains themed buttons, alerts, forms and tables to copy from. Delete it when you
no longer need it.
<!--#endif-->
<!--#if (IncludeNotes)-->

### Notes

The **Notebooks** page, its pages and the note editor are an example of using the sync module: notebooks of
markdown pages, organised into folders, kept in the browser for the signed in user and synced to their own prefix in
the data bucket.

- **Notebooks**: each notebook is a sync profile with its own passphrase and keys. The default notebook is the `notes`
  profile, set up in code (`Sync/NotesSyncProfile.cs`), whose passphrase is set the first time. Every other notebook
  is a protected profile the user creates (`NotebookService`), with its pages under `profiles/<id>/` and its name kept
  encrypted inside it, so a locked notebook is shown by its id.
- **Storage**: pages are `.md` files and folders are paths (`notes/Work/Ideas.md`), kept by the notebook's file store,
  which encrypts them. `NoteServiceFactory` creates a `NoteService` for each notebook over its store, so every change
  is tracked for sync automatically.
- **Wording**: the sync module says "files"; `NotesText.resx` overrides its text to say "notes".
  `NotesTextTests` checks every override still names framework text after an upgrade.
- **Editor**: the toolbar's formatting (`MarkdownFormatService`) toggles, like a word processor. The
  preview is rendered by [Markdig](https://github.com/xoofx/markdig) with raw HTML disabled and unsafe
  link URLs removed. `Ctrl+S` saves, and leaving with unsaved changes asks what to do with them.
<!--#endif-->

<!--#if (IncludeSync)-->
### End to end encryption

Every sync profile is encrypted end to end with its own keys and its own passphrase, so files on the device and in S3
can only be read once the user has entered it. A profile's id (`UseKeyId`, a GUID) names its keys, kept in the user's
own Secrets Manager secrets, or in the browser when no user pool is configured (local development). Until its
passphrase is set or entered the profile is locked: a banner points to Settings to set a first passphrase, and a
page wrapping the profile's content in `E2eUnlockPanel` asks for it there. There is no recovery: a forgotten
passphrase loses the profile's files. Syncing without encryption is not supported.

<!--#endif-->
### Add a service

Put your services in `MyApp.Portal.Application/Services` (interface in `Services/Interfaces`) and register them in
`AppServiceCollectionExtensions.AddAppServices`.
<!--#if (IncludeTests)-->
Add a matching test file in `MyApp.Portal.Application.UnitTests`.
<!--#endif-->

### Languages

The framework's text is English. To offer more languages, list them in `wwwroot/appsettings.json` (the first is the
default) and add their text as resources:

```json
"Text": { "SupportedCultures": [ "en-GB", "fr-FR" ] }
```

Settings then shows a Language choice, kept on the device; until the user picks one, the browser's language is used
when the app has it. Translate the framework's text with a resource of your own, e.g. `Text/AppText.fr.resx` with the
framework's keys (`Settings.Language.Title`, ...), added with `AddBlazwizText(text => text.AddOverrides<AppText>())`.
Dates and numbers follow the language without any work.

### Passwords

Signed-in users change their password on the Profile page; the sign-in page links to resetting a forgotten one
(`/forgot-password`, sent by email by Cognito). Replace or turn off that page like any framework page, e.g.
`builder.Services.DisablePage(BlazwizPages.ForgotPassword)`, which also hides the link.

### Theming

The app uses Bootstrap 5.3 colour modes (`data-bs-theme="light|dark"` on `<html>`). The framework's colours are CSS
variables (`--bw-*`); override them in `wwwroot/css/app.css` to re-brand, e.g. `--bw-accent` and `--bw-accent-2`.
The saved theme is applied by a small script in `index.html` before Blazor starts, so there is no flash of the wrong
theme.

### Configuration

`wwwroot/appsettings.json` holds `AppSettings` (title, description, copyright), `AwsCognito`
(region, user pool, client and identity pool ids) and `AwsS3` (region, data bucket).
`appsettings.Development.json` overrides values when running locally and is not published; it lightens the Argon2id
settings (`Argon2id`), so passphrases are quick to enter while developing and in the UI tests. When
deploying, `deploy.ps1` generates `appsettings.Production.json` with the stack's ids (git ignored).
<!--#if (IncludeSync)-->

Sync profiles (what is synced, where it is kept on the device, and the id of its keys) are classes deriving
from `SyncProfileDefinition`, found in the app's assemblies by `AddProfilesFromAssemblyContaining` in `Program.cs`.
<!--#if (IncludeNotes)-->
The notes example's is `MyApp.Portal.Application/Sync/NotesSyncProfile.cs`.
<!--#else-->
The app's is `MyApp.Portal/Sync/FilesSyncProfile.cs`.
<!--#endif-->
Add a profile by adding a class like it. The other sync settings (when to sync after a change, how long the sync log
is kept) are in `MyApp.Portal/sync.json`, embedded in the app and read by `UseEmbeddedConfiguration`. See the
`Devoctomy.Blazwiz.Sync` README for both.
<!--#endif-->

## Authentication

`AddBlazwizCognito` signs in with Cognito when `AwsCognito` has a region, user pool id and app client id (deployed
builds get these from `appsettings.Production.json`), using SRP through AWS's
[`Amazon.Extensions.CognitoAuthentication`](https://github.com/aws/aws-sdk-net-extensions-cognito), so the password
is never sent. Otherwise it keeps the placeholder sign-in (any credentials), e.g. for local development and the UI
tests.

## Deploying to AWS

The scripts are in `cloudformation/`; see [`cloudformation/README.md`](cloudformation/README.md). The base stack
(CloudFront, S3, Cognito) comes with the framework (`Devoctomy.Blazwiz.Aws`), so upgrading the framework upgrades it;
add the app's own AWS resources to `cloudformation/app.yaml`. You need the AWS CLI v2, the .NET 10 SDK and credentials
for the target account.

```powershell
# Deploy (asks for confirmation, then for the test user's password)
./cloudformation/deploy.ps1 -Region eu-west-2 -TestUserEmail me@example.com

# Check it all works
./cloudformation/test-auth.ps1 -Region eu-west-2 -Email me@example.com

# Re-publish just the portal after code changes
./cloudformation/deploy.ps1 -Region eu-west-2 -SkipInfrastructure

# Tear it down
./cloudformation/remove.ps1 -Region eu-west-2
```

All scripts take `-AppName` (default `myapp`), `-Environment` (default `dev`), `-Region` and `-AwsProfile`. The stack
is named `<AppName>-<Environment>`, so several environments can share an account.
