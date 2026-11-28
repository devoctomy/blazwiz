using Devoctomy.Blazwiz.Sync.Exceptions;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;

namespace Devoctomy.Blazwiz.Sync.UnitTests.Fakes;

/// <summary>
/// An in-memory remote store with S3's conditional write semantics and versioning, call recording and failure injection.
/// </summary>
public sealed class FakeSyncRemoteStore : ISyncRemoteStore
{
    private int nextETag = 1;

    public Dictionary<string, RemoteObject> Objects { get; } = new(StringComparer.Ordinal);

    // Every version uploaded of each key, newest first; a delete keeps them.
    public Dictionary<string, List<(string VersionId, RemoteObject Object)>> Versions { get; } = new(StringComparer.Ordinal);

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
        var uploaded = new RemoteObject
        {
            Key = key,
            ETag = etag,
            Content = [.. content],
            LastModified = UploadTime,
            ModifiedAt = modifiedAt,
            Size = content.Length,
        };
        Objects[key] = uploaded;
        if (!Versions.TryGetValue(key, out var versions))
        {
            versions = [];
            Versions[key] = versions;
        }

        versions.Insert(0, ($"v{etag.Trim('"')}", uploaded));
        return etag;
    }

    public Task<IReadOnlyList<RemoteObjectVersion>> ListVersionsAsync(string key, CancellationToken ct = default)
    {
        Calls.Add($"versions {key}");
        if (FailList != null)
        {
            throw FailList;
        }

        IReadOnlyList<RemoteObjectVersion> result = Versions.TryGetValue(key, out var versions)
            ? versions.Select(v => new RemoteObjectVersion
            {
                Key = key,
                VersionId = v.VersionId,
                ETag = v.Object.ETag,
                LastModified = v.Object.LastModified,
                Size = v.Object.Size,
                IsLatest = Objects.TryGetValue(key, out var current) && ReferenceEquals(current, v.Object),
            }).ToList()
            : [];
        return Task.FromResult(result);
    }

    public Task<RemoteObject?> GetVersionAsync(string key, string versionId, CancellationToken ct = default)
    {
        Calls.Add($"get {key} {versionId}");
        var found = Versions.TryGetValue(key, out var versions) ? versions.FirstOrDefault(v => v.VersionId == versionId).Object : null;
        return Task.FromResult(found == null ? null : Copy(found));
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
