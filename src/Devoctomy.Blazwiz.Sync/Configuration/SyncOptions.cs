using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Interfaces;

namespace Devoctomy.Blazwiz.Sync.Configuration;

/// <summary>
/// The sync configuration, built by <see cref="SyncBuilder"/>.
/// </summary>
public class SyncOptions
{
    /// <summary>
    /// Gets or sets how long after a local change a pass runs, so a burst of changes syncs once. 2 seconds by default.
    /// </summary>
    public TimeSpan ChangeDebounce { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Gets or sets the connectivity check timings.
    /// </summary>
    public ConnectivityTimings ConnectivityTimings { get; set; } = new ConnectivityTimings();

    /// <summary>
    /// Gets or sets how long log entries are kept. 30 days by default.
    /// </summary>
    public TimeSpan EventLogMaxAge { get; set; } = TimeSpan.FromDays(30);

    /// <summary>
    /// Gets or sets the most log entries kept per user. 5,000 by default.
    /// </summary>
    public int EventLogMaxEventsPerUser { get; set; } = 5000;

    /// <summary>
    /// Gets or sets the IndexedDB database holding the log.
    /// </summary>
    public string EventLogDatabase { get; set; } = SyncDefaults.IndexDatabase;

    /// <summary>
    /// Gets or sets the IndexedDB object store holding the log.
    /// </summary>
    public string EventLogStore { get; set; } = SyncDefaults.EventLogStore;

    /// <summary>
    /// Gets or sets a factory for a custom log, or null for IndexedDB.
    /// </summary>
    public Func<IServiceProvider, ISyncEventLog>? EventLogFactory { get; set; }

    /// <summary>
    /// Gets or sets the security settings of the profiles users create.
    /// </summary>
    public E2eProfileSettings ProfileDefaults { get; set; } = new E2eProfileSettings();

    /// <summary>
    /// Gets the profiles.
    /// </summary>
    public List<SyncProfileOptions> Profiles { get; } = [];
}
