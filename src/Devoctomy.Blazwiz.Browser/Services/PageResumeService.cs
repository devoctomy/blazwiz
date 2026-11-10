using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Microsoft.JSInterop;

namespace Devoctomy.Blazwiz.Browser.Services;

/// <summary>
/// <see cref="IPageResumeService"/> over the <c>visibilitychange</c> and <c>online</c> events, through
/// <c>js/page-resume.js</c>.
/// </summary>
public sealed class PageResumeService(IJSRuntime jsRuntime)
    : IPageResumeService, IAsyncDisposable, IDisposable
{
    /// <summary>
    /// The module's path.
    /// </summary>
    public const string ModulePath = "./_content/Devoctomy.Blazwiz.Browser/js/page-resume.js";

    private readonly IJSRuntime jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
    private IJSObjectReference? module;
    private DotNetObjectReference<PageResumeService>? selfReference;

    /// <inheritdoc/>
    public event EventHandler? Resumed;

    /// <inheritdoc/>
    public async Task StartAsync()
    {
        if (selfReference != null)
        {
            return;
        }

        var js = await GetModuleAsync();
        selfReference = DotNetObjectReference.Create(this);
        await js.InvokeVoidAsync("subscribe", selfReference);
    }

    /// <inheritdoc/>
    public async Task StopAsync()
    {
        if (selfReference == null)
        {
            return;
        }

        var js = await GetModuleAsync();
        await js.InvokeVoidAsync("unsubscribe");
        selfReference.Dispose();
        selfReference = null;
    }

    /// <summary>
    /// Called from <c>page-resume.js</c> when the page becomes visible again or the browser comes back online.
    /// </summary>
    [JSInvokable]
    public void OnResumed()
    {
        Resumed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await StopAsync();
            if (module != null)
            {
                await module.DisposeAsync();
            }
        }
        catch (JSDisconnectedException)
        {
            // The page is going away.
        }

        selfReference?.Dispose();
    }

    /// <summary>
    /// Stops .NET being called back. The browser's listeners go with the page; prefer <see cref="DisposeAsync"/>,
    /// which removes them too.
    /// </summary>
    public void Dispose()
    {
        selfReference?.Dispose();
        selfReference = null;
    }

    private async Task<IJSObjectReference> GetModuleAsync()
    {
        return module ??= await jsRuntime.InvokeAsync<IJSObjectReference>("import", ModulePath);
    }
}
