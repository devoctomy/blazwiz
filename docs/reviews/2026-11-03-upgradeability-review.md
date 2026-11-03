# Upgradeability review: making blazwiz a framework you upgrade, not a template you fork

**Date:** 2026-11-03
**Goal:** a user creates a site from the template, customises it, then takes framework updates by bumping a package
version. Minor versions should just work.
**How this was done:** by reading the source on `main` (74c18c8). Nothing was built or run for this review.

---

## 1. The short version

- **About 80% of a generated app is framework code, and the user owns all of it.** `dotnet new blazwiz` copies the
  whole repository and renames every namespace (`blazwiz.Core` becomes `AcmePortal.Core`). Of roughly 27k lines in
  a new app, about 22k are framework (sync, crypto, auth, shell, settings). Only the notes feature (about 4k lines)
  and the dashboard and showcase pages are really "the app". With no package boundary, the only way to upgrade is to
  diff and merge by hand.
- **The code is in good shape for this move.** Logic already sits in injectable services and view models
  (Blazing.Mvvm), Razor files are thin, and Core has no dependency on the portal. Most of the work is drawing package
  boundaries and adding a few extension points. Little needs rewriting.
- **The main blocker is that framework UI is wired in by hand.** The shell, login page, settings page and navigation
  include sync and encryption pieces directly, using `//#if (IncludeNotes)` blocks. Packaged modules cannot work
  that way. They need to plug themselves in.
- **Password changing and forgot-password do not exist yet.** They should be built straight into the new auth
  packages rather than into the template.

Section 6 has the plan in five phases. Section 7 records the decisions made.

---

## 2. Where it stands today

| Concern | Today | Upgradeable? |
|---|---|---|
| Framework code delivery | Source copied by `dotnet new`, then renamed | No |
| Namespaces | Renamed to the app's name (`AcmePortal.Core.Sync...`) | No: the app's copy cannot be matched to the original |
| Optional features | `#if` blocks inside framework classes (`MainLayoutViewModel`, `LoginViewModel`, `Settings.razor`, `NavigationMenuService`, DI setup) | No: they are decided once, when the app is created |
| Shell, login, profile, settings UI | In `blazwiz.Portal`, the user's own project | No |
| Theme CSS (1,081 lines in `app.css`) | In the user's `wwwroot` | No: framework styles and the user's own are mixed together |
| JS interop | Global scripts in `index.html` (`portalInterop`, `webAuthnWrapper`, `notesInterop`) plus ES modules served from `_content/blazwiz.Core` | Partly: the Core modules already follow the right pattern |
| Browser storage keys | Literal strings such as `"blazwiz_saved_users"` in about 12 places, renamed per app, and duplicated in `app.js` | No: they need to become configuration (see U7) |
| Infrastructure | `cloudformation/` copied into the app | No |
| Tests | Framework tests copied into the app | Not needed there: the framework's own tests belong in this repo |

### What is already right

- Services are interface plus implementation, registered through `Add*` extensions (`AddBlazwizSync`,
  `AddBlazwizE2eEncryption`, `AddBlazwizPrfKeyDerivation`). Those builders already behave like a package API.
- Core is a Razor class library that serves its own JS modules from `_content/blazwiz.Core/js/*.js` and loads them
  with `import`. Every packaged module should work this way.
- Pages and components are thin `MvvmComponentBase<TViewModel>` wrappers. The view models are the logic, so a user
  who wants a different look can write their own Razor file against the packaged view model. This "headless view
  model plus default view" split is what makes "the portal is purely UI" achievable.
- `AppIdentity.Id` is already a per-app GUID, used to salt the WebAuthn-derived keys.

---

## 3. Findings

Each finding has an ID so it can be referenced from the plan. **Blocker** means upgrades cannot work until it is fixed.
**Needed** means upgrades would be fragile without it. **Tidy** is worth doing along the way.

### Blockers

**U1. Framework code ships as source and is renamed.**
`.template.config/template.json` replaces `blazwiz.` and `blazwiz` in every file, including `blazwiz.Core` and
`blazwiz.Portal.Application`.
*Fix:* move framework code into NuGet packages with fixed IDs and namespaces (`Devoctomy.Blazwiz.*`). The template
should rename only the app's own projects.

