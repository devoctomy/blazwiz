namespace blazwiz.Core.Sync.Models;

/// <summary>
/// Where a profile keeps its files on this device.
/// </summary>
public enum SyncStorageKind
{
    /// <summary>
    /// An IndexedDB object store.
    /// </summary>
    IndexedDb,

    /// <summary>
    /// The origin private file system (OPFS).
    /// </summary>
    Opfs,

    /// <summary>
    /// A folder the user picks on their computer (File System Access API; Chromium desktop only).
    /// </summary>
    LocalFolder,
}
