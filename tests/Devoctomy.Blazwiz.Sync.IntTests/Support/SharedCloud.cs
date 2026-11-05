using Devoctomy.Blazwiz.Sync.Exceptions;
using Devoctomy.Blazwiz.Sync.Models;
using Devoctomy.Blazwiz.TestSupport.Fakes;

namespace Devoctomy.Blazwiz.Sync.IntTests.Support;

/// <summary>
/// What every device of the user shares, in memory: the keys documents and the index of protected profiles (Secrets
/// Manager), and the user's prefix in the data bucket, with S3's conditional writes.
/// </summary>
public sealed class SharedCloud
{
    private int nextETag = 1;

    /// <summary>
    /// Gets the keys documents, by profile id.
    /// </summary>
    public FakeUserKeysStore Keys { get; } = new();

    /// <summary>
    /// Gets the index of the user's protected profiles.
    /// </summary>
    public FakeE2eProfileIndexStore Index { get; } = new();

    /// <summary>
    /// Gets the objects in the user's prefix, by key.
    /// </summary>
    public Dictionary<string, RemoteObject> Objects { get; } = new(StringComparer.Ordinal);

    public string Put(string key, byte[] content, DateTimeOffset modifiedAt, string? ifMatch, bool ifNoneMatch)
    {
        var exists = Objects.TryGetValue(key, out var existing);
        if ((ifNoneMatch && exists) || (ifMatch != null && existing?.ETag != ifMatch))
        {
            throw new SyncPreconditionFailedException("412");
        }

        var etag = $"\"{nextETag++}\"";
        Objects[key] = new RemoteObject
        {
            Key = key,
            ETag = etag,
            Content = [.. content],
            LastModified = DateTimeOffset.UtcNow,
            ModifiedAt = modifiedAt,
            Size = content.Length,
        };
        return etag;
    }

    public void Delete(string key, string? ifMatch)
    {
        if (Objects.TryGetValue(key, out var existing) && ifMatch != null && existing.ETag != ifMatch)
        {
            throw new SyncPreconditionFailedException("412");
        }

        Objects.Remove(key);
    }
}
