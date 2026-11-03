using Devoctomy.Blazwiz.Sync.Connectivity;
using Devoctomy.Blazwiz.Sync.Models;
using Devoctomy.Blazwiz.UI.Sync.Models;

namespace Devoctomy.Blazwiz.UI.Sync.Services.Interfaces;

/// <summary>
/// Combines the sync status and connectivity into what the status pill, Settings and the Sync page show: up to date,
/// syncing, N pending, offline, or error.
/// </summary>
public interface ISyncStatusPresenter
{
    /// <summary>
    /// Describes the current state.
    /// </summary>
    /// <param name="status">The sync status.</param>
    /// <param name="connectivity">The connectivity status.</param>
    /// <returns>The text, tooltip, icon and tone.</returns>
    SyncStatusPresentation Present(SyncStatus status, ConnectivityStatus connectivity);

    /// <summary>
    /// Describes the connection for the user, e.g. "Offline: S3 unreachable".
    /// </summary>
    /// <param name="connectivity">The connectivity status.</param>
    /// <returns>The description.</returns>
    string DescribeConnectivity(ConnectivityStatus connectivity);
}
