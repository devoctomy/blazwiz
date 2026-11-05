using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.Sync.Interfaces;
using MyApp.Portal.Application.Services.Interfaces;
using MyApp.Portal.Models.Notes;

namespace MyApp.Portal.Application.Services;

/// <summary>
/// Implementation of <see cref="INoteServiceFactory"/>: a <see cref="NoteService"/> over the notebook's sync profile's
/// file store, which encrypts and decrypts with the notebook's own keys.
/// </summary>
/// <param name="registry">The sync profiles, for each notebook's file store.</param>
/// <param name="authService">The signed in user.</param>
/// <param name="stateStore">Whether the welcome note was created.</param>
/// <param name="syncService">Whether a sync has run.</param>
/// <param name="connectivityService">Whether a sync can run.</param>
/// <param name="noteNameService">Note names.</param>
/// <param name="noteTreeService">Keys and the tree of folders and notes.</param>
/// <param name="timeProvider">The clock.</param>
public class NoteServiceFactory(
    ISyncProfileRegistry registry,
    IAuthenticationService authService,
    INotesStateStore stateStore,
    ISyncService syncService,
    IConnectivityService connectivityService,
    INoteNameService noteNameService,
    INoteTreeService noteTreeService,
    TimeProvider timeProvider)
    : INoteServiceFactory
{
    private readonly ISyncProfileRegistry registry = registry ?? throw new ArgumentNullException(nameof(registry));
    private readonly IAuthenticationService authService = authService ?? throw new ArgumentNullException(nameof(authService));
    private readonly INotesStateStore stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
    private readonly ISyncService syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));
    private readonly IConnectivityService connectivityService = connectivityService ?? throw new ArgumentNullException(nameof(connectivityService));
    private readonly INoteNameService noteNameService = noteNameService ?? throw new ArgumentNullException(nameof(noteNameService));
    private readonly INoteTreeService noteTreeService = noteTreeService ?? throw new ArgumentNullException(nameof(noteTreeService));
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc/>
    public INoteService Create(Notebook notebook)
    {
        ArgumentNullException.ThrowIfNull(notebook);

        return new NoteService(
            notebook,
            registry.GetFileStore(notebook.Name),
            authService,
            stateStore,
            syncService,
            connectivityService,
            noteNameService,
            noteTreeService,
            timeProvider);
    }
}
