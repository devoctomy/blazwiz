namespace Devoctomy.Blazwiz.Auth.Models;

/// <summary>
/// A device the user's sign ins skip the code from the authenticator app on.
/// </summary>
public class RememberedDevice
{
    /// <summary>
    /// Gets or sets the sign-in service's key for the device.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name it was remembered under, or null when it has none.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets when it last signed in, if known.
    /// </summary>
    public DateTimeOffset? LastSignedInAt { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this is the device the current session signed in from.
    /// </summary>
    public bool IsThisDevice { get; set; }
}
