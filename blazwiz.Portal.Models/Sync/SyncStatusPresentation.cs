namespace blazwiz.Portal.Models.Sync;

/// <summary>
/// The sync and connectivity state as shown by the status pill, the Settings Sync card and the Sync page.
/// </summary>
public class SyncStatusPresentation
{
    /// <summary>
    /// Gets or sets the short text, e.g. "Up to date", "Syncing", "3 pending", "Offline" or "Sync error".
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the longer description for the tooltip, e.g. "Offline: S3 unreachable".
    /// </summary>
    public string Tooltip { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Bootstrap Icons class.
    /// </summary>
    public string Icon { get; set; } = "bi-cloud";

    /// <summary>
    /// Gets or sets the tone, used as a CSS modifier: <c>success</c>, <c>info</c>, <c>warning</c>, <c>danger</c> or <c>secondary</c>.
    /// </summary>
    public string Tone { get; set; } = "secondary";

    /// <summary>
    /// Gets or sets a value indicating whether a pass is running.
    /// </summary>
    public bool IsSyncing { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the app is offline.
    /// </summary>
    public bool IsOffline { get; set; }
}
