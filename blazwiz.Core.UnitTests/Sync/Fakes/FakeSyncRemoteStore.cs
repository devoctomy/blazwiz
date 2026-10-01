using blazwiz.Core.Sync.Exceptions;
using blazwiz.Core.Sync.Interfaces;
using blazwiz.Core.Sync.Models;

namespace blazwiz.Core.UnitTests.Sync.Fakes;

/// <summary>
/// An in-memory remote store with S3's conditional write semantics, call recording and failure injection.
/// </summary>
public sealed class FakeSyncRemoteStore : ISyncRemoteStore
{
    private int nextETag = 1;

    public Dictionary<string, RemoteObject> Objects { get; } = new(StringComparer.Ordinal);

    public List<string> Calls { get; } = [];

    public Dictionary<string, Exception> FailGet { get; } = [];

    public Exception? FailList { get; set; }

    /// <summary>
    /// Called before a put or delete is applied, e.g. to change the object "on another device" first.
    /// </summary>
    public Action<string>? BeforeWrite { get; set; }

    public DateTimeOffset UploadTime { get; set; } = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    public string Seed(string key, byte[] content, DateTimeOffset? modifiedAt)
    {
        var etag = $"\"{nextETag++}\"";
        Objects[key] = new RemoteObject
        {
            Key = key,
            ETag = etag,
            Content = [.. content],
            LastModified = UploadTime,
            ModifiedAt = modifiedAt,
            Size = content.Length,
        };
        return etag;
    }

    public Task<IReadOnlyList<RemoteObjectInfo>> ListAsync(CancellationToken ct = default)
    {
        Calls.Add("list");
        if (FailList != null)
        {
            throw FailList;
        }

        IReadOnlyList<RemoteObjectInfo> result = Objects.Values
            .Select(o => new RemoteObjectInfo { Key = o.Key, ETag = o.ETag, LastModified = o.LastModified, Size = o.Size })
            .ToList();
        return Task.FromResult(result);
    }

    public Task<RemoteObject?> GetAsync(string key, CancellationToken ct = default)
    {
        Calls.Add($"get {key}");
        if (FailGet.TryGetValue(key, out var failure))
        {
            throw failure;
        }

        return Task.FromResult(Objects.TryGetValue(key, out var o) ? Copy(o) : null);
    }

    public Task<RemoteObjectInfo?> GetInfoAsync(string key, CancellationToken ct = default)
    {
        Calls.Add($"head {key}");
        return Task.FromResult<RemoteObjectInfo?>(Objects.TryGetValue(key, out var o)
            ? new RemoteObjectInfo { Key = o.Key, ETag = o.ETag, LastModified = o.LastModified, ModifiedAt = o.ModifiedAt, Size = o.Size }
            : null);
    }

    public Task<string> PutAsync(string key, byte[] content, DateTimeOffset modifiedAt, string? ifMatch, bool ifNoneMatch, CancellationToken ct = default)
    {
        Calls.Add($"put {key}");
        BeforeWrite?.Invoke(key);
        var exists = Objects.TryGetValue(key, out var existing);
        if ((ifNoneMatch && exists) || (ifMatch != null && existing?.ETag != ifMatch))
        {
            throw new SyncPreconditionFailedException("412");
        }

        return Task.FromResult(Seed(key, content, modifiedAt));
    }

    public Task DeleteAsync(string key, string? ifMatch, CancellationToken ct = default)
    {
        Calls.Add($"delete {key}");
        BeforeWrite?.Invoke(key);
        if (Objects.TryGetValue(key, out var existing) && ifMatch != null && existing.ETag != ifMatch)
        {
            throw new SyncPreconditionFailedException("412");
        }

        Objects.Remove(key);
        return Task.CompletedTask;
    }

    private static RemoteObject Copy(RemoteObject o) => new()
    {
        Key = o.Key,
        ETag = o.ETag,
        Content = [.. o.Content],
        LastModified = o.LastModified,
        ModifiedAt = o.ModifiedAt,
        Size = o.Size,
    };
}
