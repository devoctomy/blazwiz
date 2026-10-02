using blazwiz.Core.Sync.Interfaces;
using blazwiz.Core.Sync.Models;
using blazwiz.Core.Time.Interfaces;

namespace blazwiz.Core.Sync.InMemory;

/// <summary>
/// Keeps files in memory, partitioned by user. Nothing survives a reload, so this is for tests and tools (e.g. to
/// simulate a device), not for the app's own files.
/// </summary>
public class InMemorySyncFileStore(ISyncUserContext userContext, ISystemClock systemClock)
    : ISyncFileStore
{
    private readonly ISyncUserContext userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
    private readonly ISystemClock systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));
    private readonly Dictionary<(string Owner, string Key), SyncFile> files = [];

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SyncFileInfo>> ListAsync(CancellationToken ct = default)
    {
        var owner = await GetOwnerAsync();
        return files.Where(f => f.Key.Owner == owner).Select(f => f.Value.ToInfo()).ToList();
    }

    /// <inheritdoc/>
    public async Task<SyncFile?> ReadAsync(string key, CancellationToken ct = default)
    {
        var owner = await GetOwnerAsync();
        return files.TryGetValue((owner, key), out var file)
            ? new SyncFile { Key = file.Key, Content = [.. file.Content], ModifiedAt = file.ModifiedAt }
            : null;
    }

    /// <inheritdoc/>
    public async Task WriteAsync(string key, byte[] content, DateTimeOffset? modifiedAt = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(content);

        var owner = await GetOwnerAsync();
        files[(owner, key)] = new SyncFile { Key = key, Content = [.. content], ModifiedAt = modifiedAt ?? systemClock.UtcNow };
    }

    /// <inheritdoc/>
    public Task CreateFolderAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        if (!key.EndsWith('/'))
        {
            throw new ArgumentException("Folder keys end with '/'.", nameof(key));
        }

        return WriteAsync(key, [], null, ct);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        var owner = await GetOwnerAsync();
        files.Remove((owner, key));
    }

    private async Task<string> GetOwnerAsync()
    {
        return await userContext.GetUserIdAsync()
            ?? throw new InvalidOperationException("Sign in to use your files.");
    }
}
