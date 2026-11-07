using Devoctomy.Blazwiz.UI.Models;
using Devoctomy.Blazwiz.UI.Services.Interfaces;

namespace Devoctomy.Blazwiz.UI.Services;

/// <summary>
/// Implementation of <see cref="IToastService"/>: each toast goes after its duration.
/// </summary>
/// <param name="timeProvider">Times the toasts.</param>
public sealed class ToastService(TimeProvider timeProvider)
    : IToastService, IDisposable
{
    /// <summary>
    /// The most toasts shown at once.
    /// </summary>
    public const int MaximumToasts = 3;

    /// <summary>
    /// How long a toast is shown, unless it says otherwise.
    /// </summary>
    public static readonly TimeSpan DefaultDuration = TimeSpan.FromSeconds(4);

    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    private readonly List<Toast> toasts = [];
    private readonly CancellationTokenSource lifetime = new CancellationTokenSource();

    /// <inheritdoc/>
    public event EventHandler? Changed;

    /// <inheritdoc/>
    public IReadOnlyList<Toast> Toasts => toasts.ToList();

    /// <inheritdoc/>
    public Toast Show(string message, ToastTone tone = ToastTone.Info, TimeSpan? duration = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        var shownFor = duration ?? DefaultDuration;
        if (shownFor <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "A toast must be shown for some time.");
        }

        var toast = new Toast { Message = message, Tone = tone };
        toasts.Add(toast);
        while (toasts.Count > MaximumToasts)
        {
            toasts.RemoveAt(0);
        }

        Changed?.Invoke(this, EventArgs.Empty);
        _ = DismissLaterAsync(toast.Id, shownFor);
        return toast;
    }

    /// <inheritdoc/>
    public void Dismiss(Guid id)
    {
        if (toasts.RemoveAll(t => t.Id == id) > 0)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lifetime.Cancel();
        lifetime.Dispose();
    }

    private async Task DismissLaterAsync(Guid id, TimeSpan after)
    {
        try
        {
            await Task.Delay(after, timeProvider, lifetime.Token);
            Dismiss(id);
        }
        catch (OperationCanceledException)
        {
            // The app is closing.
        }
    }
}
