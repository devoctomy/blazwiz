using Blazing.Mvvm;
using Devoctomy.Blazwiz.Auth.Extensions;
using Devoctomy.Blazwiz.Core.Extensions;
using Devoctomy.Blazwiz.UI.Components;
using Devoctomy.Blazwiz.UI.Components.Settings;
using Devoctomy.Blazwiz.UI.Models;
using Devoctomy.Blazwiz.UI.Services;
using Devoctomy.Blazwiz.UI.Services.Interfaces;
using Devoctomy.Blazwiz.UI.Shell;
using Devoctomy.Blazwiz.UI.Text;
using Devoctomy.Blazwiz.UI.ViewModels;
using Devoctomy.Blazwiz.UI.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Devoctomy.Blazwiz.UI.Extensions;

/// <summary>
/// Registers the app shell: sign in, theme and settings, the framework's pages, Settings sections and sidebar items,
/// and the extension points modules and the app add to.
/// </summary>
public static class UiServiceCollectionExtensions
{
    /// <summary>
    /// The configuration section holding the app's settings (<see cref="AppSettings"/>).
    /// </summary>
    public const string ConfigurationSection = "AppSettings";

    /// <summary>
    /// The setting that pseudo-localises every text (see <c>TextBuilder.UsePseudoLocalization</c>). For testing only.
    /// </summary>
    public const string PseudoLocalizationSetting = "Text:PseudoLocalization";

    /// <summary>
    /// Adds the app shell and everything it uses (core, cryptography, browser and sign in). Needs
    /// <c>AddBlazwizApp</c> first. Add modules and replace or turn off pages afterwards.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The app's configuration.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddBlazwizUi(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddBlazwizAuth();

        var appSettings = new AppSettings();
        configuration.GetSection(ConfigurationSection).Bind(appSettings);
        services.AddSingleton(appSettings);

        // The extension points modules and the app add to: shell regions, Settings sections, sidebar, pages.
        services.AddBlazwizShell();

        // Text from resources the app can override and translate, and error descriptions. Text:PseudoLocalization
        // (testing only) marks every text from resources, to find text that is not.
        services.AddBlazwizUiText();
        if (configuration.GetValue<bool>(PseudoLocalizationSetting))
        {
            services.AddBlazwizText(text => text.UsePseudoLocalization());
        }

        services.TryAddScoped<IUserSettingsService, UserSettingsService>();
        services.TryAddScoped<IThemeService, ThemeService>();
        services.TryAddSingleton<IAppBuildInfo, AppBuildInfo>();

        // The framework's view models. The app registers its own with AddMvvm too.
        services.AddMvvm(options =>
        {
            options.HostingModelType = BlazorHostingModelType.WebAssembly;
            options.RegisterViewModelsFromAssemblyContaining<MainLayoutViewModel>();
        });

        services.ReplacePage<LoginView>(BlazwizPages.Login);
        services.ReplacePage<ProfileView>(BlazwizPages.Profile);
        services.ReplacePage<SettingsView>(BlazwizPages.Settings);

        services.AddShellComponent<OfflineBanner>(ShellRegion.Banners, order: 0);

        services.AddSettingsSection<AppearanceSettingsSection>(SettingsOrder.Appearance);
        services.AddSettingsSection<OfflineSettingsSection>(SettingsOrder.Offline);
        services.AddSettingsSection<AboutSettingsSection>(SettingsOrder.About);

        services.AddNavigationItem(new NavigationItem
        {
            Text = "Nav.Settings",
            Description = "Nav.Settings.Description",
            TextResource = typeof(UiText),
            Href = "settings",
            Icon = "bi-gear",
            Order = 10_000,
            PageKey = BlazwizPages.Settings,
        });
        return services;
    }

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
}
