namespace blazwiz.Portal.Models.Settings;

/// <summary>
/// Application wide settings, bound from the <c>AppSettings</c> section of <c>wwwroot/appsettings.json</c>.
/// </summary>
public class AppSettings
{
    /// <summary>
    /// Gets or sets the application title shown in the sidebar, login page and browser tab.
    /// </summary>
    public string ApplicationTitle { get; set; } = "blazwiz";

    /// <summary>
    /// Gets or sets a short description of the application, shown on the login page.
    /// </summary>
    public string ApplicationDescription { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the copyright message.
    /// </summary>
    public string CopyrightMessage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether WebAuthn authentication is enabled for portal login.
    /// </summary>
    public bool EnablePortalLoginAuthn { get; set; }
}
