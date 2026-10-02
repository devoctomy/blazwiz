using blazwiz.Core.Sync.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace blazwiz.Core.Sync.Configuration;

/// <summary>
/// Configures sync: the log, timings and profiles. Used by <c>AddBlazwizSync</c>.
/// </summary>
/// <param name="options">The options to configure.</param>
public class SyncBuilder(SyncOptions options)
{
    /// <summary>
    /// Gets the options being configured.
    /// </summary>
    public SyncOptions Options { get; } = options ?? throw new ArgumentNullException(nameof(options));

    /// <summary>
    /// Keeps the sync log in IndexedDB.
    /// </summary>
    /// <param name="database">The database name.</param>
    /// <param name="store">The object store name.</param>
    /// <returns>The builder.</returns>
    public SyncBuilder UseIndexedDbEventLog(string database, string store)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(database);
        ArgumentException.ThrowIfNullOrWhiteSpace(store);

        Options.EventLogDatabase = database;
        Options.EventLogStore = store;
        Options.EventLogFactory = null;
        return this;
    }

    /// <summary>
    /// Keeps the sync log in a custom store, created from the service provider.
    /// </summary>
    /// <typeparam name="T">The log type.</typeparam>
    /// <returns>The builder.</returns>
    public SyncBuilder UseEventLog<T>()
        where T : class, ISyncEventLog
    {
        Options.EventLogFactory = sp => ActivatorUtilities.CreateInstance<T>(sp);
        return this;
    }

    /// <summary>
    /// Sets how long sync log entries are kept, and the most kept per user. Pruned after each pass.
    /// </summary>
    /// <param name="maxAge">The longest an entry is kept.</param>
    /// <param name="maxEventsPerUser">The most entries kept per user.</param>
    /// <returns>The builder.</returns>
    public SyncBuilder UseEventLogRetention(TimeSpan maxAge, int maxEventsPerUser)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxAge, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxEventsPerUser, 1);

        Options.EventLogMaxAge = maxAge;
        Options.EventLogMaxEventsPerUser = maxEventsPerUser;
        return this;
    }

    /// <summary>
    /// Sets how long after a local change a pass runs.
    /// </summary>
    /// <param name="debounce">The delay.</param>
    /// <returns>The builder.</returns>
    public SyncBuilder UseChangeDebounce(TimeSpan debounce)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(debounce, TimeSpan.Zero);

        Options.ChangeDebounce = debounce;
        return this;
    }

    /// <summary>
    /// Sets the connectivity check timings.
    /// </summary>
    /// <param name="configure">Changes the timings.</param>
    /// <returns>The builder.</returns>
    public SyncBuilder UseConnectivityTimings(Action<ConnectivityTimings> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        configure(Options.ConnectivityTimings);
        if (Options.ConnectivityTimings.RecheckBackoff is not { Count: > 0 })
        {
            throw new ArgumentException("At least one recheck delay is required.", nameof(configure));
        }

        return this;
    }

    /// <summary>
    /// Adds a profile: one set of index store, file store and remote pattern.
    /// </summary>
    /// <param name="name">The profile name, e.g. <c>notes</c>. App code gets its file store with this service key.</param>
    /// <param name="configure">Configures the profile. <see cref="SyncProfileBuilder.UseRemote"/> is required.</param>
    /// <returns>The builder.</returns>
    public SyncBuilder AddProfile(string name, Action<SyncProfileBuilder> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(configure);
        if (Options.Profiles.Any(p => p.Name == name))
        {
            throw new ArgumentException($"A sync profile called '{name}' has already been added.", nameof(name));
        }

        var profile = new SyncProfileOptions { Name = name };
        configure(new SyncProfileBuilder(profile));
        if (string.IsNullOrWhiteSpace(profile.RemotePattern))
        {
            throw new ArgumentException($"Sync profile '{name}' needs a remote pattern (UseRemote).", nameof(configure));
        }

        Options.Profiles.Add(profile);
        return this;
    }
}