**U2. Optional framework features are compiled in with template conditionals.**
`MainLayoutViewModel` takes 9 sync and encryption dependencies inside `//#if (IncludeNotes)`. `LoginViewModel`,
`Settings.razor`, `MainLayout.razor`, `NavigationMenuService` and `AddPortalApplicationServices` do the same.
*Fix:* make modules add themselves through extension points (U3 to U6). A module that is referenced and registered
shows up. A module that is not referenced does not. No `#if` blocks.

**U3. The shell is hard-wired.**
`MainLayout.razor` renders `OfflineBanner`, `E2eBanner`, the folder-access banner, the sync pill and popover, and
the storage-migration overlay directly.
*Fix:* a packaged `BlazwizShell` layout with named regions (banners, top bar actions, floating actions, overlays,
sidebar footer). Modules register components into regions, for example `shell.AddBanner<E2eBanner>(order: 20)`. The
shell renders each one with `DynamicComponent`. The app can add its own components the same way.

**U4. Startup and sign-out logic for modules lives inside the shell view model.**
After sign-in, `MainLayoutViewModel` unlocks E2E, completes storage migrations, starts the storage channel and starts
the sync scheduler. `LoginViewModel` handles "start with sync off" and persistent storage.
*Fix:* an `ISessionLifecycleHandler` interface (`OnSignedInAsync`, `OnResumedAsync`, `OnSigningOutAsync`,
`OnSessionEndedAsync`, with an order) that modules implement and the shell calls. The shell view model goes back to
auth, navigation and layout state only.

**U5. Settings, navigation and login options are fixed lists.**
`NavigationMenuService` is a static list with `#if` blocks. `Settings.razor` has the sync, storage, E2E and Argon2id
sections inline. The login page's "Advanced options" contains a sync-only option.
*Fix:* registries that modules and the app both add to:
- `services.AddBlazwizNavigation(nav => nav.Add(...))`, with order and grouping.
- `settings.AddSection<SyncSettingsSection>("Sync", order: 30)`.
- `login.AddOption<StartSyncOffOption>()`.

The packaged Settings page renders the registered sections. The app adds its own sections the same way.

**U6. The router cannot see pages in packages.**
`App.razor` uses `Router AppAssembly=...` only.
*Fix:* a packaged `BlazwizRouter` that adds the package assemblies to `AdditionalAssemblies`, so pages such as
`/login`, `/profile`, `/settings` and `/sync` come from their packages.

Apps replace or remove packaged pages through DI configuration at startup (decision D3). The app cannot simply
declare its own `@page "/login"`, because the router finds routes by scanning assemblies and fails when two of them
claim the same path. Instead:
- Each packaged route is a thin host page (`@page "/login"`) that renders, with `DynamicComponent`, the component
  type configured for it. By default that is the package's own view.
- The app chooses a different component in `Program.cs`, for example `ui.Pages(p => p.Login.Use<MyLogin>())`.
  `MyLogin` can inherit `MvvmComponentBase<LoginViewModel>`, so it reuses the packaged logic and changes only the
  markup.
- `p.Sync.Disable()` removes a page: the route shows Not Found and its navigation entry is not added.

### Needed

**U7. App identity and storage keys are literals.**
`AppIdentity` is a static class with consts. Storage keys are string literals: `blazwiz_session`,
`blazwiz_cognito_session`, `blazwiz_saved_users`, `blazwiz_user_settings`, `blazwiz_e2e_keys_`, `blazwiz_sync`,
`blazwiz_notes`, and in `SyncDefaults` the `blazwiz-sync-*` lock and channel names. `app.js` repeats
`blazwiz_user_settings`.
*Fix:* `BlazwizAppOptions { Name, Id, StorageKeyPrefix }` set once in `Program.cs`. Every package builds its keys
from the prefix, and the app's identity reaches the packages through options instead of a template rename. Key names
can change freely in this restructure: nothing has been released, so there is no stored data to preserve (D8).

