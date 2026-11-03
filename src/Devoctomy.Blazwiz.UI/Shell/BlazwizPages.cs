namespace Devoctomy.Blazwiz.UI.Shell;

/// <summary>
/// The framework's pages that an app can replace or turn off, keeping their routes (see
/// <see cref="Extensions.ShellServiceCollectionExtensions.ReplacePage{TComponent}"/> and
/// <see cref="Extensions.ShellServiceCollectionExtensions.DisablePage"/>).
/// </summary>
public static class BlazwizPages
{
    /// <summary>
    /// The sign-in page, <c>/login</c>.
    /// </summary>
    public const string Login = "login";

    /// <summary>
    /// The page for resetting a forgotten password, <c>/forgot-password</c>. Turning it off also hides the link to it
    /// on the sign-in page.
    /// </summary>
    public const string ForgotPassword = "forgot-password";

    /// <summary>
    /// The signed-in user's profile, <c>/profile</c>.
    /// </summary>
    public const string Profile = "profile";

    /// <summary>
    /// The settings page, <c>/settings</c>.
    /// </summary>
    public const string Settings = "settings";

    /// <summary>
    /// The sync log and status, <c>/sync</c>.
    /// </summary>
    public const string Sync = "sync";
}
