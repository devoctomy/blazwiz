using blazwiz.Core.Sync.Models;
using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Models.Notes;

namespace blazwiz.Portal.Application.Services;

/// <inheritdoc/>
public class NoteTreeService
    : INoteTreeService
{
    /// <summary>
    /// The key every note and folder is under, relative to the user's root.
    /// </summary>
    public const string RootKey = "notes/";

    /// <summary>
    /// The file extension of notes.
    /// </summary>
    public const string NoteExtension = ".md";

    /// <inheritdoc/>
    public IReadOnlyList<NoteEntry> BuildEntries(IEnumerable<SyncFileInfo> files, string owner)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(owner);

        var entries = new Dictionary<string, NoteEntry>(StringComparer.Ordinal);
        foreach (var file in files.Where(f => f.Key.StartsWith(RootKey, StringComparison.Ordinal) && f.Key.Length > RootKey.Length))
        {
            if (file.IsFolder)
            {
                AddFolders(entries, file.Key, owner, file.ModifiedAt);
            }
            else if (IsNoteKey(file.Key))
            {
                entries[file.Key] = CreateEntry(file.Key, owner, NoteEntryKind.Note, file.ModifiedAt);
                var parent = GetParentKey(file.Key);
                if (parent != null)
                {
                    AddFolders(entries, parent, owner, file.ModifiedAt);
                }
            }
        }

        return entries.Values.ToList();
    }

    /// <inheritdoc/>
    public string GetChildKey(string? parentKey, string name, bool isFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return (parentKey ?? RootKey) + name + (isFolder ? "/" : string.Empty);
    }

    /// <inheritdoc/>
    public string GetName(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        var path = key.TrimEnd('/');
        var nameStart = path.LastIndexOf('/') + 1;
        return path[nameStart..];
    }

    /// <inheritdoc/>
    public string? GetParentKey(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        var path = key.TrimEnd('/');
        var parentLength = path.LastIndexOf('/') + 1;
        var parent = path[..parentLength];
        return parent.Length <= RootKey.Length ? null : parent;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetFolderNames(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        var parent = GetParentKey(key);
        return parent == null
            ? []
            : parent[RootKey.Length..].TrimEnd('/').Split('/');
    }

    /// <inheritdoc/>
    public bool IsNoteKey(string key)
    {
        return key != null
            && key.StartsWith(RootKey, StringComparison.Ordinal)
            && key.EndsWith(NoteExtension, StringComparison.OrdinalIgnoreCase)
            && key.Length > RootKey.Length + NoteExtension.Length;
    }

    /// <inheritdoc/>
    public IReadOnlyList<NoteEntry> GetChildren(IEnumerable<NoteEntry> entries, string? parentId)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return entries
            .Where(e => e.ParentId == parentId)
            .OrderBy(e => e.IsFolder ? 0 : 1)
            .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <inheritdoc/>
    public IReadOnlySet<string> GetDescendantIds(IEnumerable<NoteEntry> entries, string folderId)
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

    /// <inheritdoc/>
    public IReadOnlyList<string> GetFolderPath(IEnumerable<NoteEntry> entries, NoteEntry entry)
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

    /// <inheritdoc/>
    public IReadOnlyList<FileTreeItem> BuildVisibleItems(IEnumerable<NoteEntry> entries, IReadOnlySet<string> expandedFolderIds)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(expandedFolderIds);

        var list = entries.ToList();
        var items = new List<FileTreeItem>();
        AddVisibleItems(list, null, 0, expandedFolderIds, items);
        return items;
    }

    /// <inheritdoc/>
    public IReadOnlyList<FolderOption> BuildMoveTargets(IEnumerable<NoteEntry> entries, string entryId, string topLevelName)
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

    private void AddFolders(Dictionary<string, NoteEntry> entries, string folderKey, string owner, DateTimeOffset modifiedAt)
    {
        string? key = folderKey;
        while (key != null)
        {
            if (entries.TryGetValue(key, out var existing))
            {
                if (modifiedAt > existing.ModifiedAt)
                {
                    existing.ModifiedAt = modifiedAt;
                    existing.CreatedAt = modifiedAt;
                }
            }
            else
            {
                entries[key] = CreateEntry(key, owner, NoteEntryKind.Folder, modifiedAt);
            }

            key = GetParentKey(key);
        }
    }

    private NoteEntry CreateEntry(string key, string owner, NoteEntryKind kind, DateTimeOffset modifiedAt)
    {
        return new NoteEntry
        {
            Id = key,
            Owner = owner,
            Kind = kind,
            Name = GetName(key),
            ParentId = GetParentKey(key),
            CreatedAt = modifiedAt,
            ModifiedAt = modifiedAt,
        };
    }

    private void AddVisibleItems(List<NoteEntry> entries, string? parentId, int depth, IReadOnlySet<string> expanded, List<FileTreeItem> items)
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

    private void AddFolderOptions(List<NoteEntry> entries, string? parentId, int depth, HashSet<string> excluded, List<FolderOption> options)
    {
        foreach (var folder in GetChildren(entries, parentId).Where(e => e.IsFolder && !excluded.Contains(e.Id)))
        {
            options.Add(new FolderOption { Id = folder.Id, Name = folder.Name, Depth = depth });
            AddFolderOptions(entries, folder.Id, depth + 1, excluded, options);
        }
    }
}
