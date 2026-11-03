using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;

namespace Devoctomy.Blazwiz.Sync.InMemory;

/// <summary>
/// Keeps a sync index in memory. Nothing survives a reload, so this is for tests and tools.
/// </summary>
public class InMemorySyncIndexStore : ISyncIndexStore
{
    private readonly Dictionary<(string Profile, string Owner, string Key), SyncIndexRecord> records = [];

    /// <summary>
    /// Gets a copy of every record, for inspection.
    /// </summary>
    public IReadOnlyList<SyncIndexRecord> Records => records.Values.Select(r => r.Clone()).ToList();

    /// <inheritdoc/>
    public Task<IReadOnlyList<SyncIndexRecord>> GetAllAsync(string profile, string owner, CancellationToken ct = default)
    {
        IReadOnlyList<SyncIndexRecord> result = records.Values
            .Where(r => r.Profile == profile && r.Owner == owner)
            .Select(r => r.Clone())
            .ToList();
        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task<SyncIndexRecord?> GetAsync(string profile, string owner, string key, CancellationToken ct = default)
    {
        return Task.FromResult(records.TryGetValue((profile, owner, key), out var record) ? record.Clone() : null);
    }

    /// <inheritdoc/>
    public Task SaveAsync(SyncIndexRecord record, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        records[(record.Profile, record.Owner, record.Key)] = record.Clone();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task DeleteAsync(string profile, string owner, string key, CancellationToken ct = default)
    {
        records.Remove((profile, owner, key));
        return Task.CompletedTask;
    }

    /// <summary>
    /// Removes every record, e.g. to simulate a lost index.
    /// </summary>
    public void Clear() => records.Clear();
}
