using blazwiz.Core.Sync.Exceptions;
using blazwiz.Core.Sync.Interfaces;
using blazwiz.Core.Sync.Models;

namespace blazwiz.Core.Sync.Storage;

/// <summary>
/// A profile's file store, without change tracking, that passes each call to the storage the signed in user's profile
/// uses on this device. Changes are refused while files are being copied to another storage, so none are left behind.
/// </summary>
public class SwitchableSyncFileStore(string profile, ISyncStorageResolver resolver)
    : ISyncFileStore
{
    /// <summary>
    /// The message of the exception thrown for a change made while files are being copied.
    /// </summary>
    public const string MigratingMessage = "Your files are being moved to another storage. Try again in a moment.";

    private readonly string profile = string.IsNullOrWhiteSpace(profile) ? throw new ArgumentException("A profile is required.", nameof(profile)) : profile;
    private readonly ISyncStorageResolver resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SyncFileInfo>> ListAsync(CancellationToken ct = default)
    {
        return await (await GetStoreAsync(change: false)).ListAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<SyncFile?> ReadAsync(string key, CancellationToken ct = default)
    {
        return await (await GetStoreAsync(change: false)).ReadAsync(key, ct);
    }

    /// <inheritdoc/>
    public async Task WriteAsync(string key, byte[] content, DateTimeOffset? modifiedAt = null, CancellationToken ct = default)
    {
        await (await GetStoreAsync(change: true)).WriteAsync(key, content, modifiedAt, ct);
    }

    /// <inheritdoc/>
    public async Task CreateFolderAsync(string key, CancellationToken ct = default)
    {
        await (await GetStoreAsync(change: true)).CreateFolderAsync(key, ct);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        await (await GetStoreAsync(change: true)).DeleteAsync(key, ct);
    }

    private async Task<ISyncFileStore> GetStoreAsync(bool change)
    {
        var state = await resolver.GetStateAsync(profile);
        if (change && state.IsCopying)
        {
            throw new SyncStorageMigrationException(MigratingMessage);
        }

        return resolver.GetStore(profile, state.Kind);
    }
}
