using blazwiz.Portal.Models.Notes;

namespace blazwiz.Portal.Application.Services.Interfaces;

/// <summary>
/// Works with the folder structure of a flat list of <see cref="NoteEntry"/> items.
/// </summary>
public interface INoteTreeService
{
    /// <summary>
    /// Gets the entries directly inside a folder, folders first, then by name.
    /// </summary>
    /// <param name="entries">All of the user's entries.</param>
    /// <param name="parentId">The folder id, or null for the top level.</param>
    /// <returns>The sorted child entries.</returns>
    IReadOnlyList<NoteEntry> GetChildren(IEnumerable<NoteEntry> entries, string? parentId);

    /// <summary>
    /// Gets the ids of every entry inside a folder, at any depth.
    /// </summary>
    /// <param name="entries">All of the user's entries.</param>
    /// <param name="folderId">The folder id.</param>
    /// <returns>The ids of the folder's descendants (not including the folder itself).</returns>
    IReadOnlySet<string> GetDescendantIds(IEnumerable<NoteEntry> entries, string folderId);

    /// <summary>
    /// Gets the names of the folders containing an entry, outermost first.
    /// </summary>
    /// <param name="entries">All of the user's entries.</param>
    /// <param name="entry">The entry.</param>
    /// <returns>The folder names. Empty when the entry is at the top level.</returns>
    IReadOnlyList<string> GetFolderPath(IEnumerable<NoteEntry> entries, NoteEntry entry);

    /// <summary>
    /// Builds the rows shown in the files tree: top level entries, plus the contents of expanded folders.
    /// </summary>
    /// <param name="entries">All of the user's entries.</param>
    /// <param name="expandedFolderIds">The ids of the expanded folders.</param>
    /// <returns>The visible rows in display order.</returns>
    IReadOnlyList<FileTreeItem> BuildVisibleItems(IEnumerable<NoteEntry> entries, IReadOnlySet<string> expandedFolderIds);

    /// <summary>
    /// Builds the list of folders an entry can be moved into: the top level and every folder except the entry and its descendants.
    /// </summary>
    /// <param name="entries">All of the user's entries.</param>
    /// <param name="entryId">The id of the entry being moved.</param>
    /// <param name="topLevelName">The name to show for the top level.</param>
    /// <returns>The folders in tree order.</returns>
    IReadOnlyList<FolderOption> BuildMoveTargets(IEnumerable<NoteEntry> entries, string entryId, string topLevelName);
}
