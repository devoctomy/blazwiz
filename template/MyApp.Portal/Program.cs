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
//#if (IncludeSync)
using Devoctomy.Blazwiz.UI.Sync.Extensions;
//#endif
#if (IncludeNotes)
using MyApp.Portal.Application.Services.Interfaces;
using MyApp.Portal.Services;
#elif (IncludeSync)
using Devoctomy.Blazwiz.Sync.Models;
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
builder.Services.AddBlazwizCognito(builder.Configuration);
//#if (IncludeSync)

// Sync and end to end encryption: what is synced (profiles), where it is kept on the device, and the Sync page
builder.Services.AddBlazwizSyncModule(builder.Configuration, sync => sync
    .UseIndexedDbEventLog(database: "sync", store: "events")
    .UseEventLogRetention(maxAge: TimeSpan.FromDays(30), maxEventsPerUser: 5000)
    .UseChangeDebounce(TimeSpan.FromSeconds(2))
#if (IncludeNotes)
    .AddNotesProfile());

// The notes example: markdown notes in the synced "notes" profile, on the Files page
builder.Services.AddNotes();
builder.Services.AddScoped<INoteEditorInteropService, NoteEditorInteropService>();
#else
    .AddProfile("files", profile => profile
        .UseIndexedDbIndexStore(database: "sync", store: "files_index")
        .UseFileStores(stores => stores
            .AddIndexedDb(database: "files", store: "files")
            .AddOpfs(root: "files")
            .AddLocalFolder()
            .UseDefault(SyncStorageKind.IndexedDb))
        .UseRemote("files/**/*")
        .UseEncryption()));
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
