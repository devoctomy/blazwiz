using System.Text.Json.Serialization;

namespace blazwiz.Core.Sync.Models;

/// <summary>
/// The storage a user's profile uses on this device, and any move to another storage in progress.
/// </summary>
public class SyncStorageState
{
    /// <summary>
    /// Gets or sets the profile.
    /// </summary>
    public string Profile { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user id.
    /// </summary>
    public string Owner { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the storage in use.
    /// </summary>
    public SyncStorageKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the storage files are being moved from, or null when no move is in progress.
    /// </summary>
    public SyncStorageKind? MigrationFrom { get; set; }

    /// <summary>
    /// Gets or sets the storage files are being moved to, or null when no move is in progress.
    /// </summary>
    public SyncStorageKind? MigrationTo { get; set; }

    /// <summary>
    /// Gets or sets how far the move has got, or null when no move is in progress.
    /// </summary>
    public SyncStorageMigrationPhase? MigrationPhase { get; set; }

    /// <summary>
    /// Gets a value indicating whether files are being copied, so changes must wait.
    /// </summary>
    [JsonIgnore]
    public bool IsCopying => MigrationPhase == SyncStorageMigrationPhase.Copying;
}
