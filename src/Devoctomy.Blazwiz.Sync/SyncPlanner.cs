using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;

namespace Devoctomy.Blazwiz.Sync;

/// <summary>
/// The sync decision table. The index says which side changed since the last sync: the ETag for the remote side,
/// and the dirty flag (or, on a verify pass, the content hash) for the local side.
/// </summary>
public class SyncPlanner : ISyncPlanner
{
    /// <inheritdoc/>
    public SyncPlan Plan(
        IEnumerable<SyncFileInfo> local,
        IEnumerable<RemoteObjectInfo> remote,
        IEnumerable<SyncIndexRecord> index,
        IReadOnlyDictionary<string, string>? localHashes)
    {
        ArgumentNullException.ThrowIfNull(local);
        ArgumentNullException.ThrowIfNull(remote);
        ArgumentNullException.ThrowIfNull(index);

        var localByKey = local.ToDictionary(f => f.Key, StringComparer.Ordinal);
        var remoteByKey = remote.ToDictionary(o => o.Key, StringComparer.Ordinal);
        var indexByKey = index.ToDictionary(r => r.Key, StringComparer.Ordinal);
        var keys = localByKey.Keys.Concat(remoteByKey.Keys).Concat(indexByKey.Keys)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

        var actions = keys.Select(key => PlanItem(new SyncPlanItem
        {
            Key = key,
            Local = localByKey.GetValueOrDefault(key),
            Remote = remoteByKey.GetValueOrDefault(key),
            Record = indexByKey.GetValueOrDefault(key),
            LocalHash = localHashes?.GetValueOrDefault(key),
        })).ToList();

        return new SyncPlan { Actions = actions };
    }

    /// <inheritdoc/>
    public SyncAction PlanItem(SyncPlanItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var record = item.Record;
        var local = item.Local;
        var remote = item.Remote;
        var hasBase = record?.HasBase == true;

        if (local == null)
        {
            if (record?.Deleted == true)
            {
                // Tombstone: the file was deleted locally.
                if (remote == null)
                {
                    return Action(item, SyncActionKind.DropRecord);
                }

                return hasBase && remote.ETag == record.BaseETag
                    ? Action(item, SyncActionKind.DeleteRemote)
                    : Action(item, SyncActionKind.Conflict, SyncConflictKind.DeleteEdit);
            }

            if (remote == null)
            {
                return Action(item, record == null ? SyncActionKind.None : SyncActionKind.DropRecord);
            }

            // Either a new remote file, or the local copy went missing without a tombstone: download it again.
            return Action(item, hasBase ? SyncActionKind.Download : SyncActionKind.DownloadNew);
        }

        if (!hasBase)
        {
            // A new local file (never synced).
            return Action(item, remote == null ? SyncActionKind.UploadNew : SyncActionKind.CompareContent);
        }

        var localChanged = record!.Dirty
            || record.Deleted
            || (item.LocalHash != null && item.LocalHash != record.BaseHash);

        if (remote == null)
        {
            // Deleted remotely since the last sync.
            return localChanged
                ? Action(item, SyncActionKind.Conflict, SyncConflictKind.EditDelete)
                : Action(item, SyncActionKind.DeleteLocal);
        }

        var remoteChanged = remote.ETag != record.BaseETag;
        return (localChanged, remoteChanged) switch
        {
            (false, false) => Action(item, SyncActionKind.None),
            (true, false) => Action(item, SyncActionKind.Upload),
            (false, true) => Action(item, SyncActionKind.Download),
            _ => Action(item, SyncActionKind.Conflict, SyncConflictKind.EditEdit),
        };
    }

    private static SyncAction Action(SyncPlanItem item, SyncActionKind kind, SyncConflictKind? conflictKind = null)
    {
        return new SyncAction
        {
            Key = item.Key,
            Kind = kind,
            ConflictKind = conflictKind,
            Local = item.Local,
            Remote = item.Remote,
            Record = item.Record,
        };
    }
}
