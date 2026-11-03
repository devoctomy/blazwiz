using Devoctomy.Blazwiz.Sync.IndexedDb;
using Devoctomy.Blazwiz.Sync.Interfaces;
using MyApp.Portal.Application.Services.Interfaces;
using MyApp.Portal.Models.Notes;

namespace MyApp.Portal.Application.Services;

/// <summary>
/// Keeps <see cref="NotesState"/> records in the <c>state</c> object store of the notes database, keyed by owner.
/// </summary>
public class IndexedDbNotesStateStore(IIndexedDbClient client)
    : INotesStateStore
{
    /// <summary>
    /// The IndexedDB database holding the notes files and state.
    /// </summary>
    public const string Database = "notes";

    /// <summary>
    /// The object store holding the state.
    /// </summary>
    public static readonly IndexedDbStore Store = new IndexedDbStore(Database, "state", ["owner"]);

    private readonly IIndexedDbClient client = client ?? throw new ArgumentNullException(nameof(client));

    /// <inheritdoc/>
    public Task<NotesState?> GetAsync(string owner)
    {
        ArgumentException.ThrowIfNullOrEmpty(owner);

        return client.GetAsync<NotesState>(Store, owner);
    }

    /// <inheritdoc/>
    public Task SaveAsync(NotesState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return client.PutAsync(Store, state);
    }
}
