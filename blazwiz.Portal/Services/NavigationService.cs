using blazwiz.Portal.Application.Services.Interfaces;
using Microsoft.AspNetCore.Components;

namespace blazwiz.Portal.Services;

/// <inheritdoc/>
public class NavigationService(NavigationManager navigationManager) : INavigationService
{
    private readonly NavigationManager navigationManager = navigationManager ?? throw new ArgumentNullException(nameof(navigationManager));

    /// <inheritdoc/>
    public void NavigateTo(string uri)
    {
        navigationManager.NavigateTo(uri);
    }

    /// <inheritdoc/>
    public void Reload()
    {
        navigationManager.Refresh(forceReload: true);
    }
}
