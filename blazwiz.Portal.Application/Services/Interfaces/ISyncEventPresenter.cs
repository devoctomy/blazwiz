using blazwiz.Core.Sync.Models;
using blazwiz.Portal.Models.Sync;

namespace blazwiz.Portal.Application.Services.Interfaces;

/// <summary>
/// Maps sync log entry kinds to icons, colours, labels and filter groups, so Core stays free of UI concerns.
/// </summary>
public interface ISyncEventPresenter
{
    /// <summary>
    /// Gets how a kind of entry is shown.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The icon, colour, label and group.</returns>
    SyncEventPresentation Present(SyncEventKind kind);

    /// <summary>
    /// Gets the kinds of entry in a filter group.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <returns>The kinds.</returns>
    IReadOnlyCollection<SyncEventKind> GetKinds(SyncEventGroup group);
}
