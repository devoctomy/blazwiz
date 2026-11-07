namespace Devoctomy.Blazwiz.UI.Models;

/// <summary>
/// What kind of message a toast gives, which sets its colour and icon.
/// </summary>
public enum ToastTone
{
    /// <summary>
    /// Information.
    /// </summary>
    Info,

    /// <summary>
    /// Something worked.
    /// </summary>
    Success,

    /// <summary>
    /// Something to be careful of.
    /// </summary>
    Warning,

    /// <summary>
    /// Something failed.
    /// </summary>
    Danger,
}
