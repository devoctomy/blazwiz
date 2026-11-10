using Devoctomy.Blazwiz.Sync.Configuration;
using Devoctomy.Blazwiz.Sync.Interfaces;

namespace Devoctomy.Blazwiz.Sync.S3;

/// <summary>
/// <see cref="ISyncScratchStore"/> in the user's prefix of the data bucket: <c>&lt;identity id&gt;/scratch/&lt;name&gt;</c>.
/// </summary>
/// <param name="targetProvider">Supplies the S3 client, bucket and the user's prefix.</param>
/// <param name="timeProvider">Dates each upload.</param>
public class S3SyncScratchStore(IS3SyncTargetProvider targetProvider, TimeProvider timeProvider)
    : ISyncScratchStore
{
    private readonly S3SyncRemoteStore store = new S3SyncRemoteStore(
        targetProvider ?? throw new ArgumentNullException(nameof(targetProvider)),
        new GlobSyncPathMatcher(SyncDefaults.ScratchFolder + "/*", includeEmptyFolders: false));

    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc/>
    public Task PutAsync(string name, byte[] content, CancellationToken ct = default)
    {
        return store.PutAsync(Key(name), content, timeProvider.GetUtcNow(), ifMatch: null, ifNoneMatch: false, ct);
    }

    /// <inheritdoc/>
    public Task DeleteAsync(string name, CancellationToken ct = default)
    {
        return store.DeleteAsync(Key(name), ifMatch: null, ct);
    }

    private static string Key(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Contains('/', StringComparison.Ordinal))
        {
            throw new ArgumentException("A scratch object's name cannot contain '/'.", nameof(name));
        }

        return $"{SyncDefaults.ScratchFolder}/{name}";
    }
}
