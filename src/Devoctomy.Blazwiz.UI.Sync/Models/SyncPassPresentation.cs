namespace Devoctomy.Blazwiz.UI.Sync.Models;

/// <summary>
/// A running pass as the status pill shows it, while more than one runs at once: one pill for each profile, so their
/// progress is shown apart.
/// </summary>
public class SyncPassPresentation
{
    /// <summary>
    /// Gets or sets the profile's name.
    /// </summary>
    public string Profile { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name the profile is shown by.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the progress, naming the profile, e.g. "Syncing Work 12 of 201", or "Syncing Work" until the pass
    /// has planned its changes.
    /// </summary>
    public string Text { get; set; } = string.Empty;
}
