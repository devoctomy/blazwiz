using blazwiz.Core.Time.Interfaces;
using blazwiz.Portal.Application.Exceptions;
using blazwiz.Portal.Application.Helpers;
using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Models.Notes;

namespace blazwiz.Portal.Application.Services;

/// <inheritdoc/>
public class NoteService(
    IAuthenticationService authService,
    INoteIndexStore indexStore,
    IEnumerable<INoteContentStore> contentStores,
    ISystemClock systemClock)
    : INoteService
{
    /// <summary>
    /// The name of the note created for new users.
    /// </summary>
    public const string WelcomeNoteName = "Welcome.md";

    /// <summary>
    /// The logical name of the embedded resource holding the welcome note's content.
    /// </summary>
    public const string WelcomeNoteResourceName = "WelcomeNote.md";

    /// <summary>
    /// The storage provider used for new notes.
    /// </summary>
    public const string DefaultStorageProvider = NoteStorageProviders.IndexedDb;

    private readonly IAuthenticationService authService = ValidationHelpers.VerifiedAssignment(authService);
    private readonly INoteIndexStore indexStore = ValidationHelpers.VerifiedAssignment(indexStore);
    private readonly IReadOnlyList<INoteContentStore> contentStores = ValidationHelpers.VerifiedAssignment(contentStores).ToList();
    private readonly ISystemClock systemClock = ValidationHelpers.VerifiedAssignment(systemClock);

    /// <summary>
    /// Reads the welcome note's markdown from the embedded resource.
    /// </summary>
    /// <returns>The markdown.</returns>
    public static string ReadWelcomeNote()
    {
        using var stream = typeof(NoteService).Assembly.GetManifestResourceStream(WelcomeNoteResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{WelcomeNoteResourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().ReplaceLineEndings("\n");
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<NoteEntry>> GetEntriesAsync()
    {
        var owner = await GetOwnerAsync();
        var entries = await indexStore.GetEntriesAsync(owner);

        var state = await indexStore.GetStateAsync(owner);
        if (state?.WelcomeNoteCreated == true)
        {
            return entries;
        }

        if (!entries.Any(e => e.ParentId == null && string.Equals(e.Name, WelcomeNoteName, StringComparison.OrdinalIgnoreCase)))
        {
            var welcome = await CreateNoteEntryAsync(owner, entries, WelcomeNoteName, null, ReadWelcomeNote());
            entries = [.. entries, welcome];
        }

        await indexStore.SaveStateAsync(new NotesState { Owner = owner, WelcomeNoteCreated = true });
        return entries;
    }

    /// <inheritdoc/>
    public async Task<NoteEntry> CreateFolderAsync(string name, string? parentId)
    {
        var owner = await GetOwnerAsync();
        var entries = await indexStore.GetEntriesAsync(owner);
        var folderName = ValidName(name);
        EnsureFolderExists(entries, parentId);
        EnsureNameIsFree(entries, parentId, folderName, null);

        var now = systemClock.UtcNow;
        var folder = new NoteEntry
        {
            Id = NewId(),
            Owner = owner,
            Kind = NoteEntryKind.Folder,
            Name = folderName,
            ParentId = parentId,
            CreatedAt = now,
            ModifiedAt = now,
        };

        await indexStore.SaveEntryAsync(folder);
        return folder;
    }

    /// <inheritdoc/>
    public async Task<NoteEntry> CreateNoteAsync(string name, string? parentId, string content = "")
    {
        ArgumentNullException.ThrowIfNull(content);

        var owner = await GetOwnerAsync();
        var entries = await indexStore.GetEntriesAsync(owner);
        return await CreateNoteEntryAsync(owner, entries, name, parentId, content);
    }

    /// <inheritdoc/>
    public async Task<NoteEntry> RenameAsync(string id, string name)
    {
        var owner = await GetOwnerAsync();
        var entries = await indexStore.GetEntriesAsync(owner);
        var entry = FindEntry(entries, id);
        var newName = entry.IsFolder ? ValidName(name) : NoteNameHelpers.ToNoteFileName(ValidName(name));
        if (newName == entry.Name)
        {
            return entry;
        }

        EnsureNameIsFree(entries, entry.ParentId, newName, entry.Id);
        entry.Name = newName;
        entry.ModifiedAt = systemClock.UtcNow;
        await indexStore.SaveEntryAsync(entry);
        return entry;
    }

    /// <inheritdoc/>
    public async Task<NoteEntry> MoveAsync(string id, string? parentId)
    {
        var owner = await GetOwnerAsync();
        var entries = await indexStore.GetEntriesAsync(owner);
        var entry = FindEntry(entries, id);
        if (entry.ParentId == parentId)
        {
            return entry;
        }

        EnsureFolderExists(entries, parentId);
        if (parentId != null && entry.IsFolder && (parentId == entry.Id || NoteTreeHelpers.GetDescendantIds(entries, entry.Id).Contains(parentId)))
        {
            throw new NotesException("A folder cannot be moved into itself.");
        }

        EnsureNameIsFree(entries, parentId, entry.Name, entry.Id);
        entry.ParentId = parentId;
        entry.ModifiedAt = systemClock.UtcNow;
        await indexStore.SaveEntryAsync(entry);
        return entry;
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string id)
    {
        var owner = await GetOwnerAsync();
        var entries = await indexStore.GetEntriesAsync(owner);
        var entry = FindEntry(entries, id);

        var ids = new HashSet<string> { entry.Id };
        if (entry.IsFolder)
        {
            ids.UnionWith(NoteTreeHelpers.GetDescendantIds(entries, entry.Id));
        }

        // Remove the index entries first, so a failure part way through never leaves an entry without content.
        var notes = entries.Where(e => ids.Contains(e.Id) && !e.IsFolder).ToList();
        await indexStore.DeleteEntriesAsync(ids);
        foreach (var note in notes)
        {
            await GetContentStore(note).DeleteAsync(note.Id);
        }
    }

    /// <inheritdoc/>
    public async Task<NoteDocument?> GetNoteAsync(string id)
    {
        var owner = await GetOwnerAsync();
        var entries = await indexStore.GetEntriesAsync(owner);
        var entry = entries.FirstOrDefault(e => e.Id == id && !e.IsFolder);
        if (entry == null)
        {
            return null;
        }

        return new NoteDocument
        {
            Entry = entry,
            Content = await GetContentStore(entry).ReadAsync(entry.Id) ?? string.Empty,
            FolderPath = NoteTreeHelpers.GetFolderPath(entries, entry),
        };
    }

    /// <inheritdoc/>
    public async Task<NoteEntry> SaveNoteAsync(string id, string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var owner = await GetOwnerAsync();
        var entries = await indexStore.GetEntriesAsync(owner);
        var entry = FindEntry(entries, id);
        if (entry.IsFolder)
        {
            throw new NotesException("Folders cannot be edited.");
        }

        await GetContentStore(entry).WriteAsync(entry.Id, content);
        entry.ModifiedAt = systemClock.UtcNow;
        await indexStore.SaveEntryAsync(entry);
        return entry;
    }

    private static string NewId() => Guid.NewGuid().ToString("N");

    private static string ValidName(string name)
    {
        var error = NoteNameHelpers.Validate(name);
        if (error != null)
        {
            throw new NotesException(error);
        }

        return name.Trim();
    }

    private static NoteEntry FindEntry(IEnumerable<NoteEntry> entries, string id)
    {
        return entries.FirstOrDefault(e => e.Id == id)
            ?? throw new NotesException("That note or folder no longer exists.");
    }

    private static void EnsureFolderExists(IEnumerable<NoteEntry> entries, string? folderId)
    {
        if (folderId != null && !entries.Any(e => e.Id == folderId && e.IsFolder))
        {
            throw new NotesException("That folder no longer exists.");
        }
    }

    private static void EnsureNameIsFree(IEnumerable<NoteEntry> entries, string? parentId, string name, string? ignoreId)
    {
        if (entries.Any(e => e.ParentId == parentId && e.Id != ignoreId && string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new NotesException($"There is already something called '{name}' in this folder.");
        }
    }

    private async Task<NoteEntry> CreateNoteEntryAsync(string owner, IReadOnlyList<NoteEntry> entries, string name, string? parentId, string content)
    {
        var noteName = NoteNameHelpers.ToNoteFileName(ValidName(name));
        EnsureFolderExists(entries, parentId);
        EnsureNameIsFree(entries, parentId, noteName, null);

        var now = systemClock.UtcNow;
        var note = new NoteEntry
        {
            Id = NewId(),
            Owner = owner,
            Kind = NoteEntryKind.Note,
            Name = noteName,
            ParentId = parentId,
            StorageProvider = DefaultStorageProvider,
            CreatedAt = now,
            ModifiedAt = now,
        };

        // Write the content before the index entry, so the index never points at missing content.
        await GetContentStore(note).WriteAsync(note.Id, content);
        await indexStore.SaveEntryAsync(note);
        return note;
    }

    private INoteContentStore GetContentStore(NoteEntry note)
    {
        var provider = note.StorageProvider ?? DefaultStorageProvider;
        return contentStores.FirstOrDefault(s => s.ProviderName == provider)
            ?? throw new NotesException($"Notes stored in '{provider}' cannot be opened in this version of the app.");
    }

    private async Task<string> GetOwnerAsync()
    {
        var user = await authService.GetCurrentUserAsync();
        return user?.Username ?? throw new NotesException("Please sign in to use your notes.");
    }
}
