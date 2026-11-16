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
    /// Describes one running pass with its profile's name, for the status pill: e.g. "Syncing Work 12 of 201", or
    /// "Syncing Work" until it has planned its changes.
    /// </summary>
    /// <param name="pass">The pass.</param>
    /// <param name="name">The name its profile is shown by, or null when it has none to show (e.g. a locked profile whose
    /// label is not known on this device): the text is then the progress alone, e.g. "Syncing 12 of 201".</param>
    /// <returns>The profile and the pass's progress, with the name in the text when there is one.</returns>
    SyncPassPresentation PresentPass(SyncPassStatus pass, string? name);

    /// <summary>
    /// Describes the connection for the user, e.g. "Offline: S3 unreachable".
    /// </summary>
    /// <param name="connectivity">The connectivity status.</param>
    /// <returns>The description.</returns>
    string DescribeConnectivity(ConnectivityStatus connectivity);
}
