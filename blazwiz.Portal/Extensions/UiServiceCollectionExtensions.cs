using blazwiz.Portal.Application.Extensions;
using blazwiz.Portal.Application.Shell;
using blazwiz.Portal.Components;
using blazwiz.Portal.Components.Settings;
using blazwiz.Portal.Models.Navigation;
using blazwiz.Portal.Views;
using Microsoft.Extensions.DependencyInjection;

namespace blazwiz.Portal.Extensions;

/// <summary>
/// Registers the framework's own UI: its pages, the Settings sections and shell components every app has, and the
/// Settings item in the sidebar.
/// </summary>
public static class UiServiceCollectionExtensions
{
    /// <summary>
    /// The Settings sections' positions, so modules and apps can place theirs around them.
    /// </summary>
    public static class SettingsOrder
    {
        /// <summary>
        /// The Appearance section (theme).
        /// </summary>
        public const int Appearance = 100;

        /// <summary>
        /// The Offline mode section.
        /// </summary>
        public const int Offline = 200;

        /// <summary>
        /// The About section (version and build), last.
        /// </summary>
        public const int About = 10_000;
    }

    /// <summary>
    /// Adds the framework's UI. Call after <c>AddPortalApplicationServices</c>; replace or turn off pages afterwards.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddBlazwizUi(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddBlazwizShell();
        services.ReplacePage<LoginView>(BlazwizPages.Login);
        services.ReplacePage<ProfileView>(BlazwizPages.Profile);
        services.ReplacePage<SettingsView>(BlazwizPages.Settings);

        services.AddShellComponent<OfflineBanner>(ShellRegion.Banners, order: 0);

        services.AddSettingsSection<AppearanceSettingsSection>(SettingsOrder.Appearance);
        services.AddSettingsSection<OfflineSettingsSection>(SettingsOrder.Offline);
        services.AddSettingsSection<AboutSettingsSection>(SettingsOrder.About);

        services.AddNavigationItem(new NavigationItem
        {
            Text = "Settings",
            Href = "settings",
            Icon = "bi-gear",
            Description = "Appearance and application preferences.",
            Order = 10_000,
            PageKey = BlazwizPages.Settings,
        });
        return services;
    }
}
