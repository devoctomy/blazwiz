using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Models.Notes;
using Microsoft.JSInterop;

namespace blazwiz.Portal.Application.Services;

/// <summary>
/// Keeps the notes file index in the browser's IndexedDB, using the functions in <c>wwwroot/js/notes.js</c>.
/// </summary>
public class IndexedDbNoteIndexStore(IJSRuntime jsRuntime)
    : INoteIndexStore
{
    /// <summary>
    /// The object store holding <see cref="NoteEntry"/> records, indexed by owner.
    /// </summary>
    public const string EntriesStore = "entries";

    /// <summary>
    /// The object store holding <see cref="NotesState"/> records, keyed by owner.
    /// </summary>
    public const string StateStore = "state";

    private readonly IJSRuntime jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));

    /// <inheritdoc/>
    public async Task<IReadOnlyList<NoteEntry>> GetEntriesAsync(string owner)
    {
        return await jsRuntime.InvokeAsync<List<NoteEntry>>("notesInterop.db.getAllByOwner", EntriesStore, owner);
    }

    /// <inheritdoc/>
    public async Task SaveEntryAsync(NoteEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        await jsRuntime.InvokeVoidAsync("notesInterop.db.put", EntriesStore, entry);
    }

    /// <inheritdoc/>
    public async Task DeleteEntriesAsync(IEnumerable<string> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);

        await jsRuntime.InvokeVoidAsync("notesInterop.db.deleteMany", EntriesStore, ids.ToArray());
    }

    /// <inheritdoc/>
    public async Task<NotesState?> GetStateAsync(string owner)
    {
        return await jsRuntime.InvokeAsync<NotesState?>("notesInterop.db.get", StateStore, owner);
    }

    /// <inheritdoc/>
    public async Task SaveStateAsync(NotesState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        await jsRuntime.InvokeVoidAsync("notesInterop.db.put", StateStore, state);
    }
}
