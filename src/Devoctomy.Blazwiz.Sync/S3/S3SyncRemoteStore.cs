using System.Globalization;
using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Devoctomy.Blazwiz.Sync.Exceptions;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;

namespace Devoctomy.Blazwiz.Sync.S3;

/// <summary>
/// The remote side of a profile in S3 (AWSSDK.S3), under the user's root prefix. Uploads and deletes are conditional
/// (<c>If-Match</c> / <c>If-None-Match</c>), and each upload records the file's edit time in
/// <c>x-amz-meta-modified-at</c>. A file's past versions are listed and read from the bucket's versioning.
/// </summary>
public class S3SyncRemoteStore(IS3SyncTargetProvider targetProvider, ISyncPathMatcher matcher)
    : ISyncRemoteStore
{
    /// <summary>
    /// The metadata header holding the file's edit time (ISO 8601).
    /// </summary>
    public const string ModifiedAtMetadata = "x-amz-meta-modified-at";

    private readonly IS3SyncTargetProvider targetProvider = targetProvider ?? throw new ArgumentNullException(nameof(targetProvider));
    private readonly ISyncPathMatcher matcher = matcher ?? throw new ArgumentNullException(nameof(matcher));

    /// <inheritdoc/>
    public Task<IReadOnlyList<RemoteObjectInfo>> ListAsync(CancellationToken ct = default)
    {
        return CallAsync<IReadOnlyList<RemoteObjectInfo>>(async target =>
        {
            var results = new List<RemoteObjectInfo>();
            var request = new ListObjectsV2Request
            {
                BucketName = target.Bucket,
                Prefix = target.RootPrefix + matcher.Prefix,
            };

            do
            {
                var response = await target.Client.ListObjectsV2Async(request, ct);
                foreach (var item in response.S3Objects ?? [])
                {
                    var key = item.Key[target.RootPrefix.Length..];
                    if (matcher.IsMatch(key))
                    {
                        results.Add(new RemoteObjectInfo
                        {
                            Key = key,
                            ETag = item.ETag,
                            LastModified = ToOffset(item.LastModified),
                            Size = item.Size ?? 0,
                        });
                    }
                }

                request.ContinuationToken = response.IsTruncated == true ? response.NextContinuationToken : null;
            }
            while (request.ContinuationToken != null);

            return results;
        });
    }

    /// <inheritdoc/>
    public Task<RemoteObject?> GetAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        return DownloadAsync(key, null, ct);
    }

    /// <inheritdoc/>
    public Task<RemoteObjectInfo?> GetInfoAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        return CallAsync(async target =>
        {
            try
            {
                var response = await target.Client.GetObjectMetadataAsync(target.Bucket, target.RootPrefix + key, ct);
                return new RemoteObjectInfo
                {
                    Key = key,
                    ETag = response.ETag,
                    LastModified = ToOffset(response.LastModified),
                    ModifiedAt = ReadModifiedAt(response.Metadata),
                    Size = response.ContentLength,
                };
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return (RemoteObjectInfo?)null;
            }
        });
    }

    /// <inheritdoc/>
    public Task<string> PutAsync(string key, byte[] content, DateTimeOffset modifiedAt, string? ifMatch, bool ifNoneMatch, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(content);

        return CallAsync(async target =>
        {
            using var stream = new MemoryStream(content, writable: false);
            var request = new PutObjectRequest
            {
                BucketName = target.Bucket,
                Key = target.RootPrefix + key,
                InputStream = stream,
                AutoCloseStream = false,

                // Browsers cannot send aws-chunked bodies, so sign the whole (in memory) payload instead.
                UseChunkEncoding = false,
            };
            request.Metadata[ModifiedAtMetadata] = modifiedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
            if (ifMatch != null)
            {
                request.IfMatch = ifMatch;
            }

            if (ifNoneMatch)
            {
                request.IfNoneMatch = "*";
            }

            var response = await target.Client.PutObjectAsync(request, ct);
            return response.ETag;
        });
    }

    /// <inheritdoc/>
    public Task DeleteAsync(string key, string? ifMatch, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        return CallAsync(async target =>
        {
            var request = new DeleteObjectRequest
            {
                BucketName = target.Bucket,
                Key = target.RootPrefix + key,
            };
            if (ifMatch != null)
            {
                request.IfMatch = ifMatch;
            }

            try
            {
                await target.Client.DeleteObjectAsync(request, ct);
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Already gone.
            }

            return true;
        });
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<RemoteObjectVersion>> ListVersionsAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        return CallAsync<IReadOnlyList<RemoteObjectVersion>>(async target =>
        {
            var fullKey = target.RootPrefix + key;
            var results = new List<RemoteObjectVersion>();
            var request = new ListVersionsRequest
            {
                BucketName = target.Bucket,
                Prefix = fullKey,
            };

            do
            {
                var response = await target.Client.ListVersionsAsync(request, ct);

                // The prefix also matches longer keys (notes/a.md.bak); only this key's versions are wanted.
                foreach (var item in response.Versions ?? [])
                {
                    if (item.Key == fullKey && item.IsDeleteMarker != true)
                    {
                        results.Add(new RemoteObjectVersion
                        {
                            Key = key,
                            VersionId = item.VersionId,
                            ETag = item.ETag,
                            LastModified = ToOffset(item.LastModified),
                            Size = item.Size ?? 0,
                            IsLatest = item.IsLatest == true,
                        });
                    }
                }

                var more = response.IsTruncated == true;
                request.KeyMarker = more ? response.NextKeyMarker : null;
                request.VersionIdMarker = more ? response.NextVersionIdMarker : null;
            }
            while (request.KeyMarker != null);

            return results.OrderByDescending(v => v.IsLatest).ThenByDescending(v => v.LastModified).ToList();
        });
    }

    /// <inheritdoc/>
    public Task<RemoteObject?> GetVersionAsync(string key, string versionId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentException.ThrowIfNullOrEmpty(versionId);

        return DownloadAsync(key, versionId, ct);
    }

    private static DateTimeOffset ToOffset(DateTime? value)
    {
        return value == null
            ? DateTimeOffset.MinValue
            : new DateTimeOffset(DateTime.SpecifyKind(value.Value.ToUniversalTime(), DateTimeKind.Utc));
    }

    private static DateTimeOffset? ReadModifiedAt(MetadataCollection metadata)
    {
        var value = metadata?[ModifiedAtMetadata];
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : null;
    }

    // The current version (versionId null), or the one asked for.
    private Task<RemoteObject?> DownloadAsync(string key, string? versionId, CancellationToken ct)
    {
        return CallAsync(async target =>
        {
            try
            {
                var request = new GetObjectRequest
                {
                    BucketName = target.Bucket,
                    Key = target.RootPrefix + key,
                    VersionId = versionId,
                };
                using var response = await target.Client.GetObjectAsync(request, ct);
                using var buffer = new MemoryStream();
                await response.ResponseStream.CopyToAsync(buffer, ct);
                return new RemoteObject
                {
                    Key = key,
                    ETag = response.ETag,
                    LastModified = ToOffset(response.LastModified),
                    ModifiedAt = ReadModifiedAt(response.Metadata),
                    Size = buffer.Length,
                    Content = buffer.ToArray(),
                };
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        });
    }

    private async Task<T> CallAsync<T>(Func<S3SyncTarget, Task<T>> call)
    {
        S3SyncTarget? target;
        try
        {
            target = await targetProvider.GetTargetAsync();
        }
        catch (HttpRequestException ex)
        {
            throw new SyncNetworkException(SyncErrorCodes.RemoteNoCredentials, ex);
        }

        if (target == null)
        {
            // Nothing set up to sync with, or set up but no credentials right now: callers treat them differently.
            throw new SyncAccessDeniedException(targetProvider.IsConfigured ? SyncErrorCodes.RemoteNoCredentials : SyncErrorCodes.RemoteNotConfigured);
        }

        try
        {
            return await call(target);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed || ex.ErrorCode == "ConditionalRequestConflict")
        {
            throw new SyncPreconditionFailedException(SyncErrorCodes.RemoteChanged, ex);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new SyncAccessDeniedException(SyncErrorCodes.RemoteAccessDenied, ex);
        }
        catch (AmazonServiceException ex) when (ex.InnerException is HttpRequestException or TaskCanceledException or IOException)
        {
            throw new SyncNetworkException(SyncErrorCodes.RemoteUnreachable, ex);
        }
        catch (HttpRequestException ex)
        {
            throw new SyncNetworkException(SyncErrorCodes.RemoteUnreachable, ex);
        }
    }
}
