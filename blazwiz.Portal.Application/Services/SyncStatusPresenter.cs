using blazwiz.Core.Sync.Connectivity;
using blazwiz.Core.Sync.Models;
using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Models.Sync;

namespace blazwiz.Portal.Application.Services;

/// <inheritdoc/>
public class SyncStatusPresenter : ISyncStatusPresenter
{
    /// <inheritdoc/>
    public SyncStatusPresentation Present(SyncStatus status, ConnectivityStatus connectivity)
    {
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(connectivity);

        var pending = status.PendingCount;
        var pendingText = pending == 1 ? "1 change waiting to sync" : $"{pending} changes waiting to sync";

        if (status.State == SyncState.Syncing)
        {
            return Create("Syncing", "Syncing your changes", "bi-arrow-repeat", "info", isSyncing: true);
        }

        if (!connectivity.IsOnline && connectivity.Reason != null)
        {
            var reason = connectivity.Describe();
            return pending > 0
                ? Create($"Offline · {pending} pending", $"{reason}. {pendingText}.", "bi-wifi-off", "secondary", isOffline: true)
                : Create("Offline", reason, "bi-wifi-off", "secondary", isOffline: true);
        }

        if (status.State == SyncState.Error)
        {
            return Create("Sync error", status.LastError ?? "The last sync failed.", "bi-exclamation-octagon", "danger");
        }

        if (pending > 0)
        {
            return Create($"{pending} pending", pendingText, "bi-cloud-arrow-up", "warning");
        }

        if (connectivity.LastCheckedAt == null)
        {
            return Create("Checking", "Checking the connection", "bi-cloud", "secondary");
        }

        var tooltip = status.ConflictsResolved switch
        {
            0 => "All changes synced",
            1 => "All changes synced. 1 conflict resolved.",
            _ => $"All changes synced. {status.ConflictsResolved} conflicts resolved.",
        };
        return Create("Up to date", tooltip, "bi-cloud-check", "success");
    }

    private static SyncStatusPresentation Create(string text, string tooltip, string icon, string tone, bool isSyncing = false, bool isOffline = false) =>
        new SyncStatusPresentation { Text = text, Tooltip = tooltip, Icon = icon, Tone = tone, IsSyncing = isSyncing, IsOffline = isOffline };
}
