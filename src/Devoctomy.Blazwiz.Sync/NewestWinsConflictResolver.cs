using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;

namespace Devoctomy.Blazwiz.Sync;

/// <summary>
/// The default conflict policy: the newest version of the whole file wins, by edit time. The remote edit time is the
/// <c>x-amz-meta-modified-at</c> metadata, falling back to S3's upload time when it is missing. A local edit always
/// beats a remote delete, because S3 does not say when the delete happened. Ties go to the remote version.
/// </summary>
public class NewestWinsConflictResolver : ISyncConflictResolver
{
    /// <inheritdoc/>
    public SyncConflictWinner Resolve(SyncConflict conflict)
    {
        ArgumentNullException.ThrowIfNull(conflict);

        if (conflict.Kind == SyncConflictKind.EditDelete || conflict.Remote == null)
        {
            return SyncConflictWinner.Local;
        }

        if (conflict.LocalChangedAt == null)
        {
            return SyncConflictWinner.Remote;
        }

        return conflict.LocalChangedAt.Value > conflict.Remote.EditedAt
            ? SyncConflictWinner.Local
            : SyncConflictWinner.Remote;
    }
}
