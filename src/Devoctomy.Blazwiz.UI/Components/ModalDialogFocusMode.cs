namespace Devoctomy.Blazwiz.UI.Components;

/// <summary>
/// What a <see cref="ModalDialog"/> focuses when it is shown.
/// </summary>
public enum ModalDialogFocusMode
{
    /// <summary>
    /// The first input, select or textarea, selecting an input's text; the dialog itself if it has none.
    /// </summary>
    FirstInput,

    /// <summary>
    /// The first button in the footer; the dialog itself if it has none.
    /// </summary>
    FirstButton,

    /// <summary>
    /// The dialog itself, so nothing is triggered by Enter and screen readers read its title.
    /// </summary>
    Dialog,

    /// <summary>
    /// The first element in the dialog marked <c>autofocus</c>; the dialog itself if there is none.
    /// </summary>
    Autofocus,

    /// <summary>
    /// Nothing: the app focuses what it wants. Escape only closes the dialog once focus is inside it.
    /// </summary>
    None,
}
