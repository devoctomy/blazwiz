using Devoctomy.Blazwiz.UI.Models;

namespace Devoctomy.Blazwiz.UI.Services.Interfaces;

/// <summary>
/// Shows toasts: short messages over the page that go away by themselves. The shell shows them (<c>ToastHost</c>).
/// </summary>
public interface IToastService
{
    /// <summary>
    /// Raised when a toast is shown or goes away.
    /// </summary>
    event EventHandler? Changed;

    /// <summary>
    /// Gets the toasts being shown, oldest first.
    /// </summary>
    IReadOnlyList<Toast> Toasts { get; }

    /// <summary>
    /// Shows a toast. If too many are shown, the oldest goes.
    /// </summary>
    /// <param name="message">The message: text the app has already localised.</param>
    /// <param name="tone">What kind of message it is.</param>
    /// <param name="duration">How long it is shown, or null for the default.</param>
    /// <returns>The toast.</returns>
    Toast Show(string message, ToastTone tone = ToastTone.Info, TimeSpan? duration = null);

    /// <summary>
    /// Takes a toast away before its time.
    /// </summary>
    /// <param name="id">The toast's id.</param>
    void Dismiss(Guid id);
}
