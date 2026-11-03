using Devoctomy.Blazwiz.Sync.Models;
using Devoctomy.Blazwiz.UI.Sync.Models;

namespace Devoctomy.Blazwiz.UI.Sync.Services.Interfaces;

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

    /// <summary>
    /// Describes what an event says, from its code and arguments, in the user's language.
    /// </summary>
    /// <param name="entry">The event.</param>
    /// <returns>The description.</returns>
    string Describe(SyncEvent entry);

    /// <summary>
    /// Describes a sync pass for the header of its row in the log, e.g. "Checking for changes · 2 uploaded".
    /// </summary>
    /// <param name="completed">The pass's completed event, or null if it has not finished.</param>
    /// <returns>The description.</returns>
    string DescribePass(SyncEvent? completed);

    /// <summary>
    /// Gets the label of a filter chip.
    /// </summary>
    /// <param name="group">The group, or null for every event.</param>
    /// <returns>The label.</returns>
    string DescribeGroup(SyncEventGroup? group);
}
