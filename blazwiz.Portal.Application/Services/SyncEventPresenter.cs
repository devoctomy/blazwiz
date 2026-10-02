using blazwiz.Core.Sync.Models;
using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Models.Sync;

namespace blazwiz.Portal.Application.Services;

/// <inheritdoc/>
public class SyncEventPresenter : ISyncEventPresenter
{
    private static readonly IReadOnlyDictionary<SyncEventKind, SyncEventPresentation> Presentations = new Dictionary<SyncEventKind, SyncEventPresentation>
    {
        [SyncEventKind.PassStarted] = Create("bi-arrow-repeat", "text-body-secondary", "Checking", SyncEventGroup.Activity),
        [SyncEventKind.PassCompleted] = Create("bi-check-circle", "text-success", "Checked", SyncEventGroup.Activity),
        [SyncEventKind.UploadNew] = Create("bi-cloud-plus", "text-primary", "Uploaded", SyncEventGroup.Uploads),
        [SyncEventKind.UploadLocalNewer] = Create("bi-cloud-arrow-up", "text-primary", "Uploaded", SyncEventGroup.Uploads),
        [SyncEventKind.DownloadNew] = Create("bi-file-earmark-arrow-down", "text-info", "Downloaded", SyncEventGroup.Downloads),
        [SyncEventKind.DownloadRemoteNewer] = Create("bi-cloud-arrow-down", "text-info", "Downloaded", SyncEventGroup.Downloads),
        [SyncEventKind.DeleteRemote] = Create("bi-cloud-slash", "text-warning", "Deleted from S3", SyncEventGroup.Deletes),
        [SyncEventKind.DeleteLocal] = Create("bi-trash", "text-warning", "Deleted here", SyncEventGroup.Deletes),
        [SyncEventKind.ConflictResolved] = Create("bi-exclamation-triangle", "text-warning", "Conflict", SyncEventGroup.Conflicts),
        [SyncEventKind.AlreadyInSync] = Create("bi-check2-all", "text-success", "Already in sync", SyncEventGroup.Activity),
        [SyncEventKind.Retried] = Create("bi-arrow-clockwise", "text-body-secondary", "Retried", SyncEventGroup.Activity),
        [SyncEventKind.Failed] = Create("bi-x-circle", "text-danger", "Failed", SyncEventGroup.Errors),
        [SyncEventKind.WentOffline] = Create("bi-wifi-off", "text-body-secondary", "Offline", SyncEventGroup.Connectivity),
        [SyncEventKind.WentOnline] = Create("bi-wifi", "text-success", "Online", SyncEventGroup.Connectivity),
        [SyncEventKind.OfflineModeChanged] = Create("bi-airplane", "text-body-secondary", "Offline mode", SyncEventGroup.Connectivity),
    };

    /// <inheritdoc/>
    public SyncEventPresentation Present(SyncEventKind kind)
    {
        var known = Presentations.TryGetValue(kind, out var presentation)
            ? presentation
            : Create("bi-circle", "text-body-secondary", kind.ToString(), SyncEventGroup.Activity);

        // A copy, so callers cannot change the shared table.
        return Create(known.Icon, known.ColorClass, known.Label, known.Group);
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<SyncEventKind> GetKinds(SyncEventGroup group)
    {
        return Presentations.Where(p => p.Value.Group == group).Select(p => p.Key).ToList();
    }

    private static SyncEventPresentation Create(string icon, string colorClass, string label, SyncEventGroup group) =>
        new SyncEventPresentation { Icon = icon, ColorClass = colorClass, Label = label, Group = group };
}
