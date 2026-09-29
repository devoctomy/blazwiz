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

`wwwroot/appsettings.json` holds `AppSettings` (title, description, copyright) and `AwsCognito`
(region, user pool, client and identity pool ids). `appsettings.Development.json` overrides values when
running locally and is not published.

## Roadmap

1. **Cognito authentication**: implement `IAuthenticationService` against Cognito (SRP login, new
   password challenge, token refresh) and swap it for `LocalAuthenticationService` in
   `AddPortalApplicationServices`. Exchange the ID token for temporary AWS credentials through the
   Cognito identity pool, for optional direct access to AWS resources.
2. **One command AWS deployment**, planned as:
   - `Cloudformation/blazwiz-aws.yaml`: private S3 bucket + CloudFront (origin access control,
     `404 → /index.html` for client side routing, HTTPS), Cognito user pool + app client (no secret),
     identity pool with an authenticated IAM role scoped per user.
   - `Cloudformation/deploy.ps1`: uses a local AWS CLI profile to run `aws cloudformation deploy`, read
     the stack outputs, write them into `appsettings.json`, `dotnet publish` the portal, `aws s3 sync`
     it, invalidate CloudFront, and create a test user (`admin-create-user` + `admin-set-user-password`).
3. **`dotnet new` template**: add `.template.config/template.json` with `sourceName: "blazwiz"` so every
   project, namespace, storage key and title is renamed for the new app.
