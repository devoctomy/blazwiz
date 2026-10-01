using System.Text;
using blazwiz.Core.Sync.Interfaces;
using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Models.Notes;
using Microsoft.Extensions.DependencyInjection;

namespace blazwiz.Portal.Application.Services;

/// <summary>
/// Moves notes from the old <c>entries</c> and <c>content</c> object stores into the notes file store, through the
/// tracked store so they are uploaded on the next sync. Names are cleaned up to be valid S3 keys.
/// </summary>
public class NoteMigrationService(
    IIndexedDbClient client,
    [FromKeyedServices(NoteService.ProfileName)] ISyncFileStore fileStore,
    INoteNameService noteNameService,
    INoteTreeService noteTreeService)
    : INoteMigrationService
{
    /// <summary>
    /// The old object store holding the folder and note entries.
    /// </summary>
    public const string EntriesStore = "entries";

    /// <summary>
    /// The old object store holding note content.
    /// </summary>
    public const string ContentStore = "content";

    private readonly IIndexedDbClient client = client ?? throw new ArgumentNullException(nameof(client));
    private readonly ISyncFileStore fileStore = fileStore ?? throw new ArgumentNullException(nameof(fileStore));
    private readonly INoteNameService noteNameService = noteNameService ?? throw new ArgumentNullException(nameof(noteNameService));
    private readonly INoteTreeService noteTreeService = noteTreeService ?? throw new ArgumentNullException(nameof(noteTreeService));
    private readonly HashSet<string> checkedOwners = [];

    /// <inheritdoc/>
    public async Task MigrateAsync(string owner)
    {
        ArgumentException.ThrowIfNullOrEmpty(owner);
        if (checkedOwners.Contains(owner))
        {
            return;
        }

        var database = IndexedDbNotesStateStore.Database;
        if (!await client.StoreExistsAsync(database, EntriesStore))
        {
            checkedOwners.Add(owner);
            return;
        }

        var allEntries = await client.GetAllFromExistingAsync<NoteEntry>(database, EntriesStore);
        var entries = allEntries.Where(e => e.Owner == owner).ToList();
        if (entries.Count > 0)
        {
            var contents = (await client.GetAllFromExistingAsync<LegacyNoteContent>(database, ContentStore))
                .ToDictionary(c => c.Id, c => c.Content);
            var keys = BuildKeys(entries);

            // Folders first (so empty ones are kept), then notes with their content and original edit time.
            foreach (var folder in entries.Where(e => e.IsFolder))
            {
                await fileStore.CreateFolderAsync(keys[folder.Id]);
            }

            foreach (var note in entries.Where(e => !e.IsFolder))
            {
                var content = contents.GetValueOrDefault(note.Id) ?? string.Empty;
                await fileStore.WriteAsync(keys[note.Id], Encoding.UTF8.GetBytes(content), note.ModifiedAt);
            }

            var ids = entries.Select(e => (object)e.Id).ToList();
            await client.DeleteManyFromExistingAsync(database, ContentStore, ids);
            await client.DeleteManyFromExistingAsync(database, EntriesStore, ids);
        }

        if (allEntries.Count == entries.Count)
        {
            // Nobody else's notes are left in the old stores, so remove them.
            await client.DeleteStoresAsync(database, [EntriesStore, ContentStore]);
        }

        checkedOwners.Add(owner);
    }

    private static string CleanName(string name, IEnumerable<char> invalid)
    {
        var builder = new StringBuilder(name.Trim());
        foreach (var character in invalid)
        {
            builder.Replace(character, '_');
        }

        var cleaned = builder.ToString().Trim();
        return cleaned.Trim('.').Length == 0 ? "Untitled" : cleaned;
    }

    private Dictionary<string, string> BuildKeys(IReadOnlyList<NoteEntry> entries)
    {
        var byId = entries.ToDictionary(e => e.Id);
        var keys = new Dictionary<string, string>();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        string KeyFor(NoteEntry entry, HashSet<string> visiting)
        {
            if (keys.TryGetValue(entry.Id, out var existing))
            {
                return existing;
            }

            string? parentKey = null;
            if (entry.ParentId != null && byId.TryGetValue(entry.ParentId, out var parent) && parent.IsFolder && visiting.Add(entry.Id))
            {
                parentKey = KeyFor(parent, visiting);
            }

            var name = CleanName(entry.Name, InvalidCharacters(entry.Name));
            if (!entry.IsFolder)
            {
                name = noteNameService.ToNoteFileName(name);
            }

            var key = noteTreeService.GetChildKey(parentKey, name, entry.IsFolder);
            for (var copy = 2; !used.Add(key); copy++)
            {
                var suffixed = entry.IsFolder ? $"{name} ({copy})" : $"{noteNameService.WithoutExtension(name)} ({copy}){noteNameService.NoteExtension}";
                key = noteTreeService.GetChildKey(parentKey, suffixed, entry.IsFolder);
            }

            keys[entry.Id] = key;
            return key;
        }

        foreach (var entry in entries)
        {
            KeyFor(entry, []);
        }

        return keys;
    }

    private IEnumerable<char> InvalidCharacters(string name)
    {
        // Anything the name validation rejects (e.g. S3 unsafe characters allowed by older versions).
        return name.Distinct().Where(c => char.IsControl(c) || noteNameService.Validate($"a{c}a") != null);
    }
}
