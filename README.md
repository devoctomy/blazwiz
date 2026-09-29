# blazwiz

Boilerplate for a **Blazor WebAssembly static PWA** that authenticates with **AWS Cognito** and can
optionally use AWS resources directly from the browser. It is intended to become a `dotnet new`
template, so new apps of this shape can be created in seconds and built out from there.

> **Status: V1.** The app runs locally with a placeholder login (any username / password).
> Cognito and AWS deployment are the next steps (see [Roadmap](#roadmap)).

## Solution structure

| Project | Purpose |
|---|---|
| `blazwiz.Core` | .NET 10 class library for all backend / domain logic. No UI dependencies. |
| `blazwiz.Core.UnitTests` | xUnit + Moq. Mirrors the `Core` folder structure, one test file per class. |
| `blazwiz.Core.IntTests` | Reqnroll (Gherkin) integration tests. Scenarios tagged `@aws` are skipped unless AWS credentials are set. |
| `blazwiz.Portal` | The Blazor WASM PWA: pages, layouts, components, `wwwroot`. Views only. |
| `blazwiz.Portal.Application` | Unit testable portal logic: view models (Blazing.Mvvm), services, helpers. |
| `blazwiz.Portal.Application.UnitTests` | xUnit + Moq. Mirrors the `Portal.Application` folder structure. |
| `blazwiz.Portal.Models` | DTOs / settings used by the portal. No logic. |
| `blazwiz.Portal.UITests` | Playwright (MSTest) end-to-end tests. Starts the portal automatically. |

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
`BLAZWIZ_INTTEST_AWS_ACCESSKEY`, `BLAZWIZ_INTTEST_AWS_SECRETACCESSKEY`, `BLAZWIZ_INTTEST_AWS_REGION`.

## Building out

### Add a page

1. **View model**: `blazwiz.Portal.Application/ViewModels/ThingsViewModel.cs`, deriving from `ViewModelBase`.
   It is registered automatically.
2. **Page**: `blazwiz.Portal/Pages/Things.razor` with `@page "/things"` and
   `@inherits MvvmComponentBase<ThingsViewModel>`. Use `<PageHeader Title="Things" Icon="bi-box" />`
   for a consistent heading and browser tab title.
3. **Navigation**: add a `NavigationItem` to `NavigationMenuService`. It appears in the sidebar and as a
   quick link on the dashboard.
4. **Tests**: `blazwiz.Portal.Application.UnitTests/ViewModels/ThingsViewModelTests.cs`.

The `Showcase` page contains themed buttons, alerts, forms and tables to copy from. Delete it when you
no longer need it.

### Add a service

Put domain logic in `blazwiz.Core` (interface in an `Interfaces` sub folder), register it in
`AddBlazwizCoreServices`, and add a matching test file in `blazwiz.Core.UnitTests`. Portal specific
services go in `blazwiz.Portal.Application/Services` and `AddPortalApplicationServices`.

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

## Deploying to AWS

Everything lives in `Cloudformation/`. You need the AWS CLI v2, the .NET 10 SDK and credentials for the
target account (an AWS CLI profile, or the usual `AWS_*` environment variables).

| File | Purpose |
|---|---|
| `blazwiz-aws.yaml` | The CloudFormation template (below). |
| `deploy.ps1` | Deploys / updates the stack, publishes and uploads the portal, creates a test user. Safe to re-run. |
| `test-auth.ps1` | Verifies a deployment end to end: portal, sign-in, identity pool credentials, per-user S3 access. |
| `remove.ps1` | Empties the buckets and deletes the stack. |

```powershell
# Deploy (asks for confirmation, then for the test user's password)
./Cloudformation/deploy.ps1 -Region eu-west-2 -TestUserEmail me@example.com

# Check it all works
./Cloudformation/test-auth.ps1 -Region eu-west-2 -Email me@example.com

# Re-publish just the portal after code changes
./Cloudformation/deploy.ps1 -Region eu-west-2 -SkipInfrastructure

# Tear it down
./Cloudformation/remove.ps1 -Region eu-west-2
```

All scripts take `-AppName` (default `blazwiz`), `-Environment` (default `dev`), `-Region` and
`-AwsProfile`. The stack is named `<AppName>-<Environment>`, so several environments can share an
account. `deploy.ps1` and `remove.ps1` show the account, caller and region and ask for confirmation
before changing anything (`-Force` skips this).

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
> line. The portal will use SRP. Remove it from the template if you do not want it.

## Roadmap

1. **Cognito authentication**: implement `IAuthenticationService` against Cognito (SRP login, new
   password challenge, token refresh) and swap it for `LocalAuthenticationService` in
   `AddPortalApplicationServices`. Exchange the ID token for temporary AWS credentials through the
   Cognito identity pool, for optional direct access to AWS resources. The infrastructure is already
   in place (see [Deploying to AWS](#deploying-to-aws)).
2. **`dotnet new` template**: add `.template.config/template.json` with `sourceName: "blazwiz"` so every
   project, namespace, storage key and title is renamed for the new app.
