using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;

namespace Devoctomy.Blazwiz.Sync;

/// <summary>
/// Implementation of <see cref="ISyncFileHistoryService"/> over each profile's remote store, decryptor and file store,
/// from the <see cref="ISyncProfileRegistry"/>.
/// </summary>
/// <param name="registry">The profiles' stores.</param>
/// <param name="targetProvider">Whether there is a bucket.</param>
/// <param name="userContext">The signed in user, whose index says which version this device last synced.</param>
public class SyncFileHistoryService(ISyncProfileRegistry registry, IS3SyncTargetProvider targetProvider, ISyncUserContext userContext)
    : ISyncFileHistoryService
{
    private readonly ISyncProfileRegistry registry = registry ?? throw new ArgumentNullException(nameof(registry));
    private readonly IS3SyncTargetProvider targetProvider = targetProvider ?? throw new ArgumentNullException(nameof(targetProvider));
    private readonly ISyncUserContext userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));

    /// <inheritdoc/>
    public bool IsAvailable => targetProvider.IsConfigured;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SyncFileVersion>> GetVersionsAsync(string profile, string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profile);
        ArgumentException.ThrowIfNullOrEmpty(key);

        var syncProfile = registry.GetSyncProfile(profile);
        var versions = await syncProfile.RemoteStore.ListVersionsAsync(key, ct);
        var syncedETag = await GetSyncedETagAsync(syncProfile, key, ct);
        return versions.Select(v => new SyncFileVersion
        {
            Profile = profile,
            Key = key,
            VersionId = v.VersionId,
            ETag = v.ETag,
            UploadedAt = v.LastModified,
            Size = v.Size,
            IsLatest = v.IsLatest,
            IsSynced = syncedETag != null && v.ETag == syncedETag,
        }).ToList();
    }

    /// <inheritdoc/>
    public async Task<SyncFileVersionContent?> ReadVersionAsync(SyncFileVersion version, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(version);

        var remote = await registry.GetSyncProfile(version.Profile).RemoteStore.GetVersionAsync(version.Key, version.VersionId, ct);
        if (remote == null)
        {
            return null;
        }

        return new SyncFileVersionContent
        {
            Version = version,
            Content = await registry.GetFileDecryptor(version.Profile).DecryptAsync(version.Key, remote.Content),
        };
    }

    /// <inheritdoc/>
    public Task RestoreAsync(SyncFileVersionContent version, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(version);

        // An ordinary save: the tracked store marks it changed and asks for a sync.
        return registry.GetFileStore(version.Version.Profile).WriteAsync(version.Version.Key, version.Content, ct: ct);
    }

    private async Task<string?> GetSyncedETagAsync(SyncProfile syncProfile, string key, CancellationToken ct)
    {
        var owner = await userContext.GetUserIdAsync();
        if (owner == null)
        {
            return null;
        }

        var record = await syncProfile.IndexStore.GetAsync(syncProfile.Name, owner, key, ct);
        return record?.BaseETag;
    }
}
