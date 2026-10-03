# blazwiz

Boilerplate for a **Blazor WebAssembly static PWA** that authenticates with **AWS Cognito** and can
optionally use AWS resources directly from the browser.
<!--#if (false)-->

This repository is also a `dotnet new` template, so new apps of this shape can be created in seconds
and built out from there. See [Creating a new app from this template](#creating-a-new-app-from-this-template).
<!--#endif-->

> **Status:** deployed builds sign in with AWS Cognito. Running locally without Cognito settings uses a
> placeholder login (any username / password). See [Authentication](#authentication).

## Solution structure

| Project | Purpose |
|---|---|
| `blazwiz.Core` | .NET 10 class library for all backend / domain logic. No UI dependencies. |
| `blazwiz.Portal` | The Blazor WASM PWA: pages, layouts, components, `wwwroot`. Views only. |
| `blazwiz.Portal.Application` | Unit testable portal logic: view models (Blazing.Mvvm) and services. |
| `blazwiz.Portal.Models` | DTOs / settings used by the portal. No logic. |
<!--#if (IncludeTests)-->
| `blazwiz.Core.UnitTests` | xUnit + Moq. Mirrors the `Core` folder structure, one test file per class. |
| `blazwiz.Core.IntTests` | Reqnroll (Gherkin) integration tests. Scenarios tagged `@aws` are skipped unless AWS credentials are set. |
| `blazwiz.Portal.Application.UnitTests` | xUnit + Moq. Mirrors the `Portal.Application` folder structure. |
| `blazwiz.Portal.UITests` | Playwright (MSTest) end-to-end tests. Starts the portal automatically. |
<!--#endif-->

Package versions are managed centrally in `Directory.Packages.props`.

### How the layers fit together

```
Portal (Razor views)  ──binds to──▶  Portal.Application (ViewModels + services)  ──uses──▶  Core
        │                                   │
        └── implements browser abstractions ┘   (INavigationService, IBrowserInteropService)
                                            Portal.Models (DTOs) are shared by all of the above
```

Pages contain markup only. Each page `@inherits MvvmComponentBase<TViewModel>` and all logic lives
in the view model, which only depends on interfaces, so it can be unit tested without a browser.

## Getting started

Prerequisites: .NET 10 SDK.

```powershell
dotnet run --project blazwiz.Portal
```

Open <https://localhost:7090> (or <http://localhost:5090>) and sign in with any username and password.

To sign in against a real Cognito user pool locally instead, copy the deployed stack's ids into the
`AwsCognito` section of `wwwroot/appsettings.Development.json` (the data bucket already allows the local
origins for CORS).

<!--#if (IncludeTests)-->
## Running the tests

```powershell
dotnet test blazwiz.Core.UnitTests
dotnet test blazwiz.Core.IntTests
dotnet test blazwiz.Portal.Application.UnitTests
dotnet test blazwiz.Portal.UITests
```

**UI tests** start the portal themselves on a random free port (its output goes to
`TestResults/portal.log`), and save screenshots to `TestResults/Screenshots`. The first time, install
the Playwright browser:

```powershell
dotnet build blazwiz.Portal.UITests
pwsh blazwiz.Portal.UITests/bin/Debug/net10.0/playwright.ps1 install chromium
```

To test a portal that is already running (or deployed), set `BLAZWIZ_UITEST_URL`, e.g.
`$env:BLAZWIZ_UITEST_URL = "http://localhost:5090"`. Timeouts are deliberately short (10s per step,
30s per test, see `TestBase.cs` and `.runsettings`).

**Integration tests** tagged `@aws` need these environment variables, otherwise they are skipped:
`BLAZWIZ_INTTEST_AWS_ACCESSKEY`, `BLAZWIZ_INTTEST_AWS_SECRETACCESSKEY`, `BLAZWIZ_INTTEST_AWS_REGION`<!--#if (IncludeNotes)-->
and `BLAZWIZ_INTTEST_AWS_BUCKETNAME`. The sync scenarios simulate two devices syncing through that bucket, each
under its own `blazwiz-inttests/<guid>/` prefix, which is emptied afterwards<!--#endif-->.
<!--#endif-->

## Building out

### Add a page

1. **View model**: `blazwiz.Portal.Application/ViewModels/ThingsViewModel.cs`, deriving from `ViewModelBase`.
   It is registered automatically.
2. **Page**: `blazwiz.Portal/Pages/Things.razor` with `@page "/things"` and
   `@inherits MvvmComponentBase<ThingsViewModel>`. Use `<PageHeader Title="Things" Icon="bi-box" />`
   for a consistent heading and browser tab title.
3. **Navigation**: add a `NavigationItem` to `NavigationMenuService`. It appears in the sidebar and as a
   quick link on the dashboard.
<!--#if (IncludeTests)-->
4. **Tests**: `blazwiz.Portal.Application.UnitTests/ViewModels/ThingsViewModelTests.cs`.
<!--#endif-->
<!--#if (IncludeShowcase)-->

The `Showcase` page contains themed buttons, alerts, forms and tables to copy from. Delete it when you
no longer need it.
<!--#endif-->

<!--#if (IncludeNotes)-->

### Notes

The **Files** page and note editor are a working example of an offline-first feature: markdown notes,
organised into folders, stored in the browser for the signed in user and synced to their own prefix in the
data bucket.

- **Storage**: notes are plain `.md` files and folders are paths (`notes/Work/Ideas.md`), kept in IndexedDB by
  the `notes` sync profile's file store. `NoteService` asks for it with `[FromKeyedServices("notes")] ISyncFileStore`,
  so every change is tracked for sync automatically. Notes from earlier versions are migrated to files on first load.
- **Logic**: `NoteService` handles creating, renaming, moving and deleting (S3 has no rename, so a rename writes the
  new keys and deletes the old ones), using `NoteNameService` to validate names (including characters unsafe in
  S3 keys) and `NoteTreeService` for the folder structure. The welcome note (`Resources/WelcomeNote.md`) is created
  once the first sync shows the user has no notes anywhere, or straight away when sync cannot run.
- **Editor**: the toolbar's formatting (`MarkdownFormatService`) toggles, like a word processor. The
  preview is rendered by [Markdig](https://github.com/xoofx/markdig) with raw HTML disabled and unsafe
  link URLs removed. `Ctrl+S` saves, and leaving with unsaved changes asks what to do with them. If a sync
  changes the open note while it has unsaved edits, the user chooses whether to keep theirs or take the synced one.

### Sync

`blazwiz.Core/Sync` is a reusable engine that keeps local files in step with objects under the user's S3 prefix
(`<identity id>/`), for any feature and any file type. Notes is its first user (see `docs/wip/s3-sync.md`):

```csharp
services.AddBlazwizSync(sync => sync
    .UseIndexedDbEventLog(database: "blazwiz_sync", store: "events")
    .UseEventLogRetention(maxAge: TimeSpan.FromDays(30), maxEventsPerUser: 5000)
    .UseChangeDebounce(TimeSpan.FromSeconds(2))
    .AddProfile("notes", profile => profile
        .UseIndexedDbIndexStore(database: "blazwiz_sync", store: "notes_index")
        .UseIndexedDbFileStore(database: "blazwiz_notes", store: "files")
        .UseRemote("notes/**/*.md", includeEmptyFolders: true)));
```

- **How it decides**: an index records each file's hash and ETag at the last sync, so a pass knows which side
  changed. Uploads and deletes are conditional (`If-Match` / `If-None-Match`), so nothing newer is overwritten; when
  both sides changed, the newest edit wins (`NewestWinsConflictResolver`, using the edit time saved in
  `x-amz-meta-modified-at`).
- **When it runs**: on sign in (a full verify pass), 2 seconds after local changes, when the app regains focus and
  when it comes back online. A Web Lock keeps two tabs from syncing the same profile at once.
- **Connectivity**: offline mode, the browser's network, AWS credentials and an S3 probe are checked in that
  order. Offline mode (the status pill in the bottom right, Settings, the Sync page, or **Start in offline mode**
  under the login page's advanced options) makes no network calls at all; changes keep being recorded and sync
  when it is turned off.
- **Credentials**: `CognitoS3TargetProvider` exchanges the ID token for temporary credentials through the
  identity pool. With the placeholder sign-in there is no cloud sync: everything works locally and the status
  says why ("Offline: no credentials").
- **Sync log**: everything the engine does is logged in IndexedDB and shown on the **Sync** page, with search,
  filters and live updates. After the first sign in the app asks the browser for persistent storage, and the
  Settings Sync card shows whether it was granted.
<!--#endif-->

### Add a service

Put domain logic in `blazwiz.Core` (interface in an `Interfaces` sub folder) and register it in
`AddBlazwizCoreServices`. Portal specific services go in `blazwiz.Portal.Application/Services` and
`AddPortalApplicationServices`.
<!--#if (IncludeTests)-->
Add a matching test file in `blazwiz.Core.UnitTests` or `blazwiz.Portal.Application.UnitTests`.
<!--#endif-->

### Theming

The app uses Bootstrap 5.3 colour modes (`data-bs-theme="light|dark"` on `<html>`). All custom colours
are CSS variables (`--bw-*`) at the top of `wwwroot/css/app.css`. Change `--bw-accent` and
`--bw-accent-2` to re-brand. The saved theme is applied by `wwwroot/js/app.js` before Blazor starts, so
there is no flash of the wrong theme. The user's choice is stored in local storage and defaults to the
operating system preference.

### Configuration

`wwwroot/appsettings.json` holds `AppSettings` (title, description, copyright), `AwsCognito`
(region, user pool, client and identity pool ids) and `AwsS3` (region, data bucket).
`appsettings.Development.json` overrides values when running locally and is not published. When
deploying, `deploy.ps1` generates `appsettings.Production.json` with the stack's ids (git ignored).

## Authentication

`IAuthenticationService` has two implementations, chosen in `AddPortalApplicationServices`:

- **`CognitoAuthenticationService`** when `AwsCognito` has a region, user pool id and app client id (deployed
  builds get these from `appsettings.Production.json`). Sign-in uses SRP, so the password is never sent.
  Invited users with a temporary password are asked to choose a new one. Tokens are kept in local storage
  and refreshed with the refresh token when they are within 5 minutes of expiring, and signing out revokes
  the refresh token.
- **`LocalAuthenticationService`** (placeholder, any credentials) otherwise, e.g. local development and the UI tests.

All Cognito protocol work, including SRP, is done by AWS's
[`Amazon.Extensions.CognitoAuthentication`](https://github.com/aws/aws-sdk-net-extensions-cognito) library,
wrapped by `CognitoUserPoolClient` (behind `ICognitoUserPoolClient`, so the service is unit testable). Two
settings make the AWS SDK work in the browser, see `CognitoUserPoolClient.BrowserHttpClientFactory`: a plain
`HttpClient`, and buffering responses because the SDK reads them synchronously, which WebAssembly does not
allow. Tokens are only decoded (with `Microsoft.IdentityModel.JsonWebTokens`) to show the user's name and email.

Once signed in to a deployed stack, `CognitoAwsCredentialsProvider` (behind `IAwsCredentialsProvider`) exchanges
the ID token for temporary AWS credentials through the identity pool, holds them in memory for the session (fetching
new ones shortly before they expire) and forgets them on sign out. Everything that calls AWS shares them, e.g.
`SecretsManagerUserKeysStore` (behind `IUserKeysStore`), which keeps the user's keys in their own Secrets Manager
secret, `<user pool sub>/keys`.

## Deploying to AWS

Everything lives in `cloudformation/`. You need the AWS CLI v2, the .NET 10 SDK and credentials for the
target account (an AWS CLI profile, or the usual `AWS_*` environment variables). Rather than full admin
credentials, you can use a dedicated IAM user with the minimum policy in
[`cloudformation/README.md`](cloudformation/README.md#minimum-iam-policy-for-running-the-scripts).

| File | Purpose |
|---|---|
| `stack.yaml` | The CloudFormation template (below). |
| `deploy.ps1` | Deploys / updates the stack, publishes and uploads the portal, creates a test user. Safe to re-run. |
| `test-auth.ps1` | Verifies a deployment end to end: portal, sign-in, identity pool credentials, per-user S3 access. |
| `stack-policy.json` | Stops stack updates from replacing or removing the user pool, identity pool and data bucket. |
| `remove.ps1` | Empties the buckets and deletes the stack. |

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

All scripts take `-AppName` (default `blazwiz`), `-Environment` (default `dev`), `-Region` and
`-AwsProfile`. The stack is named `<AppName>-<Environment>`, so several environments can share an
account. `deploy.ps1` and `remove.ps1` show the account, caller and region and ask for confirmation
before changing anything (`-Force` skips this).

`deploy.ps1` lists the infrastructure changes before applying them and asks before any resource is
replaced (recreated with a new id). Replacing or removing the user pool, identity pool or data bucket
would lose users or their data, so it is refused outright (and blocked by `stack-policy.json` for
deployments from anywhere else) unless you pass `-AllowStatefulReplacement`. Some user pool settings,
such as `UsernameAttributes`, can only be set on creation, so settle them before real users sign up.

**What gets created** (stack `blazwiz-dev`):

- **Portal hosting**: a private S3 bucket (`blazwiz-dev-portal-<account>`) served only through
  CloudFront via origin access control. HTTPS only, security headers, missing paths fall back to
  `index.html` so deep links work. Optional custom domain via `-CustomDomainName` / `-CertificateArn`
  (ACM certificate in us-east-1).
- **Sign-in**: a Cognito user pool (email as username, invite only, no self sign-up, 12+ character
  passwords) with a public app client (SRP + refresh tokens).
- **AWS access**: a Cognito identity pool and IAM role giving signed-in users temporary credentials that
  can only read / write / list `s3://blazwiz-dev-data-<account>/<their identity id>/*`.

Cost is typically pennies for a test environment: S3, CloudFront and Cognito all have free tiers
(Cognito charges per monthly active user beyond them).

`deploy.ps1` uploads the portal with `Cache-Control: no-cache` for entry files (`index.html`, service
worker, manifest, settings) and a one day cache for everything else, then invalidates CloudFront, so new
deployments show up straight away. The test user is created with a permanent password (no forced reset).

> The app client also allows `USER_PASSWORD_AUTH`, only so `test-auth.ps1` can sign in from the command
> line. The portal itself uses SRP. Remove it from `stack.yaml` if you do not want it.

## Roadmap

<!--#if (IncludeNotes)-->
- **More sync**: timer based sync, signing in with no network, large files (streaming and multipart upload), an
  OPFS file store, and telling other open tabs about synced changes (see `docs/wip/s3-sync.md`).
<!--#else-->
- **Per-user data**: the session's AWS credentials can already reach the user's prefix in the data bucket
  (see [Deploying to AWS](#deploying-to-aws)). The notes app (`--IncludeNotes`) uses it to sync notes.
<!--#endif-->
<!--#if (false)-->

## Creating a new app from this template

This repository is a working app and a `dotnet new` template at the same time. The template settings are
in `.template.config/`, and everything else is the app itself, so you keep developing, testing and
deploying it as normal.

### Install and use

```powershell
# From this folder (while working on the template)
dotnet new install .

# Or from the package built by templatepack/ (see below)
dotnet new install ./templatepack/bin/Release/Devoctomy.Blazwiz.Templates.1.0.0.nupkg

dotnet new blazwiz -n AcmePortal --Title "Acme Portal"
```

| Option | Default | Effect |
|---|---|---|
| `-n`, `--name` | output folder name | App name. Keeps your casing for projects, namespaces and the solution (`AcmePortal.Portal`). |
| `--Title` | the name | Shown in the browser tab, login page, PWA manifest and copyright. Avoid quotes. |
| `--Description` | `Sign in to continue.` | Shown under the title on the login page. |
| `--IncludeTests` | `true` | Include the four test projects. |
| `--IncludeShowcase` | `true` | Include the Showcase page of themed building blocks. |
| `--IncludeNotes` | `true` | Include the Files page and markdown note editor, and the S3 sync engine, Sync page and offline mode. |

The name is used in other forms where the context needs them: lower case letters and numbers for AWS
(stack, bucket and role names, e.g. `acmeportal-dev`) and browser storage keys, `ACMEPORTAL_` for test
environment variables, and `AcmePortal` in identifiers such as `AddAcmePortalCoreServices`. Characters
that are not valid in C# names become `_` (`my-app` gives `my_app.Portal`).

### Package and test the template

```powershell
./templatepack/test-template.ps1              # repo tests, then create, build and test sample apps
dotnet pack templatepack -c Release           # produce the .nupkg
dotnet new uninstall Devoctomy.Blazwiz.Templates
```

`test-template.ps1` checks the repository itself still builds and passes its tests, then installs the
template into an isolated location, creates apps with awkward names and options, and checks each one
builds, passes its tests and has no `blazwiz` left in it.

### Keeping the repository a valid template

- Write the app name as `blazwiz`. The only other forms allowed are `Blazwiz` in identifiers and
  `BLAZWIZ_` in environment variable names.
- Where the title is displayed (rather than the name), add the text just before it to the `appTitle`
  symbol's `onlyIf` list in `.template.config/template.json`.
- Optional content uses comment markers (`//#if (IncludeShowcase)`, `<!--#if (IncludeTests)-->`), so the
  repository still builds and runs with everything included.
<!--#endif-->
