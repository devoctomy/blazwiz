using Devoctomy.Blazwiz.Sync.Models;

namespace Devoctomy.Blazwiz.Sync.Interfaces;

/// <summary>
/// Runs one sync pass for one profile: snapshot, plan, execute, report.
/// </summary>
public interface ISyncEngine
{
    /// <summary>
    /// Runs a pass. Per file failures are recorded in the result and the pass carries on.
    /// </summary>
    /// <param name="profile">The profile.</param>
    /// <param name="verify">True to hash every local file against the index (the first pass after sign in).</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The result.</returns>
    /// <exception cref="Exceptions.SyncNetworkException">The remote could not be reached. The pass stops.</exception>
    /// <exception cref="Exceptions.SyncAccessDeniedException">The credentials were refused. The pass stops.</exception>
    Task<SyncResult> SyncAsync(SyncProfile profile, bool verify, CancellationToken ct = default);
}
