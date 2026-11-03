using Devoctomy.Blazwiz.Sync.Models;
using MyApp.Portal.Models.Notes;

namespace MyApp.Portal.Application.Services.Interfaces;

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
    /// Builds the folders and notes from the files under the notes root: markdown files become notes, folder markers
    /// become folders, and folders that only exist in a file's path are added too.
    /// </summary>
    /// <param name="files">The files in the notes file store.</param>
    /// <param name="owner">The user the entries belong to.</param>
    /// <returns>The entries, in no particular order.</returns>
    IReadOnlyList<NoteEntry> BuildEntries(IEnumerable<SyncFileInfo> files, string owner);

    /// <summary>
    /// Gets the key of a note or folder inside a folder, e.g. <c>notes/Work/Ideas.md</c> or <c>notes/Work/Meetings/</c>.
    /// </summary>
    /// <param name="parentKey">The folder key, or null for the top level.</param>
    /// <param name="name">The note or folder name.</param>
    /// <param name="isFolder">True for a folder (the key ends with <c>/</c>).</param>
    /// <returns>The key.</returns>
    string GetChildKey(string? parentKey, string name, bool isFolder);

    /// <summary>
    /// Gets the name of a note or folder from its key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The last part of the path, without a trailing <c>/</c>.</returns>
    string GetName(string key);

    /// <summary>
    /// Gets the key of the folder containing a note or folder.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The folder key, or null at the top level.</returns>
    string? GetParentKey(string key);

    /// <summary>
    /// Gets the names of the folders in a key's path, outermost first.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The folder names. Empty at the top level.</returns>
    IReadOnlyList<string> GetFolderNames(string key);

    /// <summary>
    /// Checks whether a key is a note under the notes root.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>True for a markdown file under the root.</returns>
    bool IsNoteKey(string key);

    /// <summary>
    /// Builds the list of folders an entry can be moved into: the top level and every folder except the entry and its descendants.
    /// </summary>
    /// <param name="entries">All of the user's entries.</param>
    /// <param name="entryId">The id of the entry being moved.</param>
    /// <param name="topLevelName">The name to show for the top level.</param>
    /// <returns>The folders in tree order.</returns>
    IReadOnlyList<FolderOption> BuildMoveTargets(IEnumerable<NoteEntry> entries, string entryId, string topLevelName);
}
