using blazwiz.Portal.Models.Notes;

namespace blazwiz.Portal.Application.Helpers;

/// <summary>
/// Works with the folder structure of a flat list of <see cref="NoteEntry"/> items.
/// </summary>
public static class NoteTreeHelpers
{
    /// <summary>
    /// Gets the entries directly inside a folder, folders first, then by name.
    /// </summary>
    /// <param name="entries">All of the user's entries.</param>
    /// <param name="parentId">The folder id, or null for the top level.</param>
    /// <returns>The sorted child entries.</returns>
    public static IReadOnlyList<NoteEntry> GetChildren(IEnumerable<NoteEntry> entries, string? parentId)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return entries
            .Where(e => e.ParentId == parentId)
            .OrderBy(e => e.IsFolder ? 0 : 1)
            .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Gets the ids of every entry inside a folder, at any depth.
    /// </summary>
    /// <param name="entries">All of the user's entries.</param>
    /// <param name="folderId">The folder id.</param>
    /// <returns>The ids of the folder's descendants (not including the folder itself).</returns>
    public static IReadOnlySet<string> GetDescendantIds(IEnumerable<NoteEntry> entries, string folderId)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(folderId);

        var byParent = entries.Where(e => e.ParentId != null).ToLookup(e => e.ParentId!);
        var result = new HashSet<string>();
        var pending = new Stack<string>();
        pending.Push(folderId);
        while (pending.Count > 0)
        {
            foreach (var child in byParent[pending.Pop()])
            {
                if (result.Add(child.Id))
                {
                    pending.Push(child.Id);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Gets the names of the folders containing an entry, outermost first.
    /// </summary>
    /// <param name="entries">All of the user's entries.</param>
    /// <param name="entry">The entry.</param>
    /// <returns>The folder names. Empty when the entry is at the top level.</returns>
    public static IReadOnlyList<string> GetFolderPath(IEnumerable<NoteEntry> entries, NoteEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(entry);

        var byId = entries.ToDictionary(e => e.Id);
        var path = new List<string>();
        var visited = new HashSet<string>();
        var parentId = entry.ParentId;
        while (parentId != null && visited.Add(parentId) && byId.TryGetValue(parentId, out var parent))
        {
            path.Insert(0, parent.Name);
            parentId = parent.ParentId;
        }

        return path;
    }

    /// <summary>
    /// Builds the rows shown in the files tree: top level entries, plus the contents of expanded folders.
    /// </summary>
    /// <param name="entries">All of the user's entries.</param>
    /// <param name="expandedFolderIds">The ids of the expanded folders.</param>
    /// <returns>The visible rows in display order.</returns>
    public static IReadOnlyList<FileTreeItem> BuildVisibleItems(IEnumerable<NoteEntry> entries, IReadOnlySet<string> expandedFolderIds)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(expandedFolderIds);

        var list = entries.ToList();
        var items = new List<FileTreeItem>();
        AddVisibleItems(list, null, 0, expandedFolderIds, items);
        return items;
    }

    /// <summary>
    /// Builds the list of folders an entry can be moved into: the top level and every folder except the entry and its descendants.
    /// </summary>
    /// <param name="entries">All of the user's entries.</param>
    /// <param name="entryId">The id of the entry being moved.</param>
    /// <param name="topLevelName">The name to show for the top level.</param>
    /// <returns>The folders in tree order.</returns>
    public static IReadOnlyList<FolderOption> BuildMoveTargets(IEnumerable<NoteEntry> entries, string entryId, string topLevelName)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(entryId);

        var list = entries.ToList();
        var excluded = GetDescendantIds(list, entryId).ToHashSet();
        excluded.Add(entryId);

        var options = new List<FolderOption> { new FolderOption { Id = string.Empty, Name = topLevelName, Depth = 0 } };
        AddFolderOptions(list, null, 1, excluded, options);
        return options;
    }

    private static void AddVisibleItems(List<NoteEntry> entries, string? parentId, int depth, IReadOnlySet<string> expanded, List<FileTreeItem> items)
    {
        foreach (var entry in GetChildren(entries, parentId))
        {
            var isExpanded = entry.IsFolder && expanded.Contains(entry.Id);
            items.Add(new FileTreeItem
            {
                Entry = entry,
                Depth = depth,
                IsExpanded = isExpanded,
                ChildCount = entry.IsFolder ? entries.Count(e => e.ParentId == entry.Id) : 0,
            });

            if (isExpanded)
            {
                AddVisibleItems(entries, entry.Id, depth + 1, expanded, items);
            }
        }
    }

    private static void AddFolderOptions(List<NoteEntry> entries, string? parentId, int depth, HashSet<string> excluded, List<FolderOption> options)
    {
        foreach (var folder in GetChildren(entries, parentId).Where(e => e.IsFolder && !excluded.Contains(e.Id)))
        {
            options.Add(new FolderOption { Id = folder.Id, Name = folder.Name, Depth = depth });
            AddFolderOptions(entries, folder.Id, depth + 1, excluded, options);
        }
    }
}
