using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Models.Notes;
using Microsoft.JSInterop;

namespace blazwiz.Portal.Application.Services;

/// <summary>
/// Keeps note content in the browser's IndexedDB, using the functions in <c>wwwroot/js/notes.js</c>.
/// </summary>
public class IndexedDbNoteContentStore(IJSRuntime jsRuntime)
    : INoteContentStore
{
    /// <summary>
    /// The object store holding <see cref="NoteContent"/> records, keyed by note id.
    /// </summary>
    public const string ContentStore = "content";

    private readonly IJSRuntime jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));

    /// <inheritdoc/>
    public string ProviderName => NoteStorageProviders.IndexedDb;

    /// <inheritdoc/>
    public async Task<string?> ReadAsync(string id)
    {
        var record = await jsRuntime.InvokeAsync<NoteContent?>("notesInterop.db.get", ContentStore, id);
        return record?.Content;
    }

    /// <inheritdoc/>
    public async Task WriteAsync(string id, string content)
    {
        await jsRuntime.InvokeVoidAsync("notesInterop.db.put", ContentStore, new NoteContent { Id = id, Content = content });
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string id)
    {
        await jsRuntime.InvokeVoidAsync("notesInterop.db.deleteMany", ContentStore, new[] { id });
    }
}