**U8. Global scripts and CSS have to be added to `index.html` by hand.**
Today `index.html` loads `webauthn-bridge.js`, `simplewebauthn`, `notes.js`, `app.css`, `notes.css` and `sync.css`.
A package update that adds a script would need the user to edit `index.html`.
*Fix:*
- Convert globals to ES modules loaded on demand, as Core already does.
- Use Blazor JS initializers (`<Package>.lib.module.js`), which load automatically, for anything that must run at start.
- Each UI package ships its CSS under `_content/...` and the template references one bundle.

The theme pre-paint script (which stops a flash of the wrong theme) must still run before Blazor starts. Keep a
small inline snippet in `index.html` that reads the prefixed key, and document it as the only framework line in that file.

**U9. Framework styles and app styles are mixed in one file.**
`app.css` holds the theme tokens (`--bw-*`), Bootstrap overrides, the shell, modal, sidebar, dashboard, settings and
profile styles.
*Fix:* split it.
- Framework: the token definitions, Bootstrap overrides, and the shell, auth, modal and settings styles move into
  `Devoctomy.Blazwiz.UI`.
- App: dashboard and page styles, plus a token override file (`theme.css`) for re-skinning.

Tokens become the supported theming API: changing them is a breaking change, so they follow SemVer.

**U10. Each package needs its own configuration.**
`AddPortalSettings` binds `AppSettings`, `AwsCognito` and `AwsS3` into plain singletons.
*Fix:* `IOptions<T>` with `ValidateOnStart`. Each package binds its own section (`Blazwiz:Auth:Cognito`,
`Blazwiz:Sync:S3`, ...). The app's `AppSettings` keeps only branding.

**U11. View models are registered from one assembly.**
`RegisterViewModelsFromAssemblyContaining<MainLayoutViewModel>()` only scans the application assembly.
*Fix:* each package's `AddBlazwiz*()` registers its own view models. The app keeps scanning its own assembly.

**U12. Nothing protects the public API.**
Nothing stops a minor release from breaking a user.
*Fix:*
- `Microsoft.CodeAnalysis.PublicApiAnalyzers` (`PublicAPI.Shipped.txt` / `Unshipped.txt`) on every package.
- `EnablePackageValidation` with `PackageValidationBaselineVersion`, so `dotnet pack` fails on a binary-breaking change.
- A written SemVer policy covering what counts as public: C# API, component parameters, CSS tokens, route paths,
  config sections, storage formats, and text resource keys and their placeholders (U16).

**U13. Data formats need a versioned migration path, from 1.0 onwards.**
After release, upgrades will change stored data (IndexedDB schemas, `keys.json`, session shape).
*Fix:* a small `IDataMigration` contract with a schema version stored per app and per user. Each package ships its
own migrations, run in order at sign-in through U4. A package must never require the app to migrate data by hand.

This only applies to released versions. Before 1.0, formats change freely and nothing migrates data from earlier
development builds (D8). The existing compatibility code for such builds is removed instead (T10).

**U14. Password management is missing.**
There is no change-password, forgot-password or confirm-code flow. `ICognitoUserPoolClient` has only sign-in,
new-password challenge, refresh and revoke.
*Fix:* add `ChangePasswordAsync`, `ForgotPasswordAsync` and `ConfirmForgotPasswordAsync` to the auth abstraction,
implemented in the Cognito package with `Amazon.Extensions.CognitoAuthentication` (`CognitoUser.ChangePasswordAsync`,
`ForgotPasswordAsync`, `ConfirmForgotPasswordAsync`). Ship `ChangePasswordForm` and `ForgotPasswordForm` components
with their view models, and wire them into the Profile page and login page.
Related: `docs/backlog/v1-feature-ideas.md` "Self-service password flows". MFA (TOTP) can follow the same pattern
later.

