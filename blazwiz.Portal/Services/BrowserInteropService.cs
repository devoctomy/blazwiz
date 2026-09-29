using blazwiz.Portal.Application.Services.Interfaces;
using Microsoft.JSInterop;

namespace blazwiz.Portal.Services;

/// <summary>
/// Implements <see cref="IBrowserInteropService"/> using the functions in <c>wwwroot/js/app.js</c>.
/// </summary>
public class BrowserInteropService(IJSRuntime jsRuntime) : IBrowserInteropService
{
    private readonly IJSRuntime jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));

    /// <inheritdoc/>
    public async Task SetThemeAsync(string theme)
    {
        await jsRuntime.InvokeVoidAsync("portalInterop.setTheme", theme);
    }

    /// <inheritdoc/>
    public async Task<string> GetPreferredThemeAsync()
    {
        return await jsRuntime.InvokeAsync<string>("portalInterop.getPreferredTheme");
    }
}
