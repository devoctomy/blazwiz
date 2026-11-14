using Blazing.Mvvm;
using Devoctomy.Blazwiz.Auth.Cognito.Extensions;
using Devoctomy.Blazwiz.Core.Extensions;
using Devoctomy.Blazwiz.UI.Extensions;
using Devoctomy.Blazwiz.UI.Models;
using Devoctomy.Blazwiz.UI.Services.Interfaces;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MyApp.Portal;
using MyApp.Portal.Application;
using MyApp.Portal.Application.Extensions;
using MyApp.Portal.Application.ViewModels;
//#if (IncludeEvents)
using Devoctomy.Blazwiz.Events.AppSync.Extensions;
//#endif
//#if (IncludeSync)
using Devoctomy.Blazwiz.UI.Sync.Extensions;
//#endif
#if (IncludeNotes)
using MyApp.Portal.Application.Services.Interfaces;
using MyApp.Portal.Services;
#endif

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// The app's name and ID: everything it keeps in the browser is named after it (e.g. myapp_session).
builder.Services.AddBlazwizApp(AppIdentity.Name, AppIdentity.Id);

// The framework: the app shell (sign in, theme, settings and their pages), with settings from wwwroot/appsettings.json
// (+ appsettings.{Environment}.json), signing in with Cognito once a user pool is configured.
builder.Services.AddBlazwizUi(builder.Configuration);
builder.Services.AddBlazwizCognito(builder.Configuration, allowPlaceholderSignIn: builder.HostEnvironment.IsDevelopment());
//#if (IncludeEvents)

// User events: the user's open devices tell each other at once when they sign out everywhere, over the stack's AppSync
// Events API (the Events section, written by cloudformation/deploy.ps1). In development, without it, the browser's
// other tabs stand in.
builder.Services.AddBlazwizAppSyncEvents(builder.Configuration, allowLocalChannel: builder.HostEnvironment.IsDevelopment());
//#endif
//#if (IncludeSync)

// Sync and end to end encryption, and the Sync page: the settings from sync.json (embedded in this project), and the
// profiles (what is synced, and where it is kept on the device) from the SyncProfileDefinition classes in the app.
// Protected profiles the user creates may be unlocked with the passkey once the user turns that on for one on a device.
builder.Services.AddBlazwizSyncModule(builder.Configuration, sync => sync
    .UseEmbeddedConfiguration(typeof(Program).Assembly, "sync.json")
    .UseProfileDefaults(profile => profile.AllowDeviceUnlock = true)
    .AddProfilesFromAssemblyContaining<Program>()
    .AddProfilesFromAssemblyContaining<DashboardViewModel>());
#if (IncludeNotes)

// The notes example: markdown notes in the synced "notes" profile, on the Files page
builder.Services.AddNotes();
builder.Services.AddScoped<INoteEditorInteropService, NoteEditorInteropService>();
#endif
//#endif

// The app's own services and pages. Replace or turn off framework pages here too, e.g.
// builder.Services.ReplacePage<MyLogin>(BlazwizPages.Login) or builder.Services.DisablePage(BlazwizPages.Sync).
builder.Services.AddAppServices();
//#if (IncludeShowcase)
builder.Services.AddNavigationItem(new NavigationItem
{
    Text = "Showcase",
    Href = "showcase",
    Icon = "bi-palette",
    Description = "Themed building blocks to copy into new pages.",
    Order = 30,
});
//#endif

// The app's view models: all of them in the application assembly are registered automatically.
builder.Services.AddMvvm(options =>
{
    options.HostingModelType = BlazorHostingModelType.WebAssembly;
    options.RegisterViewModelsFromAssemblyContaining<DashboardViewModel>();
});

var host = builder.Build();

// The language the user chose in Settings (or the browser's), applied before the app starts. Add languages with
// Text:SupportedCultures in wwwroot/appsettings.json, and their text with resources (see the README).
await using (var scope = host.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<ICultureService>().ApplySavedCultureAsync();
}

await host.RunAsync();