**U15. The AWS infrastructure is copied as well.**
This is about the AWS resources, not NuGet. `cloudformation/stack.yaml` (CloudFront, the S3 buckets, the Cognito
user pool and identity pool, IAM roles) and the deploy scripts are copied into each app. Packages will rely on that
infrastructure: for example, a new sync feature may need an extra IAM permission on the data bucket. Every app's copy
of `stack.yaml` would then have to be patched by hand, or the package update breaks at runtime.
*Fix (last phase, decision D4):* release the base stack and deploy scripts with each framework version, for example
as a `dotnet tool` (`blazwiz deploy`) carrying the matching `stack.yaml`. Each app keeps a small extension stack of
its own for its own resources, built from the base stack's outputs. Until then, release notes must list any change
to `stack.yaml`.

**U16. Text shown to users is hard-coded English and cannot be changed or translated.**
Once the UI is in packages, an app cannot reword it or translate it without forking the package. In scope is any text
that can reach the screen. Developer-only text, such as argument checks and log messages, stays in English.

Text reaches the screen in five ways today:
1. **Razor markup.** Labels, help text, dialogs and banners in every page and component.
2. **View models and presenters.** About 97 English literals in `Portal.Application`, for example the E2E statuses
   and step labels in `E2eSettingsViewModel`, and the sync pill text in `SyncStatusPresenter`.
3. **Exception messages shown as they are.** About 10 view models set `Error = ex.Message`, so text from Core or the
   AWS SDK goes straight to the screen. Examples: `E2eBannerViewModel.cs:239`, `E2eSettingsViewModel.cs:391`,
   `OfflineModeSwitchViewModel.cs:82`, and `SyncStorageMigrationService.FolderNotEmptyMessage` in Core.
4. **Result types carrying sentences.** `LoginResult.Error` and `SyncStatus.LastError` (shown under "Last error" in
   Settings) are English strings built by the services.
