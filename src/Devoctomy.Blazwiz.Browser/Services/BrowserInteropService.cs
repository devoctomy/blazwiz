using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Devoctomy.Blazwiz.Browser.Services;

/// <summary>
/// <see cref="IBrowserInteropService"/> over the <c>wwwroot/js/browser.js</c> module shipped with this library.
/// </summary>
public sealed class BrowserInteropService(IJSRuntime jsRuntime)
    : IBrowserInteropService
{
    /// <summary>
    /// The path the module is imported from (Razor class library static assets).
    /// </summary>
    public const string ModulePath = "./_content/Devoctomy.Blazwiz.Browser/js/browser.js";

    private readonly IJSRuntime jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
    private IJSObjectReference? module;

    /// <inheritdoc/>
    public async Task SetThemeAsync(string theme)
    {
        var js = await GetModuleAsync();
        await js.InvokeVoidAsync("setTheme", theme);
    }

    /// <inheritdoc/>
    public async Task<string> GetPreferredThemeAsync()
    {
        var js = await GetModuleAsync();
        return await js.InvokeAsync<string>("getPreferredTheme");
    }

    /// <inheritdoc/>
    public async Task SetDocumentLanguageAsync(string language)
    {
        var js = await GetModuleAsync();
        await js.InvokeVoidAsync("setLanguage", language);
    }

    /// <inheritdoc/>
    public async Task FocusFirstInputAsync(ElementReference element)
    {
        var js = await GetModuleAsync();
        await js.InvokeVoidAsync("focusFirstInput", element);
    }

    private async Task<IJSObjectReference> GetModuleAsync()
    {
        return module ??= await jsRuntime.InvokeAsync<IJSObjectReference>("import", ModulePath);
    }
}
