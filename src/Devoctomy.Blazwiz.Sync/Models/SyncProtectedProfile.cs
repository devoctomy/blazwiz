using Devoctomy.Blazwiz.Encryption.Models;

namespace Devoctomy.Blazwiz.Sync.Models;

/// <summary>
/// A sync profile with its own keys and passphrase, as the user's profiles are listed: one set up in code (such as
/// notes) or one the user created.
/// </summary>
public class SyncProtectedProfile
{
    /// <summary>
    /// Gets or sets the sync profile's name; for a profile the user created, its id.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the profile's key scope, named by its id.
    /// </summary>
    public E2eKeyScope Scope { get; set; } = null!;

    /// <summary>
    /// Gets or sets a value indicating whether the user created the profile, rather than it being set up in code.
    /// </summary>
    public bool IsCreatedByUser { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the profile has its keys: a profile set up in code has none until the
    /// user sets its passphrase.
    /// </summary>
    public bool IsSetUp { get; set; }

    /// <summary>
    /// Gets or sets the profile's security settings.
    /// </summary>
    public E2eProfileSettings Settings { get; set; } = new E2eProfileSettings();
}
