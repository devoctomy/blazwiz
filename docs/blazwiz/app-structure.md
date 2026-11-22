# How an app is put together

This page walks through an app made from the template: its projects, how `Program.cs` adds the framework, and the
settings it reads. The examples call the app `MyApp`, as the template does; yours has your app's name.

## The projects

| Project | What goes in it |
|---|---|
| `MyApp.Portal` | The Blazor WebAssembly app itself: `Program.cs`, the pages (`Pages/*.razor`), `wwwroot` (`index.html`, settings, styles, icons, the service worker). **Views only**: no logic. |
| `MyApp.Portal.Application` | Your view models (`ViewModels`), services (`Services`, with their interfaces in `Services/Interfaces`), text resources (`Text`), sync profiles (`Sync`) and the `Add*` methods that register them (`Extensions`). All of it can be unit tested. |
| `MyApp.Portal.Models` | Plain data classes (DTOs and settings). No logic. |
| `MyApp.Portal.Application.UnitTests` | xUnit and Moq tests, in the same folders as `MyApp.Portal.Application`. |
| `MyApp.Portal.UITests` | Playwright end to end tests. They start the app themselves. |
| `cloudformation/` | The scripts that deploy the app to AWS, and `app.yaml` for your own AWS resources. |

Package versions are kept in one place, `Directory.Packages.props`. `BlazwizVersion` there sets the version of every
`Devoctomy.Blazwiz.*` package.

Why split views from view models? Razor pages can only be tested in a browser. A view model is a plain class, so a
unit test can create it, call it, and check the result. Keeping the logic in `MyApp.Portal.Application` means all of
it is tested. [Views and view models](views-and-view-models.md) explains the pattern.

## The app's identity

`MyApp.Portal.Application/AppIdentity.cs` holds two constants:

```csharp
public static class AppIdentity
{
    public const string Name = "myapp";
    public const string Id = "a78e68c7-90e0-4f90-aa3d-73f7b2c9f2c8";
}
```

- `Name` is lower case letters and numbers, starting with a letter. Everything the app keeps in the browser is named
  after it, so `session` is stored as `myapp_session` (see [Browser storage](storage.md)).
- `Id` is a GUID, made new for each app the template creates. Keys derived from the user's passkey are salted with it,
  so **don't change it** once people use the app: everything encrypted with those keys (such as remembered passwords)
  would become unreadable.

## Program.cs

`MyApp.Portal/Program.cs` puts the app together. Here it is with every template option on, and the template's
option markers left out:

```csharp
var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// 1. Who the app is. Must come first.
builder.Services.AddBlazwizApp(AppIdentity.Name, AppIdentity.Id);

// 2. The shell, and signing in.
builder.Services.AddBlazwizUi(builder.Configuration);
builder.Services.AddBlazwizCognito(builder.Configuration, allowPlaceholderSignIn: builder.HostEnvironment.IsDevelopment());

// 3. Optional modules.
builder.Services.AddBlazwizAppSyncEvents(builder.Configuration, allowLocalChannel: builder.HostEnvironment.IsDevelopment());
builder.Services.AddBlazwizSyncModule(builder.Configuration, sync => sync
    .UseEmbeddedConfiguration(typeof(Program).Assembly, "sync.json")
    .UseProfileDefaults(profile => profile.AllowDeviceUnlock = true)
    .AddProfilesFromAssemblyContaining<Program>()
    .AddProfilesFromAssemblyContaining<DashboardViewModel>());

// 4. The notes example (template only).
builder.Services.AddNotes();
builder.Services.AddScoped<INoteEditorInteropService, NoteEditorInteropService>();

// 5. Your own services, pages and sidebar items.
builder.Services.AddAppServices();

// 6. Your view models: every one in the application assembly is registered.
builder.Services.AddMvvm(options =>
{
    options.HostingModelType = BlazorHostingModelType.WebAssembly;
    options.RegisterViewModelsFromAssemblyContaining<DashboardViewModel>();
});

var host = builder.Build();

// 7. The language the user chose (or the browser's), applied before the app starts.
await using (var scope = host.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<ICultureService>().ApplySavedCultureAsync();
}

await host.RunAsync();
```

What each call does:

| Call | Package | What it adds | Settings it reads |
|---|---|---|---|
| `AddBlazwizApp(name, id)` | Core | The app's name and id, and the storage prefix. The rest of the framework refuses to start without it. | none |
| `AddBlazwizUi(configuration)` | UI | The shell, its pages, theme, text, and everything they use (Core, Cryptography, Browser, Auth). | `AppSettings`, `Text:SupportedCultures`, `Text:PseudoLocalization` |
| `AddBlazwizCognito(configuration, allowPlaceholderSignIn)` | Auth.Cognito | Sign in with Cognito, and the session's AWS credentials. | `AwsCognito` |
| `AddBlazwizAppSyncEvents(configuration, allowLocalChannel)` | Events.AppSync | User events between the user's devices. | `Events` |
| `AddBlazwizSyncModule(configuration, sync => ...)` | UI.Sync | Sync, end to end encryption, the Sync page and its settings. | `AwsS3`, `Argon2id`, and `Sync` through the builder |
| `AddMvvm(...)` | Blazing.Mvvm | Registers your view models. | none |

Each `Add*` method registers its own package's services, view models and settings, and calls the methods of the
packages it needs. Those shared ones, such as `AddBlazwizAuth` (called by both `AddBlazwizUi` and
`AddBlazwizCognito`), are safe to call more than once.

`AddAppServices` is yours: it is in `MyApp.Portal.Application/Extensions/AppServiceCollectionExtensions.cs`. Add your
own services and sidebar items there. The notes example has its own, `AddNotes`, in
`NotesServiceCollectionExtensions.cs`: a good model for grouping a feature's registrations.

> **No static helper classes.** blazwiz puts logic in services: an interface plus a class, registered in an `Add*`
> method, so it can be replaced and tested. Do the same in your app.

## App.razor

`App.razor` holds only the framework's router:

```razor
<BlazwizRouter AppAssembly="@typeof(App).Assembly" />
```

`BlazwizRouter` finds your pages, the framework's pages and the pages of any module you add. Pages use the main
layout (the shell) unless they choose another; `DefaultLayout` changes that. If nobody is signed in, the shell sends
the user to `/login`.

## Configuration

Settings are in `MyApp.Portal/wwwroot/appsettings.json`, with overrides per environment:

- `appsettings.Development.json`: used by `dotnet run`, not published.
- `appsettings.Production.json`: written by the deploy script with your stack's ids. It is git ignored.

| Section | Read by | Keys |
|---|---|---|
| `AppSettings` | `AddBlazwizUi` (`AppSettings`) | `ApplicationTitle`, `ApplicationDescription`, `LogoUrl` (defaults to `images/icon.svg`), `CopyrightMessage`, `EnablePortalLoginAuthn` (offer a passkey after "Remember me"), `EnableRememberMeName` (let the user give a name to be remembered by) |
| `Text` | `AddBlazwizUi` | `SupportedCultures` (e.g. `["en-GB", "fr-FR"]`, the first is the default), `PseudoLocalization` (testing only) |
| `AwsCognito` | `AddBlazwizCognito` (`AwsCognitoConfig`) | `Region`, `UserPoolId`, `UserPoolClientId`, `IdentityPoolId` |
| `AwsS3` | `AddBlazwizSyncModule` (`AwsS3Config`) | `Region`, `DataBucketName` |
| `Argon2id` | `AddBlazwizSyncModule` | `MemoryKib`, `Iterations`, `Parallelism`, `KeyLength`: the Argon2id settings for new keys, when not the build's defaults |
| `Events` | `AddBlazwizAppSyncEvents` | `HttpEndpoint`, `RealtimeEndpoint` |
| `Sync` | the sync builder, when you call `UseConfiguration` | see [Sync and end to end encryption](sync-and-encryption.md#settings-from-a-file) |

The template's `appsettings.Development.json` lowers the Argon2id memory (`"MemoryKib": 19456`), so entering a
passphrase is quick while developing and in the UI tests. Don't copy that into production settings.

The sync settings in the template live in `MyApp.Portal/sync.json`, which is embedded in the app (an
`EmbeddedResource` in `MyApp.Portal.csproj`) and read by `UseEmbeddedConfiguration`.

## wwwroot

| File | What it is |
|---|---|
| `index.html` | The page Blazor starts in. It links the framework's styles, then yours (`css/app.css`), applies the saved theme before Blazor starts, and holds the `#app-update-banner` shown when a new version is ready. Its `data-app-name` attribute must match `AppIdentity.Name`. |
| `css/app.css` | Your styles, loaded last. Re-brand the app here (see [Theming](theming.md)). |
| `manifest.webmanifest` | The PWA manifest: name, icons, colours. |
| `service-worker.js`, `service-worker.published.js` | The development and published service workers. The published one caches the app for offline use and reports its progress, which `AppUpdateIndicator` shows. |
| `images/icon.svg` | The default logo (`AppSettings.LogoUrl`). |

The framework's packages bring their own JavaScript and styles. You don't add their scripts to `index.html`: each
package loads its own (the sync module, for example, adds its stylesheet when Blazor starts).
