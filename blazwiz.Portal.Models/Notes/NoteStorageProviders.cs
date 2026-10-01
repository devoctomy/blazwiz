namespace blazwiz.Portal.Models.Notes;

/// <summary>
/// Names of the storage providers that can hold note content, recorded against each note in the file index.
/// </summary>
/// <remarks>
/// Only IndexedDB is implemented. Others, such as the Origin Private File System (see
/// <c>docs/wip/opfs-offline-storage.md</c>), can be added by implementing <c>INoteContentStore</c>.
/// </remarks>
public static class NoteStorageProviders
{
    /// <summary>
    /// Content stored in the browser's IndexedDB. The default.
    /// </summary>
    public const string IndexedDb = "indexeddb";
}
