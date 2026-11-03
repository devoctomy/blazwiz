using Devoctomy.Blazwiz.Core.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Moq;

namespace Devoctomy.Blazwiz.TestSupport;

/// <summary>
/// A service collection set up as an app's is before it adds the framework: the app's identity, and the services the
/// Blazor host provides.
/// </summary>
public static class TestServices
{
    /// <summary>
    /// The test app's name.
    /// </summary>
    public const string AppName = "testapp";

    /// <summary>
    /// The test app's ID.
    /// </summary>
    public const string AppId = "3f1d2c4b-5a6e-4f70-8a9b-0c1d2e3f4a5b";

    /// <summary>
    /// Creates the service collection.
    /// </summary>
    /// <returns>The services.</returns>
    public static ServiceCollection Create()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<IJSRuntime>());
        services.AddSingleton(Mock.Of<NavigationManager>());
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddBlazwizApp(AppName, AppId);
        return services;
    }

    /// <summary>
    /// Builds a provider that checks every registration can be created, and scoped services are not taken from the
    /// root.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The provider.</returns>
    public static ServiceProvider BuildValidated(this IServiceCollection services) =>
        services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
}
