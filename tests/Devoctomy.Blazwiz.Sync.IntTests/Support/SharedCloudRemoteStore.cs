using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;

namespace Devoctomy.Blazwiz.Sync.IntTests.Support;

/// <summary>
/// A profile's view of the shared bucket: the objects its pattern matches, as the S3 remote store lists them.
/// </summary>
public sealed class SharedCloudRemoteStore(SharedCloud cloud, ISyncPathMatcher matcher)
    : ISyncRemoteStore
{
    public Task<IReadOnlyList<RemoteObjectInfo>> ListAsync(CancellationToken ct = default)
    {
        IReadOnlyList<RemoteObjectInfo> result = cloud.Objects.Values
            .Where(o => matcher.IsMatch(o.Key))
            .Select(o => new RemoteObjectInfo { Key = o.Key, ETag = o.ETag, LastModified = o.LastModified, ModifiedAt = o.ModifiedAt, Size = o.Size })
            .ToList();
        return Task.FromResult(result);
    }

    public Task<RemoteObject?> GetAsync(string key, CancellationToken ct = default)
    {
        return Task.FromResult(cloud.Objects.TryGetValue(key, out var o)
            ? new RemoteObject { Key = o.Key, ETag = o.ETag, Content = [.. o.Content], LastModified = o.LastModified, ModifiedAt = o.ModifiedAt, Size = o.Size }
            : null);
    }

    public Task<RemoteObjectInfo?> GetInfoAsync(string key, CancellationToken ct = default)
    {
        return Task.FromResult(cloud.Objects.TryGetValue(key, out var o)
            ? new RemoteObjectInfo { Key = o.Key, ETag = o.ETag, LastModified = o.LastModified, ModifiedAt = o.ModifiedAt, Size = o.Size }
            : null);
    }

    public Task<string> PutAsync(string key, byte[] content, DateTimeOffset modifiedAt, string? ifMatch, bool ifNoneMatch, CancellationToken ct = default)
    {
        return Task.FromResult(cloud.Put(key, content, modifiedAt, ifMatch, ifNoneMatch));
    }

    public Task DeleteAsync(string key, string? ifMatch, CancellationToken ct = default)
    {
        cloud.Delete(key, ifMatch);
        return Task.CompletedTask;
    }
}
