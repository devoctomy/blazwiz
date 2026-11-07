using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Microsoft.JSInterop;

namespace Devoctomy.Blazwiz.Browser.Services;

/// <summary>
/// Implementation of <see cref="IClipboardService"/> through the browser's Clipboard API (<c>js/clipboard.js</c>).
/// </summary>
/// <param name="jsRuntime">Calls the module.</param>
/// <param name="timeProvider">Times the clear.</param>
public sealed class ClipboardService(IJSRuntime jsRuntime, TimeProvider timeProvider)
    : IClipboardService, IDisposable
{
    /// <summary>
    /// The module's path.
    /// </summary>
    public const string ModulePath = "./_content/Devoctomy.Blazwiz.Browser/js/clipboard.js";

    private readonly IJSRuntime jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    private IJSObjectReference? module;
    private CancellationTokenSource? pendingClear;

    /// <inheritdoc/>
    public async Task CopyAsync(string text, TimeSpan? clearAfter = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (clearAfter is { } delay && delay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(clearAfter), "The time until the clipboard is cleared must be positive.");
        }

        // A new copy replaces whatever was waiting to be cleared.
        CancelPendingClear();
        var js = await GetModuleAsync();
        await js.InvokeVoidAsync("copy", text);
        if (clearAfter is { } after)
        {
            var source = new CancellationTokenSource();
            pendingClear = source;
            _ = ClearLaterAsync(js, text, after, source);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        CancelPendingClear();
    }

    private async Task ClearLaterAsync(IJSObjectReference js, string text, TimeSpan after, CancellationTokenSource source)
    {
        try
        {
            await Task.Delay(after, timeProvider, source.Token);
            await js.InvokeAsync<bool>("clearIfUnchanged", source.Token, text);
        }
        catch (OperationCanceledException)
        {
            // Replaced by a later copy, or the app is closing.
        }
        catch (JSException)
        {
            // The browser refused: nothing more can be done from here.
        }
        finally
        {
            if (pendingClear == source)
            {
                pendingClear = null;
            }

            source.Dispose();
        }
    }

    private void CancelPendingClear()
    {
        var source = pendingClear;
        pendingClear = null;
        source?.Cancel();
    }

    private async Task<IJSObjectReference> GetModuleAsync()
    {
        return module ??= await jsRuntime.InvokeAsync<IJSObjectReference>("import", ModulePath);
    }
}
