namespace blazwiz.Core.Sync.Models;

/// <summary>
/// How far a move of a profile's files from one storage to another has got. Recorded so a move cut short (e.g. by a
/// reload) can be finished or undone.
/// </summary>
public enum SyncStorageMigrationPhase
{
    /// <summary>
    /// The files are being copied. The source is still in use; undone by clearing the target.
    /// </summary>
    Copying,

    /// <summary>
    /// The files are copied and the target is in use; finished by clearing the source.
    /// </summary>
    Cleanup,
}
