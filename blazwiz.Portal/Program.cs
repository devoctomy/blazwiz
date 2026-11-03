using Blazing.Mvvm;
using blazwiz.Core.Extensions;
using blazwiz.Portal;
using blazwiz.Portal.Application.Extensions;
using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Application.ViewModels;
using blazwiz.Portal.Extensions;
using blazwiz.Portal.Models.Navigation;
using blazwiz.Portal.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Settings from wwwroot/appsettings.json (+ appsettings.{Environment}.json)
builder.Services.AddPortalSettings(builder.Configuration);

// The framework: core services, sign in, theme and settings, and its own pages and shell
builder.Services.AddBlazwizCoreServices();
builder.Services.AddPortalApplicationServices();
builder.Services.AddBlazwizUi();
//#if (IncludeNotes)

// Sync and end to end encryption, with the notes example as the synced profile
builder.Services.AddBlazwizSyncModule(sync => sync
    .UseIndexedDbEventLog(database: "sync", store: "events")
    .UseEventLogRetention(maxAge: TimeSpan.FromDays(30), maxEventsPerUser: 5000)
    .UseChangeDebounce(TimeSpan.FromSeconds(2))
    .AddNotesProfile());
builder.Services.AddBlazwizSyncUi();
builder.Services.AddNotes();
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

// Browser specific implementations of application abstractions
builder.Services.AddScoped<INavigationService, NavigationService>();
builder.Services.AddScoped<IBrowserInteropService, BrowserInteropService>();
builder.Services.AddScoped<IWebAuthnService, WebAuthnService>();
//#if (IncludeNotes)
builder.Services.AddScoped<INoteEditorInteropService, NoteEditorInteropService>();
//#endif

// MVVM - all view models in the application assembly are registered automatically
builder.Services.AddMvvm(options =>
{
    options.HostingModelType = BlazorHostingModelType.WebAssembly;
    options.RegisterViewModelsFromAssemblyContaining<MainLayoutViewModel>();
});

await builder.Build().RunAsync();
