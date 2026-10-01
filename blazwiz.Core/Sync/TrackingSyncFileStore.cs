using blazwiz.Core.Sync.Interfaces;
using blazwiz.Core.Sync.Models;
using blazwiz.Core.Time.Interfaces;

namespace blazwiz.Core.Sync;

/// <summary>
/// Wraps a profile's file store so every write marks the file dirty in the index, every delete leaves a tombstone,
/// and both ask the scheduler for a sync. This is the store app code is given, so it cannot forget to track changes.
/// </summary>
/// <remarks>
/// The file and index may be in different databases, so they cannot share a transaction. The file is always written
/// first: a crash before the index is marked leaves a file whose hash differs from the index, which the verify pass
/// after the next sign in finds and uploads.
/// </remarks>
public class TrackingSyncFileStore(
    string profile,
    ISyncFileStore inner,
    ISyncIndexStore indexStore,
    ISyncUserContext userContext,
    ISyncScheduler scheduler,
    ISystemClock systemClock)
    : ISyncFileStore
{
    private readonly string profile = string.IsNullOrWhiteSpace(profile) ? throw new ArgumentException("A profile is required.", nameof(profile)) : profile;
    private readonly ISyncFileStore inner = inner ?? throw new ArgumentNullException(nameof(inner));
    private readonly ISyncIndexStore indexStore = indexStore ?? throw new ArgumentNullException(nameof(indexStore));
    private readonly ISyncUserContext userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
    private readonly ISyncScheduler scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
    private readonly ISystemClock systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));

    /// <inheritdoc/>
    public Task<IReadOnlyList<SyncFileInfo>> ListAsync(CancellationToken ct = default) => inner.ListAsync(ct);

    /// <inheritdoc/>
    public Task<SyncFile?> ReadAsync(string key, CancellationToken ct = default) => inner.ReadAsync(key, ct);

    /// <inheritdoc/>
    public async Task WriteAsync(string key, byte[] content, DateTimeOffset? modifiedAt = null, CancellationToken ct = default)
    {
        await inner.WriteAsync(key, content, modifiedAt, ct);
        await TrackAsync(key, deleted: false, ct);
    }

    /// <inheritdoc/>
    public async Task CreateFolderAsync(string key, CancellationToken ct = default)
    {
        await inner.CreateFolderAsync(key, ct);
        await TrackAsync(key, deleted: false, ct);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        await inner.DeleteAsync(key, ct);
        await TrackAsync(key, deleted: true, ct);
    }

    private async Task TrackAsync(string key, bool deleted, CancellationToken ct)
    {
        var owner = await userContext.GetUserIdAsync()
            ?? throw new InvalidOperationException("Sign in to use your files.");
        var record = await indexStore.GetAsync(profile, owner, key, ct)
            ?? new SyncIndexRecord { Profile = profile, Owner = owner, Key = key };

        record.Revision++;
        record.Dirty = !deleted;
        record.Deleted = deleted;
        record.DeletedAt = deleted ? systemClock.UtcNow : null;
        await indexStore.SaveAsync(record, ct);

        scheduler.RequestSync(profile);
    }
}
