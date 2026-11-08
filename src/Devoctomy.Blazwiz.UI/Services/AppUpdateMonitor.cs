using Devoctomy.Blazwiz.UI.Services.Interfaces;
using Microsoft.JSInterop;

namespace Devoctomy.Blazwiz.UI.Services;

/// <summary>
/// <see cref="IAppUpdateMonitor"/> over the <c>wwwroot/js/app-update.js</c> module shipped with this library.
/// </summary>
public sealed class AppUpdateMonitor(IJSRuntime jsRuntime)
    : IAppUpdateMonitor, IAsyncDisposable
{
    /// <summary>
    /// The path the module is imported from (Razor class library static assets).
    /// </summary>
    public const string ModulePath = "./_content/Devoctomy.Blazwiz.UI/js/app-update.js";

    private readonly IJSRuntime jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
    private IJSObjectReference? module;
    private DotNetObjectReference<AppUpdateMonitor>? selfReference;

    /// <inheritdoc/>
    public event EventHandler? Changed;

    /// <inheritdoc/>
    public bool IsDownloading { get; private set; }

    /// <inheritdoc/>
    public double? Progress { get; private set; }

    /// <inheritdoc/>
    public async Task StartAsync()
    {
        if (selfReference != null)
        {
            return;
        }

        var js = await GetModuleAsync();
        selfReference = DotNetObjectReference.Create(this);
        await js.InvokeVoidAsync("watch", selfReference);
    }

    /// <summary>
    /// Called from <c>app-update.js</c> while a new version downloads.
    /// </summary>
    /// <param name="done">Files cached so far.</param>
    /// <param name="total">Files to cache, or 0 when the service worker does not report progress.</param>
    [JSInvokable]
    public void OnDownloading(int done, int total)
    {
        IsDownloading = true;
        Progress = total > 0 ? Math.Clamp((double)done / total, 0, 1) : null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Called from <c>app-update.js</c> when the download has finished, or failed.
    /// </summary>
    [JSInvokable]
    public void OnFinished()
    {
        if (!IsDownloading)
        {
            return;
        }

        IsDownloading = false;
        Progress = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        try
        {
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
        selfReference = null;
    }

    private async Task<IJSObjectReference> GetModuleAsync()
    {
        return module ??= await jsRuntime.InvokeAsync<IJSObjectReference>("import", ModulePath);
    }
}
