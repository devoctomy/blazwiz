namespace MyApp.Portal.Models.Notes;

/// <summary>
/// A notebook: a sync profile with its own passphrase and keys, whose pages are the notes under its root. The notes
/// profile is the default notebook; the others are protected profiles the user creates.
/// </summary>
public class Notebook
{
    /// <summary>
    /// Gets or sets the sync profile's name: <c>notes</c> for the default notebook, otherwise its id.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the notebook's id (a GUID), which names its keys.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the key every page and folder of the notebook is under, e.g. <c>notes/</c>.
    /// </summary>
    public string Root { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name it is shown by: the default notebook's display name, or the name the user gave it once it
    /// is unlocked (its id while it is locked).
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this is the default notebook, set up in code.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether it has a passphrase yet.
    /// </summary>
    public bool HasPassphrase { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether it is unlocked.
    /// </summary>
    public bool IsUnlocked { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether, locked, it can be unlocked with biometrics on this device: turned on
    /// here when it was unlocked with its passphrase, and the signed in user has a passkey here.
    /// </summary>
    public bool CanUnlockWithBiometrics { get; set; }
}
