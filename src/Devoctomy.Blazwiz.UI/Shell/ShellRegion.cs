namespace Devoctomy.Blazwiz.UI.Shell;

/// <summary>
/// The places in the app shell (the main layout) where modules and the app add their own components.
/// </summary>
public enum ShellRegion
{
    /// <summary>
    /// Above the page content: banners and alerts that apply to the whole app.
    /// </summary>
    Banners,

    /// <summary>
    /// In the top bar, before the theme toggle: small icon buttons.
    /// </summary>
    TopBarActions,

    /// <summary>
    /// Floating over the content, in the bottom corner: status pills and their popovers.
    /// </summary>
    Floating,

    /// <summary>
    /// Over the whole shell: blocking overlays and dialogs.
    /// </summary>
    Overlays,
}
