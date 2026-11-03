using System.Text;
using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using MyApp.Portal.Application.Exceptions;
using MyApp.Portal.Application.Services.Interfaces;
using MyApp.Portal.Models.Notes;

namespace MyApp.Portal.Application.Services;

/// <summary>
/// Notes are plain markdown files in the <c>notes</c> sync profile's file store, and folders are paths, so a note's id
/// is its key (e.g. <c>notes/Work/Ideas.md</c>). S3 has no rename, so renaming or moving writes the new keys and then
/// deletes the old ones.
/// </summary>
public class NoteService(
    IAuthenticationService authService,
    [FromKeyedServices(NoteService.ProfileName)] ISyncFileStore fileStore,
    INotesStateStore stateStore,
    ISyncService syncService,
    IConnectivityService connectivityService,
    INoteNameService noteNameService,
    INoteTreeService noteTreeService,
    TimeProvider timeProvider)
    : INoteService
{
    /// <summary>
    /// The sync profile holding the notes.
    /// </summary>
    public const string ProfileName = "notes";

    /// <summary>
    /// The name of the note created for new users.
    /// </summary>
    public const string WelcomeNoteName = "Welcome.md";

    /// <summary>
    /// The logical name of the embedded resource holding the welcome note's content.
    /// </summary>
    public const string WelcomeNoteResourceName = "WelcomeNote.md";

    private readonly IAuthenticationService authService = authService ?? throw new ArgumentNullException(nameof(authService));
    private readonly ISyncFileStore fileStore = fileStore ?? throw new ArgumentNullException(nameof(fileStore));
    private readonly INotesStateStore stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
    private readonly ISyncService syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));
    private readonly IConnectivityService connectivityService = connectivityService ?? throw new ArgumentNullException(nameof(connectivityService));
    private readonly INoteNameService noteNameService = noteNameService ?? throw new ArgumentNullException(nameof(noteNameService));
    private readonly INoteTreeService noteTreeService = noteTreeService ?? throw new ArgumentNullException(nameof(noteTreeService));
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

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
        var entries = await LoadEntriesAsync(owner);

        var state = await stateStore.GetAsync(owner);
        if (state?.WelcomeNoteCreated == true || !await CanCreateWelcomeNoteAsync())
        {
            return entries;
        }

        // Only once the first sync has shown there are no notes anywhere, so deleting the welcome note on one device
        // does not bring it back from a new one.
        if (entries.Count == 0)
        {
            await fileStore.WriteAsync(noteTreeService.GetChildKey(null, WelcomeNoteName, false), Encoding.UTF8.GetBytes(ReadWelcomeNote()));
            entries = await LoadEntriesAsync(owner);
        }

        await stateStore.SaveAsync(new NotesState { Owner = owner, WelcomeNoteCreated = true });
        return entries;
    }

    /// <inheritdoc/>
    public async Task<NoteEntry> CreateFolderAsync(string name, string? parentId)
    {
        var owner = await GetOwnerAsync();
        var entries = await LoadEntriesAsync(owner);
        var folderName = ValidName(name);
        EnsureFolderExists(entries, parentId);
        EnsureNameIsFree(entries, parentId, folderName, null);

        var key = noteTreeService.GetChildKey(parentId, folderName, true);
        await fileStore.CreateFolderAsync(key);
        return CreateEntry(owner, key, NoteEntryKind.Folder);
    }

    /// <inheritdoc/>
    public async Task<NoteEntry> CreateNoteAsync(string name, string? parentId, string content = "")
    {
        ArgumentNullException.ThrowIfNull(content);

        var owner = await GetOwnerAsync();
        var entries = await LoadEntriesAsync(owner);
        var noteName = noteNameService.ToNoteFileName(ValidName(name));
        EnsureFolderExists(entries, parentId);
        EnsureNameIsFree(entries, parentId, noteName, null);

        var key = noteTreeService.GetChildKey(parentId, noteName, false);
        await fileStore.WriteAsync(key, Encoding.UTF8.GetBytes(content));
        return CreateEntry(owner, key, NoteEntryKind.Note);
    }

    /// <inheritdoc/>
    public async Task<NoteEntry> RenameAsync(string id, string name)
    {
        var owner = await GetOwnerAsync();
        var entries = await LoadEntriesAsync(owner);
        var entry = FindEntry(entries, id);
        var newName = entry.IsFolder ? ValidName(name) : noteNameService.ToNoteFileName(ValidName(name));
        if (newName == entry.Name)
        {
            return entry;
        }

        EnsureNameIsFree(entries, entry.ParentId, newName, entry.Id);
        var newKey = noteTreeService.GetChildKey(entry.ParentId, newName, entry.IsFolder);
        await MoveKeysAsync(entry, newKey);
        return CreateEntry(owner, newKey, entry.Kind);
    }

    /// <inheritdoc/>
    public async Task<NoteEntry> MoveAsync(string id, string? parentId)
    {
        var owner = await GetOwnerAsync();
        var entries = await LoadEntriesAsync(owner);
        var entry = FindEntry(entries, id);
        if (entry.ParentId == parentId)
        {
            return entry;
        }

        EnsureFolderExists(entries, parentId);
        if (parentId != null && entry.IsFolder && parentId.StartsWith(entry.Id, StringComparison.Ordinal))
        {
            throw new NotesException("A folder cannot be moved into itself.");
        }

        EnsureNameIsFree(entries, parentId, entry.Name, entry.Id);
        var newKey = noteTreeService.GetChildKey(parentId, entry.Name, entry.IsFolder);
        await MoveKeysAsync(entry, newKey);
        return CreateEntry(owner, newKey, entry.Kind);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string id)
    {
        var owner = await GetOwnerAsync();
        var entries = await LoadEntriesAsync(owner);
        var entry = FindEntry(entries, id);
        foreach (var key in await GetKeysUnderAsync(entry))
        {
            await fileStore.DeleteAsync(key);
        }
    }

    /// <inheritdoc/>
    public async Task<NoteDocument?> GetNoteAsync(string id)
    {
        var owner = await GetOwnerAsync();
        if (!noteTreeService.IsNoteKey(id))
        {
            return null;
        }

        var file = await fileStore.ReadAsync(id);
        if (file == null)
        {
            return null;
        }

        var entry = CreateEntry(owner, id, NoteEntryKind.Note);
        entry.CreatedAt = file.ModifiedAt;
        entry.ModifiedAt = file.ModifiedAt;
        return new NoteDocument
        {
            Entry = entry,
            Content = Encoding.UTF8.GetString(file.Content),
            FolderPath = noteTreeService.GetFolderNames(id),
        };
    }

    /// <inheritdoc/>
    public async Task<NoteEntry> SaveNoteAsync(string id, string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var owner = await GetOwnerAsync();
        if (!noteTreeService.IsNoteKey(id))
        {
            throw new NotesException("Folders cannot be edited.");
        }

        // Writing a note that was deleted on another device brings it back (the user chose to keep their version).
        await fileStore.WriteAsync(id, Encoding.UTF8.GetBytes(content));
        return CreateEntry(owner, id, NoteEntryKind.Note);
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
        // S3 keys are case sensitive, but names are compared ignoring case so two devices never create both.
        if (entries.Any(e => e.ParentId == parentId && e.Id != ignoreId && string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new NotesException($"There is already something called '{name}' in this folder.");
        }
    }

    private async Task<bool> CanCreateWelcomeNoteAsync()
    {
        if (syncService.Status.LastSyncedAt != null)
        {
            return true;
        }

        // No sync can run (offline mode, no network, no credentials), so do not wait for one.
        return !(await connectivityService.CheckAsync()).IsOnline;
    }

    private async Task<IReadOnlyList<NoteEntry>> LoadEntriesAsync(string owner)
    {
        return noteTreeService.BuildEntries(await fileStore.ListAsync(), owner);
    }

    private async Task<IReadOnlyList<string>> GetKeysUnderAsync(NoteEntry entry)
    {
        if (!entry.IsFolder)
        {
            return [entry.Id];
        }

        var files = await fileStore.ListAsync();
        return files.Select(f => f.Key).Where(k => k.StartsWith(entry.Id, StringComparison.Ordinal)).ToList();
    }

    private async Task MoveKeysAsync(NoteEntry entry, string newKey)
    {
        // Write every new key before deleting any old one, so a failure part way through never loses a note.
        var oldKeys = await GetKeysUnderAsync(entry);
        foreach (var oldKey in oldKeys)
        {
            var file = await fileStore.ReadAsync(oldKey)
                ?? throw new NotesException("That note or folder no longer exists.");
            await fileStore.WriteAsync(newKey + oldKey[entry.Id.Length..], file.Content, file.ModifiedAt);
        }

        foreach (var oldKey in oldKeys)
        {
            await fileStore.DeleteAsync(oldKey);
        }
    }

    private NoteEntry CreateEntry(string owner, string key, NoteEntryKind kind)
    {
        var now = timeProvider.GetUtcNow();
        return new NoteEntry
        {
            Id = key,
            Owner = owner,
            Kind = kind,
            Name = noteTreeService.GetName(key),
            ParentId = noteTreeService.GetParentKey(key),
            CreatedAt = now,
            ModifiedAt = now,
        };
    }

    private string ValidName(string name)
    {
        var error = noteNameService.Validate(name);
        if (error != null)
        {
            throw new NotesException(error);
        }

        return name.Trim();
    }

    private async Task<string> GetOwnerAsync()
    {
        var user = await authService.GetCurrentUserAsync();
        return user?.Username ?? throw new NotesException("Please sign in to use your notes.");
    }
}
