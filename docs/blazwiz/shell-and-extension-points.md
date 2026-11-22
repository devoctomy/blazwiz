# The shell and its extension points

The **shell** is the frame around every signed-in page: the sidebar, the top bar, the banners above the page and the
overlays over it. It comes from `Devoctomy.Blazwiz.UI` (`MainLayout`), together with the sign-in, Profile and Settings
pages.

You never edit the shell. Instead, the shell has **extension points**: places your app (or a module, like sync) adds
to by registering something in `Program.cs` or in an `Add*` method. The shell has no knowledge of optional modules;
the sync module plugs in through exactly the same extension points your app uses.

Most extension points are methods in `Devoctomy.Blazwiz.UI.Extensions.ShellServiceCollectionExtensions`. The session
ones are in `Devoctomy.Blazwiz.Auth.Extensions.AuthServiceCollectionExtensions`.

| Extension point | Register with | Where it shows |
|---|---|---|
| [Navigation items](#navigation-items) | `AddNavigationItem(item)` | The sidebar |
| [Shell regions](#shell-regions) | `AddShellComponent<TComponent>(region, order)` | Banners, top bar, floating corner, overlays |
| [Settings sections](#settings-sections) | `AddSettingsSection<TComponent>(order)`, `RemoveSettingsSection<TComponent>()` | The Settings page |
| [Sign-in options](#sign-in-options) | `AddSignInOption<TOption, TComponent>()` | "Advanced options" on the sign-in page |
| [Session lifecycle handlers](#session-lifecycle-handlers) | `AddSessionLifecycleHandler<THandler>()` | Not shown: runs at sign in and sign out |
| [Sign out everywhere handlers](sign-in-and-sessions.md#signing-out-everywhere) | `AddSignOutEverywhereHandler<THandler>()` | Not shown: runs when the user signs out everywhere |
| [Page assemblies](#page-assemblies) | `AddPageAssembly(assembly)` | Routes from another assembly |
| [Replacing pages](#replacing-or-turning-off-a-framework-page) | `ReplacePage<TComponent>(key)`, `DisablePage(key)` | The framework's own pages |
| [Text](text-and-translation.md) | `AddBlazwizText(text => ...)` | Every text from resources |
| [Error text](text-and-translation.md#errors) | `AddErrorText<TResource>()` | Error messages for your error codes |

## Navigation items

```csharp
services.AddNavigationItem(new NavigationItem
{
    Text = "Things",
    Href = "things",
    Icon = "bi-box",
    Description = "Everything you have added.",
    Order = 20,
});
```

`NavigationItem` has:

| Property | What it is |
|---|---|
| `Text` | The label. A resource key when `TextResource` is set. |
| `TextResource` | The class your text resources are named after, so `Text` and `Description` are keys you can translate. Null for literal text. |
| `Href` | The relative URL, e.g. `things`. An empty string for the home page. |
| `Icon` | A [Bootstrap Icons](https://icons.getbootstrap.com/) class, e.g. `bi-box`. |
| `ExactMatch` | Only highlight the item when the URL matches exactly (use it for the home page). |
| `Description` | A short description, used by the template's Dashboard for its quick links. |
| `Order` | Its place in the sidebar; lower comes first. |
| `PageKey` | The framework page it links to, if any. The item is hidden when that page is turned off. |

The framework's own items are Sync (`Order = 20`, from the sync module) and Settings (`Order = 10_000`). The
template's are Dashboard (0), Notebooks (10) and Showcase (30).

## Shell regions

Put a component of your own into the shell:

```csharp
services.AddShellComponent<MaintenanceBanner>(ShellRegion.Banners, order: 30);
```

| `ShellRegion` | Where | What the framework puts there |
|---|---|---|
| `Banners` | Above the page content | `OfflineBanner` (0); with sync, `E2eBanner` (10) and `FolderAccessBanner` (20) |
| `TopBarActions` | In the top bar, before the theme toggle: small icon buttons | nothing |
| `Floating` | Floating in the bottom corner: status pills | with sync, `SyncPill` (0) |
| `Overlays` | Over the whole shell: blocking overlays and dialogs | `ToastHost` (0); with sync, `StorageMigrationOverlay` (0) |

Within a region, lower `order` comes first. The component is a normal view: give it a view model and inherit
`MvvmComponentBase<TViewModel>`. It takes no parameters, since the shell creates it.

Regions only exist in the main layout, so they show on signed-in pages, not on the sign-in page.

## Settings sections

Add a section to the Settings page:

```csharp
services.AddSettingsSection<BackupSettingsSection>(UiServiceCollectionExtensions.SettingsOrder.Offline + 50);
```

The framework's sections, and their positions (`UiServiceCollectionExtensions.SettingsOrder`):

| Section | Order | Shown |
|---|---|---|
| Appearance (`AppearanceSettingsSection`) | `SettingsOrder.Appearance` (100) | always |
| Language (`LanguageSettingsSection`) | `SettingsOrder.Language` (150) | when the app offers more than one language |
| Offline mode (`OfflineSettingsSection`) | `SettingsOrder.Offline` (200) | always |
| This device (`DeviceSettingsSection`) | `SettingsOrder.Device` (250) | when any `ISignOutEverywhereHandler` is registered (user events register one) |
| Sync (`SyncSettingsSection`) | 300 | with the sync module |
| End to end encryption (`E2eProfilesSection`) | 350 | with the sync module |
| Argon2id benchmark (`Argon2idBenchmarkPanel`) | 400 | with the sync module |
| About (`AboutSettingsSection`) | `SettingsOrder.About` (10 000) | always, last |

Take any section off the page, whoever added it:

```csharp
builder.Services.RemoveSettingsSection<E2eProfilesSection>();
```

It works whether it is called before or after the section was added. Use it when your app does the same job on its
own pages.

## Sign-in options

A sign-in option is a choice the user makes on the sign-in page, under "Advanced options", which takes effect when
they sign in. The sync module's "Start with sync turned off" is one.

An option has two parts:

1. A class implementing `ISignInOption`:

   ```csharp
   public class StartSyncOffSignInOption(IConnectivityService connectivityService) : ISignInOption
   {
       public bool StartWithSyncOff { get; set; }

       // True when it is not at its default: the advanced options then open by themselves.
       public bool IsSet => StartWithSyncOff;

       // When the sign-in page opens.
       public async Task LoadAsync()
       {
           await connectivityService.InitializeAsync();
           StartWithSyncOff = !connectivityService.IsSyncEnabled;
       }

       // After a successful sign in, before anything else happens.
       public async Task ApplyAsync()
       {
           if (StartWithSyncOff == connectivityService.IsSyncEnabled)
           {
               await connectivityService.SetSyncEnabledAsync(!StartWithSyncOff);
           }
       }
   }
   ```

   `ISignInOption` also has `Order` (default 0) to place it among the others.

2. A component that shows it. It injects the option by its own type, and gets the same instance the sign-in page
   uses:

   ```razor
   @inject StartSyncOffSignInOption Option

   <div class="form-check">
       <input type="checkbox" id="start-sync-off" class="form-check-input" @bind="Option.StartWithSyncOff" />
       <label class="form-check-label" for="start-sync-off">...</label>
   </div>
   ```

Register both together:

```csharp
services.AddSignInOption<StartSyncOffSignInOption, StartSyncOffOption>();
```

## Session lifecycle handlers

A lifecycle handler runs your code when a session starts and ends: start background work after sign in, stop it before
sign out, forget what you hold in memory.

```csharp
public sealed class MyHandler : ISessionLifecycleHandler
{
    public Task OnSessionStartedAsync(UserSession session)
    {
        // Start long-running work. Runs in the background once the shell is shown.
        return Task.CompletedTask;
    }

    public Task OnSigningOutAsync()
    {
        // Stop it, before the user is signed out.
        return Task.CompletedTask;
    }
}
```

```csharp
services.AddSessionLifecycleHandler<MyHandler>();
```

Every method has a default that does nothing, so implement only what you need:

| Method | When it runs |
|---|---|
| `OnSignedInAsync()` | On the sign-in page, straight after a successful sign in (online or offline). Use it for things that must follow the user's click, such as browser permission prompts. |
| `OnShellLoadingAsync(session)` | When the shell knows who is signed in, before it is shown. Keep it short: the shell waits for it. |
| `OnSessionStartedAsync(session)` | Once the shell is shown, in the background: after a sign in, and after a reload with a saved session. |
| `OnSigningOutAsync()` | When the user signs out or the session ends, before the sign-in service signs out. |
| `OnSignedOutAsync()` | After signing out, before the sign-in page is shown. |

`Order` (default 0) sets the order handlers run in; lower runs first. A handler that throws does not stop the others.
Handlers are scoped services, so they can inject anything the app uses.

The template's notes example has one: `NotebookLabelHandler` starts listening for notebooks being unlocked in
`OnSessionStartedAsync`, and stops in `OnSigningOutAsync`. The framework uses the same mechanism for its own work: AWS
credentials, sync, data migrations, the resume check.

## Page assemblies

`BlazwizRouter` always finds pages in your app's assembly and in the framework's. If you keep pages in another
assembly (a module of your own, say), add it:

```csharp
services.AddPageAssembly(typeof(MyModulePage).Assembly);
```

## Replacing or turning off a framework page

The framework's pages each have a key, in `BlazwizPages`:

| Key | Route | Page |
|---|---|---|
| `BlazwizPages.Login` | `/login` | Sign in |
| `BlazwizPages.ForgotPassword` | `/forgot-password` | Reset a forgotten password |
| `BlazwizPages.Profile` | `/profile` | The signed-in user's profile |
| `BlazwizPages.Settings` | `/settings` | Settings |
| `BlazwizPages.Sync` | `/sync` | The sync log and status (sync module) |

Show your own component in place of one, keeping its route:

```csharp
builder.Services.ReplacePage<MyLogin>(BlazwizPages.Login);
```

The last registration for a key wins, and the framework registers its own first, so yours (made later in
`Program.cs`) is the one shown.

Or turn a page off:

```csharp
builder.Services.DisablePage(BlazwizPages.ForgotPassword);
```

Its route then shows Not Found, and sidebar items linking to it (through `PageKey`) are hidden. Turning off
`ForgotPassword` also hides the "Forgot password" link on the sign-in page.

How it works: each framework route is a tiny page that renders `PageHost` with the page's key. `PageHost` asks the
page registry which component to show.

## Asking the shell to reload the page

A module can ask the shell to show the current page again from scratch, for example after access to a folder of files
was restored. Inject `IShellContentService` and call `RequestReload()`.

## How the sync module plugs in

`AddBlazwizSyncModule` (`src/Devoctomy.Blazwiz.UI.Sync/Extensions/SyncModuleServiceCollectionExtensions.cs`) uses
nearly every extension point, so it is a good example to read:

- shell components: `E2eBanner`, `FolderAccessBanner`, `SyncPill`, `StorageMigrationOverlay`;
- Settings sections: `SyncSettingsSection`, `E2eProfilesSection`, `Argon2idBenchmarkPanel`;
- a sign-in option: `StartSyncOffSignInOption` with `StartSyncOffOption`;
- a lifecycle handler: `SyncSessionHandler`;
- a sidebar item: Sync, with its text from `SyncUiText`;
- a page assembly and a page: `SyncPage` (`/sync`) showing `SyncView`;
- error text: `AddErrorText<SyncUiText>()`.

The diagrams in [docs/diagrams/extension-points.md](../diagrams/extension-points.md) show where each part appears.
