namespace Devoctomy.Blazwiz.UI.Models;

/// <summary>
/// A short message shown over the page for a few seconds, e.g. "Password copied".
/// </summary>
public class Toast
{
    /// <summary>
    /// Gets the toast's id.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets the message: text the app has already localised.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Gets what kind of message it is.
    /// </summary>
    public ToastTone Tone { get; init; }

    /// <summary>
    /// Gets the toast's icon: a Bootstrap icon class, from its tone.
    /// </summary>
    public string Icon => Tone switch
    {
        ToastTone.Success => "bi-check-circle-fill",
        ToastTone.Warning => "bi-exclamation-triangle-fill",
        ToastTone.Danger => "bi-x-circle-fill",
        _ => "bi-info-circle-fill",
    };
}
