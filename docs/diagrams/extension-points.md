# Extension points

The shell (`src/Devoctomy.Blazwiz.UI`) has no `#if` blocks or references for optional modules. A module (such as
the sync module, `src/Devoctomy.Blazwiz.UI.Sync`) and the app plug into it by registering things in `Program.cs`
or their own `Add*` method. Each extension point is an `IServiceCollection` extension method, mostly in
`src/Devoctomy.Blazwiz.UI/Extensions/ShellServiceCollectionExtensions.cs`, and a registry the shell reads.

| Extension point | Register with | Read by | Shown |
|---|---|---|---|
| Shell regions | `AddShellComponent<TComponent>(ShellRegion, order)` | `ShellRegistry`, `MainLayoutViewModel.Banners` / `TopBarActions` / `Floating` / `Overlays` | In `MainLayout`, in order |
| Settings sections | `AddSettingsSection<TComponent>(order)` | `SettingsSectionRegistry`, `SettingsView` | On the Settings page, in order (`UiServiceCollectionExtensions.SettingsOrder`) |
| Sign-in options | `AddSignInOption<TOption, TComponent>()` | `LoginViewModel` (`ISignInOption`, `SignInOptionRegistration`) | Under "Advanced options" on the sign-in page, expanded when one is set |
| Session lifecycle handlers | `AddSessionLifecycleHandler<THandler>()` (Auth) | `SessionLifecycle` | Not shown: run at sign in, shell loading, session start, signing out, signed out ([session-management.md](session-management.md), diagram 1) |
| Navigation items | `AddNavigationItem(NavigationItem)` | `NavigationMenuService` | In the sidebar, by `Order`. Text from `TextResource` when set. Hidden when its `PageKey` page is disabled |
| Page assemblies | `AddPageAssembly(assembly)` | `BlazwizRouter` (`AdditionalAssemblies`) | A module's `@page` components become routes |
| Page replacement | `ReplacePage<TComponent>(key)`, `DisablePage(key)` | `PageRegistry`, `PageHost` | The framework's pages (`BlazwizPages`: `login`, `forgot-password`, `profile`, `settings`, `sync`) show the last component registered for their key, or Not found when disabled |
| Data migrations | `AddDataMigration<TMigration>()` (Auth) | `DataMigrationRunner`, a lifecycle handler | Not shown: run as the shell loads |
| Text | `AddBlazwizText(text => ...)` (Core) | `OverridingStringLocalizerFactory` | Every text from resources ([text.md](text.md)) |
| Error descriptions | `AddErrorText<TResource>()` (UI) | `ErrorText` | Error codes (`BlazwizException.Code`) described from the module's resources |

## 1. Where things appear in the shell

```mermaid
flowchart TB
    subgraph Main["MainLayout (signed in)"]
        direction TB
        Top["Top bar: TopBarActions region<br/>(nothing registered by the framework)"]
        Side["Sidebar: navigation items by Order<br/>(the app's, Sync 20, Settings 10000)"]
        Banners["Banners region: OfflineBanner 0,<br/>E2eBanner 10, FolderAccessBanner 20"]
        Body["Page content (the route)<br/>framework pages through PageHost"]
        Floating["Floating region: SyncPill 0"]
        Overlays["Overlays region: ToastHost 0,<br/>StorageMigrationOverlay 0"]
    end

    subgraph SettingsPage["Settings page (settings)"]
        S1["Appearance 100"]
        S2["Language 150"]
        S3["Offline 200"]
        S4["Sync 300 (SyncSettingsSection, with the profiles)"]
        S5["Argon2id 400 (Argon2idPanel)"]
        S6["About 10000"]
        S1 --> S2 --> S3 --> S4 --> S5 --> S6
    end

    subgraph LoginPage["Sign-in page (login)"]
        L1["Email and password, remembered users, passkey"]
        L2["Advanced options: sign-in options<br/>(StartSyncOffOption from the sync module)"]
        L1 --> L2
    end

    Body -.-> SettingsPage
```

