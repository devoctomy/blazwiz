namespace Devoctomy.Blazwiz.UI.Sync.Models;

/// <summary>
/// A profile as the end to end encryption settings list it.
/// </summary>
public class E2eProfileRow
{
    /// <summary>
    /// Gets or sets the sync profile's name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the profile's id (a GUID).
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name it is shown by: its display name, or its id for a profile the user created.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets where it stands on this device.
    /// </summary>
    public E2eProfileState State { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user created it, rather than it being set up in code.
    /// </summary>
    public bool IsCreatedByUser { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether its settings allow unlocking it with the passkey.
    /// </summary>
    public bool AllowDeviceUnlock { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this device can unlock it with the passkey.
    /// </summary>
    public bool CanUnlockWithDevice { get; set; }
}
