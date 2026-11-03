# blazwiz

A framework for **Blazor WebAssembly PWAs** that sign in with **AWS Cognito**, sync files with S3 (end
to end encrypted) and work offline. It ships as NuGet packages (`Devoctomy.Blazwiz.*`) and a `dotnet new` template
that creates an app built on them, so apps take framework upgrades by changing one version number.

> **Status:** in development, unreleased (0.x). The restructure into packages is planned and tracked in
> [docs/reviews/2026-11-03-upgradeability-review.md](docs/reviews/2026-11-03-upgradeability-review.md).

## Repository layout

| Folder | What it holds |
|---|---|
| `src/` | The packages, one project each (below). |
| `tests/` | One unit test project per package, plus the sync integration tests. |
| `template/` | The template app (`MyApp`), with its own tests and the AWS stack (`template/cloudformation`). In this repository it builds against the packages' source; generated apps use the packages. |
| `templatepack/` | Packs the template, and `test-template.ps1`, which checks packages and template together. |
| `docs/` | The plan, versioning policy and diagrams. |

| Package | Depends on | What it is |
|---|---|---|
| `Devoctomy.Blazwiz.Core` | | App identity and browser storage names, offline mode, overridable and translatable text, coded errors. |
| `Devoctomy.Blazwiz.Cryptography` | Core | AES-GCM, Argon2id, HKDF, key wrapping, keys derived from the WebAuthn PRF output (BouncyCastle). |
| `Devoctomy.Blazwiz.Encryption` | Cryptography | End to end encryption keys: master and file keys, the key ring and the encrypted file format. |
| `Devoctomy.Blazwiz.Browser` | Cryptography | Local storage, theme, navigation and WebAuthn passkeys. |
| `Devoctomy.Blazwiz.Auth` | Browser | The sign-in contract, sessions and their lifecycle, offline sign in, remembered passwords. |
| `Devoctomy.Blazwiz.Auth.Cognito` | Auth | Cognito sign in (SRP) and the session's AWS credentials. |
| `Devoctomy.Blazwiz.Sync` | Encryption, Browser | The offline-first sync engine: IndexedDB, OPFS or a local folder, synced with S3. |
| `Devoctomy.Blazwiz.UI` | Auth | The app shell, its pages, theme and extension points. |
| `Devoctomy.Blazwiz.UI.Sync` | UI, Auth.Cognito, Sync | The sync module's UI and wiring. |
| `Devoctomy.Blazwiz.Testing` | UI | Test helpers for apps. |

Versions are in lockstep (one version for every package and the template), see [docs/versioning.md](docs/versioning.md).

## Build and test

Prerequisites: .NET 10 SDK. The first time, install the Playwright browser:

```powershell
dotnet build template/MyApp.Portal.UITests
pwsh template/MyApp.Portal.UITests/bin/Debug/net10.0/playwright.ps1 install chromium
```

```powershell
dotnet build blazwiz.slnx
dotnet test blazwiz.slnx
```

Each package has its own unit test project, `tests/Devoctomy.Blazwiz.<Package>.UnitTests`, referencing only that
package (shared fakes are in `tests/Devoctomy.Blazwiz.TestSupport`). `tests/Devoctomy.Blazwiz.Sync.IntTests` holds
the sync integration scenarios, and the template app has its own unit and Playwright UI tests.

Integration scenarios tagged `@aws` are skipped unless `BLAZWIZ_INTTEST_AWS_ACCESSKEY`,
`BLAZWIZ_INTTEST_AWS_SECRETACCESSKEY`, `BLAZWIZ_INTTEST_AWS_REGION` and `BLAZWIZ_INTTEST_AWS_BUCKETNAME` are set.

To run the template app: `dotnet run --project template/MyApp.Portal`.

## The template

```powershell
./templatepack/test-template.ps1    # packs the packages and template, creates apps with different options, builds and tests them
dotnet pack templatepack -c Release # the template package
```

| Option | Default | Effect |
|---|---|---|
| `-n`, `--name` | output folder name | App name. Keeps your casing for projects, namespaces and the solution (`AcmePortal.Portal`). |
| `--Title` | the name | Shown in the browser tab, login page, PWA manifest and copyright. Avoid quotes. |
| `--Description` | `Sign in to continue.` | Shown under the title on the login page. |
| `--IncludeTests` | `true` | Include the unit and UI test projects. |
| `--IncludeShowcase` | `true` | Include the Showcase page of themed building blocks. |
| `--Sync` | `true` | Include the sync module (`Devoctomy.Blazwiz.UI.Sync`). |
| `--Notes` | `true` | Include the notes example (Files page and markdown editor). Needs `--Sync`. |

There is no option to sync without end to end encryption (yet). Whether an app may do that will be the app
developer's choice, never its users', and it is not supported until it can be done with clear warnings: without
encryption, files sit in S3 as plain text that anyone who can read the bucket (the AWS account's operators, leaked
credentials, backups and old versions) can read, with access control as the only protection. See
`docs/backlog/sync-without-encryption.md`.

In the template the app is called `MyApp`: `MyApp` in projects, namespaces and identifiers, `myapp` for AWS names and
browser storage keys, `MYAPP_` for environment variables. Optional content uses comment markers
(`//#if (IncludeSync)`, `<!--#if (IncludeNotes)-->`, `@*#if (IncludeSync)*@`), or `#if (IncludeNotes)` where both
branches are code; `template/Directory.Build.targets` (repository only) turns every option on, so the repository
builds with everything included.

## Contributing

See [AGENTS.md](AGENTS.md) for the rules this repository follows.