Items from the sync module are added by `AddBlazwizSyncModule`; everything else by `AddBlazwizUi`. The template's own
sidebar items (Dashboard, Showcase, and the notes example's Notebooks) are added by the app.

## 2. How a module plugs in

The sync module's `AddBlazwizSyncModule` (`src/Devoctomy.Blazwiz.UI.Sync/Extensions/SyncModuleServiceCollectionExtensions.cs`)
uses every kind of extension point:

```mermaid
flowchart LR
    Mod["AddBlazwizSyncModule"]
    Mod -- "AddShellComponent" --> R1["E2eBanner, FolderAccessBanner (Banners)<br/>SyncPill (Floating)<br/>StorageMigrationOverlay (Overlays)"]
    Mod -- "AddSettingsSection" --> R2["SyncSettingsSection, Argon2idPanel"]
    Mod -- "AddSignInOption" --> R3["StartSyncOffSignInOption + StartSyncOffOption"]
    Mod -- "AddSessionLifecycleHandler" --> R4["SyncSessionHandler"]
    Mod -- "AddNavigationItem" --> R5["Sync (Nav.Sync from SyncUiText, PageKey sync)"]
    Mod -- "AddPageAssembly + ReplacePage" --> R6["SyncPage (/sync) showing SyncView"]
    Mod -- "AddErrorText" --> R7["SyncUiText error descriptions"]
```

## 3. Replacing or turning off a framework page

Each framework page is a route component (for example `LoginPage`, `@page "/login"`) holding a `PageHost` with the
page's key. `PageHost` asks `PageRegistry` for the component registered last for that key: the framework registers
its own view first, so the app's registration, made later in `Program.cs`, wins.

```mermaid
sequenceDiagram
    autonumber
    participant Program as Program.cs
    participant Reg as PageRegistry
    participant Router as BlazwizRouter
    participant Host as PageHost (Key = login)
    participant Menu as NavigationMenuService

    Program->>Reg: AddBlazwizUi: ReplacePage<LoginView>("login")
    Program->>Reg: The app: ReplacePage<MyLogin>("login")
    Program->>Reg: The app: DisablePage("sync")
    Router->>Host: /login matched LoginPage
    Host->>Reg: GetComponent("login")
    Reg-->>Host: MyLogin (the last registration)
    Host->>Host: DynamicComponent MyLogin
    Menu->>Reg: IsEnabled("sync")
    Reg-->>Menu: false, so the Sync item is not in the sidebar
    Note over Host: /sync now shows Not found
```

## 4. Sign-in options

An option (`ISignInOption`) is a choice made on the sign-in page that takes effect at sign in. Its component is shown
under "Advanced options", which opens by itself when an option is not at its default (`IsSet`), so it is never hidden
while it changes what happens.

```mermaid
sequenceDiagram
    autonumber
    actor User
    participant VM as LoginViewModel
    participant SIO as StartSyncOffSignInOption
    participant Conn as ConnectivityService
    participant Life as SessionLifecycle

    VM->>SIO: LoadAsync() (on opening the page)
    SIO->>Conn: IsSyncEnabled
    VM->>VM: IsAdvancedOptionsExpanded = any option IsSet
    User->>VM: Tick "Start with sync off", sign in
    Note over VM: Signed in (password, passkey or offline)
    VM->>SIO: ApplyAsync() (before anything syncs)
    SIO->>Conn: SetSyncEnabledAsync(false)
    VM->>Life: SignedInAsync()
```

The UI tests `SyncTests.GivenSyncOff_WhenSigningOutAndBackIn_ThenAdvancedOptionsShowItAndUntickingTurnsSyncOn` and
`GivenSyncOn_WhenOpeningLogin_ThenAdvancedOptionsCollapsed` cover this.
