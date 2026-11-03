using Devoctomy.Blazwiz.Sync.Models;

namespace Devoctomy.Blazwiz.Sync.Interfaces;

/// <summary>
/// Moves the signed in user's files from one storage to another on this device, with sync paused while they move.
/// </summary>
public interface ISyncStorageMigrationService
{
    /// <summary>
    /// Moves every profile that has the target storage to it. Sync is stopped, each profile's files are copied and
    /// checked under its sync lock, the target is selected, the source is cleared, and sync is started again.
    /// </summary>
    /// <param name="target">The storage to move to.</param>
    /// <param name="progress">Told about each file copied.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="Exceptions.SyncStorageMigrationException">A file did not copy; the source stays in use.</exception>
    Task MigrateAsync(SyncStorageKind target, IProgress<SyncStorageMigrationProgress>? progress = null, CancellationToken ct = default);

    /// <summary>
    /// Finishes or undoes a move that was cut short (e.g. by a reload). Call before starting sync.
    /// </summary>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task RecoverAsync(CancellationToken ct = default);
}
