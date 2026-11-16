using Devoctomy.Blazwiz.Core.Text.Interfaces;
using Devoctomy.Blazwiz.Sync.Connectivity;
using Devoctomy.Blazwiz.Sync.Models;
using Devoctomy.Blazwiz.UI.Sync.Models;
using Devoctomy.Blazwiz.UI.Sync.Services.Interfaces;
using Devoctomy.Blazwiz.UI.Sync.Text;
using Devoctomy.Blazwiz.UI.Text.Interfaces;
using Microsoft.Extensions.Localization;

namespace Devoctomy.Blazwiz.UI.Sync.Services;

/// <inheritdoc/>
public class SyncStatusPresenter(IStringLocalizer<SyncUiText> text, ITextPlurals plurals, IErrorText errorText)
    : ISyncStatusPresenter
{
    private readonly IStringLocalizer<SyncUiText> text = text ?? throw new ArgumentNullException(nameof(text));
    private readonly ITextPlurals plurals = plurals ?? throw new ArgumentNullException(nameof(plurals));
    private readonly IErrorText errorText = errorText ?? throw new ArgumentNullException(nameof(errorText));

    /// <inheritdoc/>
    public SyncStatusPresentation Present(SyncStatus status, ConnectivityStatus connectivity)
    {
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(connectivity);

        var pending = status.PendingCount;
        var pendingText = plurals.Format(text, "Sync.Status.PendingChanges", pending);

        if (status.State == SyncState.Syncing)
        {
            // One pass shows its count here; with several, each is shown apart (PresentPass), so none is picked.
            var syncing = status.Passes.Count == 1
                ? DescribeProgress(status.Passes[0])
                : text["Sync.Status.Syncing"];
            return Create(syncing, text["Sync.Status.Syncing.Tooltip"], "bi-arrow-repeat", "info", isSyncing: true);
        }

        var offline = PresentOffline(connectivity, pending, pendingText);
        if (offline != null)
        {
            return offline;
        }

        if (status.State == SyncState.Error)
        {
            var error = status.LastErrorCode == null
                ? text["Sync.Status.Error.Tooltip"]
                : errorText.Describe(status.LastErrorCode, status.LastErrorArguments);
            return Create(text["Sync.Status.Error"], error, "bi-exclamation-octagon", "danger");
        }

        if (pending > 0)
        {
            return Create(text["Sync.Status.Pending", pending], pendingText, "bi-cloud-arrow-up", "warning");
        }

        if (connectivity.LastCheckedAt == null)
        {
            return Create(text["Sync.Status.Checking"], text["Sync.Status.Checking.Tooltip"], "bi-cloud", "secondary");
        }

        var tooltip = status.ConflictsResolved == 0
            ? text["Sync.Status.UpToDate.Tooltip"]
            : plurals.Format(text, "Sync.Status.UpToDate.Conflicts", status.ConflictsResolved);
        return Create(text["Sync.Status.UpToDate"], tooltip, "bi-cloud-check", "success");
    }

    /// <inheritdoc/>
    public string DescribeConnectivity(ConnectivityStatus connectivity)
    {
        ArgumentNullException.ThrowIfNull(connectivity);
        if (connectivity.IsOnline)
        {
            return text["Sync.Connectivity.Online"];
        }

        return connectivity.Reason is { } reason ? text["Sync.Connectivity." + reason] : text["Sync.Connectivity.Checking"];
    }

    /// <inheritdoc/>
    public SyncPassPresentation PresentPass(SyncPassStatus pass, string? name)
    {
        ArgumentNullException.ThrowIfNull(pass);
        if (string.IsNullOrWhiteSpace(name))
        {
            // No name to show (never the profile's id): the progress alone.
            return new SyncPassPresentation { Profile = pass.Profile, Text = DescribeProgress(pass) };
        }

        var progress = pass.Total > 0
            ? text["Sync.Status.SyncingProfileChanges", name, pass.Done, pass.Total]
            : text["Sync.Status.SyncingProfile", name];
        return new SyncPassPresentation { Profile = pass.Profile, Name = name, Text = progress };
    }

    private static SyncStatusPresentation Create(string text, string tooltip, string icon, string tone, bool isSyncing = false, bool isOffline = false) =>
        new SyncStatusPresentation { Text = text, Tooltip = tooltip, Icon = icon, Tone = tone, IsSyncing = isSyncing, IsOffline = isOffline };

    private string DescribeProgress(SyncPassStatus pass) =>
        pass.Total > 0
            ? text["Sync.Status.SyncingChanges", pass.Done, pass.Total]
            : text["Sync.Status.Syncing"];

    private SyncStatusPresentation? PresentOffline(ConnectivityStatus connectivity, int pending, string pendingText)
    {
        if (connectivity.IsOnline || connectivity.Reason == null)
        {
            return null;
        }

        if (connectivity.Reason == OfflineReason.SyncDisabled)
        {
            return pending > 0
                ? Create(text["Sync.Status.OffPending", pending], text["Sync.Status.OffPending.Tooltip", pendingText], "bi-pause-circle", "secondary", isOffline: true)
                : Create(text["Sync.Status.Off"], text["Sync.Status.Off.Tooltip"], "bi-pause-circle", "secondary", isOffline: true);
        }

        var reason = DescribeConnectivity(connectivity);
        return pending > 0
            ? Create(text["Sync.Status.OfflinePending", pending], text["Sync.Status.OfflinePending.Tooltip", reason, pendingText], "bi-wifi-off", "secondary", isOffline: true)
            : Create(text["Sync.Status.Offline"], reason, "bi-wifi-off", "secondary", isOffline: true);
    }
}
