namespace Devoctomy.Blazwiz.Browser.Models;

/// <summary>
/// The platform and browser the app runs in, as far as <c>js/device.js</c> can tell. Used for the default device
/// name only: nothing relies on it being right.
/// </summary>
public sealed class DeviceDescription
{
    /// <summary>
    /// Gets or sets the platform, e.g. <c>Android</c>, <c>iOS</c> or <c>Windows</c>, or null when not recognised.
    /// </summary>
    public string? Platform { get; set; }

    /// <summary>
    /// Gets or sets the browser, e.g. <c>Edge</c>, <c>Chrome</c> or <c>Firefox</c>, or null when not recognised.
    /// </summary>
    public string? Browser { get; set; }
}
