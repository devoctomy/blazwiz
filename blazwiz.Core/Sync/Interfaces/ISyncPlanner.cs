using blazwiz.Core.Sync.Models;

namespace blazwiz.Core.Sync.Interfaces;

/// <summary>
/// Decides what to do with each file from a snapshot of both sides and the index. Pure: no I/O.
/// </summary>
public interface ISyncPlanner
{
    /// <summary>
    /// Plans a pass: one action for every key on either side or in the index.
    /// </summary>
    /// <param name="local">The local files.</param>
    /// <param name="remote">The remote objects.</param>
    /// <param name="index">The index records.</param>
    /// <param name="localHashes">The hashes of the local files on a full verify pass, by key, or null.</param>
    /// <returns>The plan, in key order.</returns>
    SyncPlan Plan(
        IEnumerable<SyncFileInfo> local,
        IEnumerable<RemoteObjectInfo> remote,
        IEnumerable<SyncIndexRecord> index,
        IReadOnlyDictionary<string, string>? localHashes);

    /// <summary>
    /// Plans one key, e.g. again after a 412.
    /// </summary>
    /// <param name="item">What is known about the key.</param>
    /// <returns>The action.</returns>
    SyncAction PlanItem(SyncPlanItem item);
}
