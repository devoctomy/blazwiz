using Devoctomy.Blazwiz.Sync.Configuration;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;

namespace Devoctomy.Blazwiz.Sync.IndexedDb;

/// <summary>
/// Keeps the sync log in an IndexedDB object store, indexed by owner. Searching and filtering run in .NET over the
/// user's entries, which retention keeps to a manageable number.
/// </summary>
public class IndexedDbSyncEventLog(
    IIndexedDbClient client,
    ISyncUserContext userContext,
    TimeProvider timeProvider,
    SyncOptions options,
    IndexedDbStore store)
    : ISyncEventLog
{
    /// <summary>
    /// The index of entries by owner.
    /// </summary>
    public const string OwnerIndex = "owner";

    /// <summary>
    /// The index of entries by owner, profile and time, used to find the last pass of a profile.
    /// </summary>
    public const string OwnerProfileTimeIndex = "owner_profile_timestamp";

    private readonly IIndexedDbClient client = client ?? throw new ArgumentNullException(nameof(client));
    private readonly ISyncUserContext userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    private readonly SyncOptions options = options ?? throw new ArgumentNullException(nameof(options));

    /// <inheritdoc/>
    public event EventHandler? EventsWritten;

    /// <summary>
    /// Gets the object store the log is kept in.
    /// </summary>
    public IndexedDbStore Store { get; } = store ?? throw new ArgumentNullException(nameof(store));

    /// <summary>
    /// Describes the object store for a database and store name.
    /// </summary>
    /// <param name="database">The database.</param>
    /// <param name="name">The store name.</param>
    /// <returns>The store description.</returns>
    public static IndexedDbStore DescribeStore(string database, string name) =>
        new IndexedDbStore(database, name, ["id"], [new IndexedDbIndex(OwnerIndex, ["owner"]), new IndexedDbIndex(OwnerProfileTimeIndex, ["owner", "profile", "timestamp"])]);

    /// <inheritdoc/>
    public async Task WriteAsync(IEnumerable<SyncEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        var list = events.ToList();
        if (list.Count == 0)
        {
            return;
        }

        await client.PutManyAsync(Store, list);
        EventsWritten?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    public async Task RecordNoChangePassAsync(string owner, string profile, string passId, DateTimeOffset timestamp)
    {
        ArgumentException.ThrowIfNullOrEmpty(owner);
        ArgumentNullException.ThrowIfNull(profile);

        var last = await client.GetLastByIndexAsync<SyncEvent>(
            Store,
            OwnerProfileTimeIndex,
            new object[] { owner, profile, string.Empty },
            new object[] { owner, profile, "￿" });

        SyncEvent entry;
        if (last is { Kind: SyncEventKind.PassCompleted, IsNoChangePass: true })
        {
            entry = last;
            entry.Count++;
            entry.Timestamp = timestamp;
            entry.PassId = passId;
        }
        else
        {
            entry = new SyncEvent
            {
                Owner = owner,
                Profile = profile,
                PassId = passId,
                Timestamp = timestamp,
                Kind = SyncEventKind.PassCompleted,
                Code = SyncEventCodes.NoChanges,
                IsNoChangePass = true,
            };
        }

        await client.PutAsync(Store, entry);
        EventsWritten?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    public async Task<SyncEventPage> QueryAsync(SyncEventQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var matches = (await GetOwnEventsAsync()).Where(e => Matches(e, query)).ToList();
        var page = matches.Skip(Math.Max(0, query.Skip)).Take(Math.Max(0, query.Take)).ToList();
        return new SyncEventPage
        {
            Events = page,
            HasMore = matches.Count > query.Skip + page.Count,
        };
    }

    /// <inheritdoc/>
    public async Task ClearAsync()
    {
        var events = await GetOwnEventsAsync();
        if (events.Count > 0)
        {
            await client.DeleteManyAsync(Store, events.Select(e => (object)e.Id));
        }

        EventsWritten?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    public async Task PruneAsync()
    {
        var events = await GetOwnEventsAsync();
        var cutoff = timeProvider.GetUtcNow() - options.EventLogMaxAge;
        var expired = events
            .Where((e, position) => e.Timestamp < cutoff || position >= options.EventLogMaxEventsPerUser)
            .Select(e => (object)e.Id)
            .ToList();

        if (expired.Count > 0)
        {
            await client.DeleteManyAsync(Store, expired);
        }
    }

    private static bool Matches(SyncEvent entry, SyncEventQuery query)
    {
        return MatchesFilters(entry, query) && MatchesText(entry, query);
    }

    private static bool MatchesFilters(SyncEvent entry, SyncEventQuery query)
    {
        if (query.Profile != null && entry.Profile != query.Profile)
        {
            return false;
        }

        if (query.Kinds != null && !query.Kinds.Contains(entry.Kind))
        {
            return false;
        }

        return !((query.From != null && entry.Timestamp < query.From) || (query.To != null && entry.Timestamp > query.To));
    }

    private static bool MatchesText(SyncEvent entry, SyncEventQuery query)
    {
        if (string.IsNullOrWhiteSpace(query.Text))
        {
            return true;
        }

        var text = query.Text.Trim();
        return Contains(entry.Key, text) || Contains(entry.Detail, text) || Contains(entry.Profile, text) ||
            (query.Describe != null && Contains(query.Describe(entry), text));
    }

    private static bool Contains(string? value, string text) => value?.Contains(text, StringComparison.OrdinalIgnoreCase) == true;

    private async Task<IReadOnlyList<SyncEvent>> GetOwnEventsAsync()
    {
        var owner = await userContext.GetUserIdAsync();
        if (owner == null)
        {
            return [];
        }

        var events = await client.GetAllByIndexAsync<SyncEvent>(Store, OwnerIndex, owner);
        return events
            .OrderByDescending(e => e.Timestamp)
            .ThenByDescending(e => e.Kind == SyncEventKind.PassCompleted)
            .ToList();
    }
}
