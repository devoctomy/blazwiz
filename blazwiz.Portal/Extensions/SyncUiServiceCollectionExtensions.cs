using blazwiz.Portal.Application.Extensions;
using blazwiz.Portal.Application.Services;
using blazwiz.Portal.Application.Shell;
using blazwiz.Portal.Components;
using blazwiz.Portal.Components.Sync;
using blazwiz.Portal.Models.Navigation;
using blazwiz.Portal.Views;
using Microsoft.Extensions.DependencyInjection;

namespace blazwiz.Portal.Extensions;

/// <summary>
/// Registers the sync module's UI: the Sync page, the status pill, banners and overlay, its Settings sections and its
/// sign-in option.
/// </summary>
public static class SyncUiServiceCollectionExtensions
{
    /// <summary>
    /// Adds the sync module's UI. Needs the sync module (<c>AddBlazwizSyncModule</c>).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddBlazwizSyncUi(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.ReplacePage<SyncView>(BlazwizPages.Sync);

        services.AddShellComponent<E2eBanner>(ShellRegion.Banners, order: 10);
        services.AddShellComponent<FolderAccessBanner>(ShellRegion.Banners, order: 20);
        services.AddShellComponent<SyncPill>(ShellRegion.Floating, order: 0);
        services.AddShellComponent<StorageMigrationOverlay>(ShellRegion.Overlays, order: 0);

        services.AddSettingsSection<SyncSettingsSection>(UiServiceCollectionExtensions.SettingsOrder.Offline + 100);
        services.AddSettingsSection<Argon2idPanel>(UiServiceCollectionExtensions.SettingsOrder.Offline + 200);

        services.AddSignInOption<StartSyncOffSignInOption, StartSyncOffOption>();

        services.AddNavigationItem(new NavigationItem
        {
            Text = "Sync",
            Href = "sync",
            Icon = "bi-arrow-repeat",
            Description = "What sync has done, connectivity and offline mode.",
            Order = 20,
            PageKey = BlazwizPages.Sync,
        });
        return services;
    }
}
