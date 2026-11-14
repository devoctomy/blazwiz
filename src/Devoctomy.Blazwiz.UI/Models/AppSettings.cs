namespace Devoctomy.Blazwiz.UI.Models;

/// <summary>
/// Application wide settings, bound from the <c>AppSettings</c> section of <c>wwwroot/appsettings.json</c>.
/// </summary>
public class AppSettings
{
    /// <summary>
    /// The logo used when <see cref="LogoUrl"/> is not set: the template's <c>wwwroot/images/icon.svg</c>.
    /// </summary>
    public const string DefaultLogoUrl = "images/icon.svg";

    /// <summary>
    /// Gets or sets the application title shown in the sidebar, login page and browser tab.
    /// </summary>
    public string ApplicationTitle { get; set; } = "blazwiz";

    /// <summary>
    /// Gets or sets a short description of the application, shown on the login page.
    /// </summary>
    public string ApplicationDescription { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the address of the application's logo, shown in the sidebar and on the sign-in pages: any image the
    /// browser shows (SVG, PNG, ...), relative to the app's base address. Defaults to <see cref="DefaultLogoUrl"/>.
    /// </summary>
    public string LogoUrl { get; set; } = DefaultLogoUrl;

    /// <summary>
    /// Gets or sets the copyright message.
    /// </summary>
    public string CopyrightMessage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether WebAuthn authentication is enabled for portal login.
    /// </summary>
    public bool EnablePortalLoginAuthn { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether ticking "Remember me" on the sign-in page offers an optional name to
    /// remember the user by, shown in the remembered users list instead of their email address.
    /// </summary>
    public bool EnableRememberMeName { get; set; }
}