5. **JS modules.** For example `folder.js` ("No folder has been chosen."), `filetree.js` ("This browser cannot write
   files here.") and `webauthn-bridge.js` ("Operation was cancelled or timed out").

Much of the sync and E2E text is also written for the notes example: "Your notes are end to end encrypted and
locked", "Move your notes?", "Encrypt your notes" and so on, in about 20 places. The logic underneath is already
generic. For example, `SettingsViewModel` works over `syncService.Profiles`, not the notes profile.

*Fix:* see "Text and localisation" in section 4. In short:
- Every package ships its UI text as resources, with neutral English defaults.
- Non-UI packages return error codes, not sentences.
- The app can override any key, and add any language, in one place.

The template's notes example overrides the sync and E2E keys it needs, so a generated app reads the same as today.

### Tidy

- **T1.** `template.json` excludes files that no longer exist: `LocalStorageOfflineModeStore.cs` and its test.
- **T2.** `blazwiz.sln` and `blazwiz.slnx` both exist, and the template ships both. Keep `.slnx`.
- **T3.** The dashboard's "Connect Cognito" card tells users to swap `LocalAuthenticationService` by hand. Cognito is
  now chosen automatically from configuration (`CognitoAuthenticationService.IsConfigured`).
- **T4.** `GreetingService` in Core is sample code inside the framework. Move it to the template app or remove it.
- **T5.** `ISystemClock` and `TimeProvider` are both used. Pick `TimeProvider` before the API is frozen by U12.
- **T6.** Core references `AWSSDK.S3`, `BouncyCastle` and globbing for everyone. The package split (section 4) fixes
  this, so an app without sync does not pull in the S3 SDK.
- **T7.** `SyncStatusPresenter` and `SyncEventPresenter` in Application return Bootstrap icon class names. That is
  fine once they live in a UI package, but they should not end up in a non-UI package.
- **T8.** The About section shows only the app's version. It should also show the framework version, which users
  will want when reporting problems after an upgrade.
- **T9.** Core's doc comments use `notes/**/*.md` and `notes/Work/Ideas.md` as examples. Switch them to neutral
  examples (`documents/**/*.json`) when the code moves, so nothing in the packages mentions notes.
- **T10.** Remove the compatibility code for earlier development builds:
  - `NoteMigrationService`, `INoteMigrationService` and `LegacyNoteContent`, which move notes out of the old
    `entries` and `content` stores.
  - `LocalStorageSyncEnabledStore.LegacyStorageKey`, the old `blazwiz_offline_mode` key.
  - Their tests.

  Remove any similar code found while moving files into packages. `SyncStorageMigrationService` is not compatibility
  code, because it is the user-facing move between storage locations, so it stays.

---

## 4. Target structure

### Packages

All packages are versioned together, so the app has one version number to bump (D5). Names follow D1.

| Package | Kind | Contains (moved from) | Depends on |
|---|---|---|---|
| `Devoctomy.Blazwiz.Core` | class lib | Options (`BlazwizAppOptions`), clock, offline mode, storage key builder, data migration contract, session lifecycle contract, text infrastructure (override-aware localiser factory, plural rules, error codes) (Core: `Time`, `Offline`) | — |
| `Devoctomy.Blazwiz.Cryptography` | class lib | AEAD, Argon2id, HKDF, key wrap, PRF key derivation (Core: `Cryptography`) | Core, BouncyCastle |
| `Devoctomy.Blazwiz.Browser` | RCL | Local storage, theme interop, WebAuthn bridge plus simplewebauthn, network status, web locks, broadcast channel, storage durability, app-update banner. All ES modules. (Portal `Services/*`, `wwwroot/js/*`; Core `Connectivity`, `Storage`) | Core |
| `Devoctomy.Blazwiz.Auth` | class lib | `IAuthenticationService`, sessions, saved users, remember-me, passkey quick login, offline sign-in, placeholder (local) provider, password-change contracts | Core, Cryptography, Browser |
| `Devoctomy.Blazwiz.Auth.Cognito` | class lib | User pool and identity pool clients, `CognitoAuthenticationService`, AWS credentials provider, `BrowserHttpClientFactory`, change and forgot password | Auth, AWS SDK |
| `Devoctomy.Blazwiz.Sync` | RCL | Engine, planner, scheduler, IndexedDB, OPFS and folder stores, JS modules (Core: `Sync` minus S3) | Core, Browser |
| `Devoctomy.Blazwiz.Sync.S3` | class lib | S3 remote store, reachability probe, Cognito S3 target provider | Sync, Auth.Cognito, AWSSDK.S3 |
| `Devoctomy.Blazwiz.Encryption` | class lib | E2E key manager, key ring, file format, sync encryption, Secrets Manager key store (Core: `Encryption`, `Sync/Encryption`) | Sync, Cryptography |
| `Devoctomy.Blazwiz.UI` | RCL | `BlazwizShell`, `BlazwizRouter`, theme CSS, `ModalDialog`, `PageHeader`, `SessionTimer`, `OfflineBanner`, Login, Profile and Settings pages, `ChangePasswordForm`, the registries (U3, U5), and their view models | Auth, Browser |
| `Devoctomy.Blazwiz.UI.Sync` | RCL | Sync page, sync pill and popover, folder banner, storage settings section, E2E banner and settings, Argon2id panel, and their view models | UI, Sync, Encryption |
| `Devoctomy.Blazwiz.Testing` | class lib | Playwright `TestBase`, fakes (`FakeSyncRemoteStore`, key stores), helpers for app tests | — |
| `Devoctomy.Blazwiz.Templates` | template | A small app that references the packages | — |

Two more splits could be made later if they prove useful: `Encryption.Aws` (Secrets Manager key store) and
`Auth.Abstractions`.

### Text and localisation

This fixes U16. It is built on the standard .NET pieces (`IStringLocalizer`, `.resx` resources, satellite
assemblies), so there is no custom resource format.

**1. Each package owns its text.**
Every package with text that users see ships a `.resx` file with neutral English defaults. Components and view models
read text only through `IStringLocalizer<TPackageText>`, so no visible text is left as a literal in Razor or C#.
Keys are namespaced and stable, for example `Sync.E2eBanner.Locked` and `Auth.Login.ServiceUnreachable`. A
generated document lists every key, its default text and its placeholders.

**2. The app can override any key.**
Core registers a localiser factory that looks for text in this order:
1. The app's override for the current culture.
2. The app's neutral override.
3. The package's text for the current culture.
4. The package's English default.

The app supplies overrides as its own `.resx` (`text.AddOverrides<AppText>()`) or, for a few keys, in code
(`text.Override("Sync.E2eBanner.Locked", "…")`). Rewording and translation use the same mechanism: a translation is
just an override file for a culture. Translations could also ship as packages later
(`Devoctomy.Blazwiz.Text.fr`).

**3. Non-UI packages return codes, not sentences.**
Auth, Sync and Encryption stop building text for users:
- `LoginResult.Error` becomes a `LoginError` code.
- `SyncStatus.LastError` becomes an error code plus arguments.
- Exceptions that can reach the screen derive from a `BlazwizException` with a `Code` and `Arguments`. For example,
  `FolderNotEmptyMessage` becomes code `Sync.Storage.FolderNotEmpty` with the file count as an argument.
- JS modules return error codes, not sentences.

The UI turns codes into text through the localiser. `ex.Message` is never shown: an unknown exception shows a generic
"Something went wrong" message, and the details are logged.

**4. Overrides are checked.**
An override with an unknown key, or with placeholders that do not match the package's text, is reported at startup
in Debug builds. `Devoctomy.Blazwiz.Testing` provides `TextOverrides.AssertValid()`, so an app's unit tests fail
instead. This is what makes overrides safe across upgrades: if a package renames a key in a major version, the app
finds out at build or test time, not from a user.

**5. Plurals and formatting.**
- Text that depends on a count ("1 file" / "3 files", in the storage move dialog today) uses keys with plural forms
  (`.One`, `.Other`, plus `.Few` and `.Many` where a language needs them), chosen by an `IPluralRules` service.
  English rules are the default, and a translation supplies the rules for its language.
- Dates and numbers already format with the current culture (`ToString("g")` and `"f"`). The portal sets
  `BlazorWebAssemblyLoadAllGlobalizationData`, so this works for every supported culture, not only the browser's.

**6. Choosing the language.**
- The app lists its supported cultures.
- The packaged Settings page shows a Language option when there is more than one.
- The choice is stored under the prefixed storage key (U7) and applied in `Program.cs` before the app starts, which is
  the standard Blazor WebAssembly pattern. Changing it reloads the app.
- By default the app uses the browser's language, if it is supported.

**7. Text that stays in the template.**
`index.html` contains the update banner and the "An unhandled error has occurred" message. Both are plain HTML, so
they still work when the app itself fails. They stay app-owned, and the app edits them directly.

**8. Testing for hard-coded text.**
The framework's Playwright suite runs once more with a pseudo-localisation culture, which wraps every resource string
(for example `[!! Ŷöûŕ ƒîļéš !!]`). Any visible text without the wrapping is still hard-coded, and the test fails.
Existing UI tests that check visible text read it from the default resources, or check element ids instead.

### What a generated app contains

```
AcmePortal.slnx
Directory.Packages.props      <- one BlazwizVersion property, used by every Devoctomy.Blazwiz.* package
AcmePortal.Portal/
  Program.cs                  <- AddBlazwiz(...) module setup, about 30 lines
  App.razor                   <- <BlazwizRouter AppAssembly=... />
  Pages/Dashboard.razor       <- the app's own pages
  Theme/theme.css             <- --bw-* token overrides (branding)
  wwwroot/index.html          <- one CSS bundle link and the theme pre-paint snippet
  wwwroot/appsettings.json
AcmePortal.Application/       <- the app's view models and services (Notes, if chosen)
AcmePortal.Application.UnitTests/
AcmePortal.Portal.UITests/    <- uses Devoctomy.Blazwiz.Testing
cloudformation/               <- extension stack only, once U15 is done
```

`Program.cs` would read roughly:

```csharp
builder.Services.AddBlazwiz(builder.Configuration, app => app
    .UseIdentity(name: "acmeportal", id: "…guid…")
    .AddAuth(auth => auth.UseCognito().UsePasskeys())
    .AddSync(sync => sync.UseS3().AddProfile(NoteService.ProfileName, …))
    .AddEncryption()
    .AddText(text => text
        .SupportedCultures("en-GB", "fr-FR")
        .AddOverrides<AppText>())          // the app's own .resx: rewording and translations
    .AddUi(ui => ui
        .Navigation(nav => nav.Add("Files", "files", "bi-journal-text"))
        .Settings(s => s.AddSection<MySettingsSection>("My app"))));
```

### Upgrading, from the user's side

1. Dependabot or `dotnet outdated` reports that `Devoctomy.Blazwiz.*` 1.4.0 is available.
2. The user bumps `BlazwizVersion` in `Directory.Packages.props`, then builds.
3. Data migrations run at the next sign-in (U13). Any infrastructure change is listed in the release notes (until
   U15 is done).
4. Major versions come with an `UPGRADING.md` entry. Package validation (U12) means a minor or patch version cannot
   break their code without the framework build failing first.

### How the repository changes

This repository becomes the framework repository:

```
src/Devoctomy.Blazwiz.*/        packages
tests/Devoctomy.Blazwiz.*.Tests/ framework unit, integration and UI tests (the four suites move here)
template/                        the generated app (what dotnet new produces) and .template.config
samples/                         optional: the full-featured app used for the Playwright suite
```

The current principle that this repository is a working app and a template at the same time still holds, but only
for the `template/` folder. Inside this repository it uses `ProjectReference`s to `src/`. A `UseBlazwizPackages`
MSBuild property switches it to `PackageReference`s for the template test. `test-template.ps1` then packs the
packages into a local feed, creates apps from the template and builds them against that feed. That checks the real
upgrade path.

---

## 5. Notes is an example, not part of the framework (D2)

Notes is only an example of using the sync and storage features. It stays as optional source in the template
(`--Notes`), for users to keep, change or delete. It will not become a package.

The rule this sets: **the framework packages know nothing about notes.** They know about sync profiles, local
stores, S3 and encryption, and nothing more specific.
- The notes feature (Files page, note editor, markdown services, `NoteService`, `IndexedDbNotesStateStore`,
  about 4k lines) stays in the template's Application project.
- The notes sync profile (`notes/**/*.md`, its store names) is set up in the template's `Program.cs`, as it is today
  in `AddPortalApplicationServices`.
- UI text in the packages is neutral (U16), and doc-comment examples are too (T9). The template's notes example uses
  text overrides to say "notes" where the packages say "files".
- A check in Phase 2 enforces this: a case-insensitive search for `note` in `src/` must find nothing.

---

## 6. Plan

Each phase leaves the app fully working, with all four test suites passing (unit, Core integration, Application
unit, Playwright UI), before the next phase starts.

### Phase 0: groundwork (small)
- [x] T1, T2, T3, T4: remove stale template entries and the old `.sln`, fix the dashboard text, move `GreetingService`.
- [x] T10: remove the compatibility code for earlier development builds.
- [x] T5: standardise on `TimeProvider`.
- [x] U7: introduce `BlazwizAppOptions` and a storage key builder. Replace every literal key, including the one in
  `app.js`.
- [x] Write the SemVer policy and the list of what counts as public API (U12, D5).

### Phase 1: extension points, still inside the current projects
Build U3 to U6 and U11 in the existing projects, then move the current sync, E2E and offline UI onto them. This is
the riskiest phase. Doing it before splitting projects keeps the diff readable.
- [ ] Shell regions (U3). Move the banners, sync pill and migration overlay out of `MainLayout.razor`.
- [ ] `ISessionLifecycleHandler` (U4). Move the sync and E2E startup out of `MainLayoutViewModel` and the
  sync-off option out of `LoginViewModel`.
- [ ] Navigation, settings and login-option registries (U5). Remove every `#if (IncludeNotes)` from shared classes.
- [ ] `BlazwizRouter` with additional assemblies, and replacing or disabling pages through DI configuration (U6).
- [ ] Text infrastructure (U16): the override-aware localiser factory, plural rules and `BlazwizException` codes.
- [ ] Move every visible string into resources with neutral wording: Razor, view models, presenters and JS.
  Replace each `ex.Message`, `LoginResult.Error` and `SyncStatus.LastError` with a code. Add the notes overrides to
  the template app.
- [ ] Exit check: `grep "#if (Include"` matches only files that belong to the app (Program.cs, the notes feature,
  csproj files, docs).

### Phase 2: split into packages
- [ ] Restructure into `src/`, `tests/` and `template/` (section 4).
- [ ] Create the projects in the package table and move the code. Rename namespaces to `Devoctomy.Blazwiz.*`.
- [ ] Each package gets an `AddBlazwiz*()` that registers its services and view models (U11) and binds its options
  section (U10).
- [ ] Convert global scripts to ES modules and JS initializers (U8). Split the CSS (U9).
- [ ] Add PublicApiAnalyzers and package validation (U12). Ship 0.x previews first, so the API can still change
  before 1.0.
- [ ] Rewrite `test-template.ps1` to pack to a local feed and build the generated apps against it.
- [ ] Replace `--IncludeNotes` with `--Sync`, `--Encryption` and `--Notes` (D6). Test each combination in
  `test-template.ps1`.
- [ ] Exit check: no mention of notes in `src/` (section 5).
- [ ] Exit check: the pseudo-localisation Playwright run finds no hard-coded visible text. Each package ships its
  generated list of text keys, and `TextOverrides.AssertValid()` is available in `Devoctomy.Blazwiz.Testing`.

### Phase 3: missing user features
- [ ] U14: change password (Profile page), forgot password and confirm code (login page), using
  `Amazon.Extensions.CognitoAuthentication`.
- [ ] Placeholder (local) auth gets matching no-op implementations, so local development still works.
- [ ] UI tests for each flow, with the Cognito parts covered by the existing `@aws` integration tests.
- [ ] Optional, same pattern: MFA (TOTP), self sign-up toggle.
- [ ] Language option in Settings and culture applied at startup (section 4, "Text and localisation", point 6). Add a
  second culture to the template's test app to prove it end to end.

### Phase 4: release process and infrastructure
- [ ] CI: build, test, pack and validate on pull requests. Publish every package and the template package to
  nuget.org on tags, with the same version (D9).
- [ ] `CHANGELOG.md` and `UPGRADING.md`. Show the framework version in About (T8).
- [ ] Data migration contract (U13), in place before 1.0 so every released version can migrate data from the one
  before.
- [ ] U15: release the AWS base stack with each framework version, plus an app-owned extension stack (D4). This is
  last on purpose.
- [ ] Prove the upgrade path: create an app from version N, customise it, bump to N+1, and check it builds, its
  tests pass and its data survives. Add this as an automated check in `test-template.ps1`.

---

## 7. Decisions

| # | Decision | Outcome |
|---|---|---|
| D1 | Package ID prefix and namespaces | `Devoctomy.Blazwiz.*`, matching the existing template package ID |
| D2 | Should Notes become a package? | No. Notes is an example of the sync and storage features, kept as optional source in the template. The packages know nothing about notes (section 5) |
| D3 | How does an app replace a packaged page? | Through DI configuration at startup. Packaged routes render the component configured for them, which the app can replace or disable in `Program.cs` (U6) |
| D4 | How are AWS infrastructure changes delivered? (CloudFormation stack and deploy scripts, not NuGet) | A versioned base stack released with the framework, plus an app-owned extension stack. Done last (U15, Phase 4) |
| D5 | Versioning | All packages in lockstep. 0.x previews until Phase 3 is done, then 1.0 |
| D6 | Template options | Replace `--IncludeNotes` with `--Sync`, `--Encryption` and `--Notes` (Notes requires Sync) |
| D7 | Wording and localisation | Every string a package can show to users is a resource the app can override or translate. Non-UI packages return codes, not sentences (U16, section 4) |
| D8 | Compatibility with apps or data from before these changes | None. The project is in development and unreleased, so nothing is kept for earlier builds, and existing compatibility code is removed (T10). Data migrations start from 1.0 (U13) |
| D9 | Where packages are published | nuget.org, for all packages and the template (Phase 4) |
