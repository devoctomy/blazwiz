using Blazing.Mvvm;
using blazwiz.Core.Extensions;
using blazwiz.Portal;
using blazwiz.Portal.Application.Extensions;
using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Application.ViewModels;
using blazwiz.Portal.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Settings from wwwroot/appsettings.json (+ appsettings.{Environment}.json)
builder.Services.AddPortalSettings(builder.Configuration);

// Core and portal application services
builder.Services.AddBlazwizCoreServices();
builder.Services.AddPortalApplicationServices();

// Browser specific implementations of application abstractions
builder.Services.AddScoped<INavigationService, NavigationService>();
builder.Services.AddScoped<IBrowserInteropService, BrowserInteropService>();

// MVVM - all view models in the application assembly are registered automatically
builder.Services.AddMvvm(options =>
{
    options.HostingModelType = BlazorHostingModelType.WebAssembly;
    options.RegisterViewModelsFromAssemblyContaining<MainLayoutViewModel>();
});

await builder.Build().RunAsync();
