using Amazon.Runtime;
using Amazon.S3.Model;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;

namespace Devoctomy.Blazwiz.Sync.S3;

/// <summary>
/// Probes S3 with <c>ListObjectsV2</c> on the user's own prefix and <c>MaxKeys = 1</c>, which also proves the
/// credentials work.
/// </summary>
public class S3ReachabilityProbe : IS3ReachabilityProbe
{
    /// <inheritdoc/>
    public async Task<bool> IsReachableAsync(S3SyncTarget target, TimeSpan timeout, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(target);

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutSource.CancelAfter(timeout);
        try
        {
            await target.Client.ListObjectsV2Async(
                new ListObjectsV2Request
                {
                    BucketName = target.Bucket,
                    Prefix = target.RootPrefix,
                    MaxKeys = 1,
                },
                timeoutSource.Token);
            return true;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception ex) when (ex is AmazonServiceException or AmazonClientException or HttpRequestException)
        {
            return false;
        }
    }
}
