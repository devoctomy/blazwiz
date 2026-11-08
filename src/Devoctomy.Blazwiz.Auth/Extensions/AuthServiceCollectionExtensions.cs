using Devoctomy.Blazwiz.Auth.Migrations;
using Devoctomy.Blazwiz.Auth.Migrations.Interfaces;
using Devoctomy.Blazwiz.Auth.Services;
using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.Auth.Sessions;
using Devoctomy.Blazwiz.Auth.Sessions.Interfaces;
using Devoctomy.Blazwiz.Browser.Extensions;
using Devoctomy.Blazwiz.Core.Extensions;
using Devoctomy.Blazwiz.Cryptography.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Devoctomy.Blazwiz.Auth.Extensions;

/// <summary>
/// Extension methods for registering sign in and sessions in an <see cref="IServiceCollection"/>.
/// </summary>
public static class AuthServiceCollectionExtensions
{
    /// <summary>
    /// Adds sign in, sessions and their lifecycle, offline sign in and remembered passwords, and the core, cryptography
    /// and browser services they use. Signs in with the placeholder <see cref="LocalAuthenticationService"/> until an
    /// identity provider replaces <see cref="IAuthenticationService"/> (e.g. <c>AddBlazwizCognito</c>). Needs
    /// <c>AddBlazwizApp</c> first. Safe to call more than once.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddBlazwizAuth(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (services.Any(d => d.ServiceType == typeof(ISessionLifecycle)))
        {
            return services;
        }

        var app = services.GetBlazwizApp();
        services.AddBlazwizCoreServices();
        services.AddBlazwizCryptography();
        services.AddBlazwizBrowser();

        services.TryAddScoped<IPasswordCryptoService, PasswordCryptoService>();
        services.TryAddSingleton<IUserDisplayService, UserDisplayService>();
        services.TryAddScoped<IOfflineSessionStore, InMemoryOfflineSessionStore>();
        services.TryAddScoped<IAuthenticationService, LocalAuthenticationService>();
        services.TryAddScoped<IPasswordService, LocalPasswordService>();
        services.TryAddScoped<ISessionLifecycle, SessionLifecycle>();

        // Packages' stored data migrations, run at sign in before modules start.
        services.AddSessionLifecycleHandler<DataMigrationRunner>();

        // The keys derived from the passkey are wiped once signed out, after the modules have locked what they hold.
        services.AddSessionLifecycleHandler<DerivedKeySessionHandler>();

        // Keys derived from the WebAuthn PRF output, kept in memory; features get them from IDerivedKeyStore by name.
        services.AddBlazwizPrfKeyDerivation(prf => prf
            .UseApplication(app.Name, app.Id)
            .AddKey(PasswordCryptoService.RememberMeKeyName));
        return services;
    }

    /// <summary>
    /// Adds a migration of stored data, run at sign in (see <see cref="IDataMigration"/>).
    /// </summary>
    /// <typeparam name="TMigration">The migration.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddDataMigration<TMigration>(this IServiceCollection services)
        where TMigration : class, IDataMigration
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IDataMigration, TMigration>());
        return services;
    }

    /// <summary>
    /// Adds a handler called when sessions start and end.
    /// </summary>
    /// <typeparam name="THandler">The handler.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddSessionLifecycleHandler<THandler>(this IServiceCollection services)
        where THandler : class, ISessionLifecycleHandler
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ISessionLifecycleHandler, THandler>());
        return services;
    }
}
