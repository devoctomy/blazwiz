using Devoctomy.Blazwiz.Sync.Models;

namespace Devoctomy.Blazwiz.Sync.Interfaces;

/// <summary>
/// Decides which side wins when a file changed on both sides since the last sync.
/// </summary>
public interface ISyncConflictResolver
{
    /// <summary>
    /// Picks the winner.
    /// </summary>
    /// <param name="conflict">The conflict.</param>
    /// <returns>The side whose version is kept.</returns>
    SyncConflictWinner Resolve(SyncConflict conflict);
}
