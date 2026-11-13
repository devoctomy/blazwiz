using System.Globalization;
using Devoctomy.Blazwiz.Core.Text.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;
using Devoctomy.Blazwiz.UI.Sync.Models;
using Devoctomy.Blazwiz.UI.Sync.Services.Interfaces;
using Devoctomy.Blazwiz.UI.Sync.Text;
using Devoctomy.Blazwiz.UI.Text.Interfaces;
using Microsoft.Extensions.Localization;

namespace Devoctomy.Blazwiz.UI.Sync.Services;

/// <inheritdoc/>
public class SyncEventPresenter(IStringLocalizer<SyncUiText> text, ITextPlurals plurals, IErrorText errorText)
    : ISyncEventPresenter
{
    private static readonly IReadOnlyDictionary<SyncEventKind, (string Icon, string ColorClass, SyncEventGroup Group)> Presentations =
        new Dictionary<SyncEventKind, (string Icon, string ColorClass, SyncEventGroup Group)>
        {
            [SyncEventKind.PassStarted] = ("bi-arrow-repeat", "text-body-secondary", SyncEventGroup.Activity),
            [SyncEventKind.PassCompleted] = ("bi-check-circle", "text-success", SyncEventGroup.Activity),
            [SyncEventKind.UploadNew] = ("bi-cloud-plus", "text-primary", SyncEventGroup.Uploads),
            [SyncEventKind.UploadLocalNewer] = ("bi-cloud-arrow-up", "text-primary", SyncEventGroup.Uploads),
            [SyncEventKind.DownloadNew] = ("bi-file-earmark-arrow-down", "text-info", SyncEventGroup.Downloads),
            [SyncEventKind.DownloadRemoteNewer] = ("bi-cloud-arrow-down", "text-info", SyncEventGroup.Downloads),
            [SyncEventKind.DeleteRemote] = ("bi-cloud-slash", "text-warning", SyncEventGroup.Deletes),
            [SyncEventKind.DeleteLocal] = ("bi-trash", "text-warning", SyncEventGroup.Deletes),
            [SyncEventKind.ConflictResolved] = ("bi-exclamation-triangle", "text-warning", SyncEventGroup.Conflicts),
            [SyncEventKind.AlreadyInSync] = ("bi-check2-all", "text-success", SyncEventGroup.Activity),
            [SyncEventKind.Retried] = ("bi-arrow-clockwise", "text-body-secondary", SyncEventGroup.Activity),
            [SyncEventKind.Failed] = ("bi-x-circle", "text-danger", SyncEventGroup.Errors),
            [SyncEventKind.WentOffline] = ("bi-wifi-off", "text-body-secondary", SyncEventGroup.Connectivity),
            [SyncEventKind.WentOnline] = ("bi-wifi", "text-success", SyncEventGroup.Connectivity),
            [SyncEventKind.OfflineModeChanged] = ("bi-airplane", "text-body-secondary", SyncEventGroup.Connectivity),
        };

    private readonly IStringLocalizer<SyncUiText> text = text ?? throw new ArgumentNullException(nameof(text));
    private readonly ITextPlurals plurals = plurals ?? throw new ArgumentNullException(nameof(plurals));
    private readonly IErrorText errorText = errorText ?? throw new ArgumentNullException(nameof(errorText));

    /// <inheritdoc/>
    public SyncEventPresentation Present(SyncEventKind kind)
    {
        var (icon, colorClass, group) = Presentations.TryGetValue(kind, out var known)
            ? known
            : ("bi-circle", "text-body-secondary", SyncEventGroup.Activity);
        var label = text["Sync.Log.Kind." + kind];
        return new SyncEventPresentation
        {
            Icon = icon,
            ColorClass = colorClass,
            Label = label.ResourceNotFound ? kind.ToString() : label.Value,
            Group = group,
        };
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<SyncEventKind> GetKinds(SyncEventGroup group)
    {
        return Presentations.Where(p => p.Value.Group == group).Select(p => p.Key).ToList();
    }

    /// <inheritdoc/>
    public string Describe(SyncEvent entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var arguments = entry.Arguments;
        switch (entry.Code)
        {
            case SyncEventCodes.Summary:
                return DescribeSummary(arguments);
            case SyncEventCodes.PassFailed:
            case SyncEventCodes.UploadFailed:
            case SyncEventCodes.DownloadFailed:
            case SyncEventCodes.DeleteFailed:
            case SyncEventCodes.FileFailed:
                return DescribeFailure(entry.Code, arguments);
            case SyncEventCodes.WentOffline:
                return arguments.Count > 0 ? text["Sync.Connectivity." + arguments[0]] : text["Sync.Log.WentOffline"];
        }

        var found = text["Sync.Log." + entry.Code, [.. arguments]];
        return found.ResourceNotFound ? entry.Code : found.Value;
    }

    /// <inheritdoc/>
    public string DescribePass(SyncEvent? completed)
    {
        var checking = text["Sync.Log.Checking"];
        return completed == null ? checking : text["Sync.Log.PassDone", checking, Describe(completed)];
    }

    /// <inheritdoc/>
    public string DescribeGroup(SyncEventGroup? group)
    {
        return group == null ? text["Sync.Log.Group.All"] : text["Sync.Log.Group." + group];
    }

    private static int Count(IReadOnlyList<string> arguments, int index) =>
        index < arguments.Count && int.TryParse(arguments[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;

    private string DescribeSummary(IReadOnlyList<string> arguments)
    {
        var parts = new List<string>();
        Add(parts, "Sync.Log.Summary.Uploaded", Count(arguments, 0));
        Add(parts, "Sync.Log.Summary.Downloaded", Count(arguments, 1));
        Add(parts, "Sync.Log.Summary.Deleted", Count(arguments, 2));
        Add(parts, "Sync.Log.Summary.Conflicts", Count(arguments, 3));
        Add(parts, "Sync.Log.Summary.Failed", Count(arguments, 4));
        return parts.Count == 0 ? text["Sync.Log.Summary.UpToDate"] : string.Join(text["Sync.Log.Summary.Separator"], parts);
    }

    private string DescribeFailure(string code, IReadOnlyList<string> arguments)
    {
        var error = arguments.Count > 0
            ? errorText.Describe(arguments[0], [.. arguments.Skip(1)])
            : errorText.Describe(new InvalidOperationException());
        return text["Sync.Log." + code, error];
    }

    private void Add(List<string> parts, string key, int count)
    {
        if (count > 0)
        {
            parts.Add(plurals.Format(text, key, count));
        }
    }
}
