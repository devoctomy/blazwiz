using blazwiz.Core.Sync.Models;

namespace blazwiz.Core.Sync.Interfaces;

/// <summary>
/// Checks S3 can be reached with the user's credentials: a one key listing of the user's own prefix (the IAM role
/// does not grant bucket wide access, so <c>HeadBucket</c> cannot be used).
/// </summary>
public interface IS3ReachabilityProbe
{
    /// <summary>
    /// Probes S3.
    /// </summary>
    /// <param name="target">The user's S3 target.</param>
    /// <param name="timeout">How long to wait.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>True if S3 answered and accepted the credentials.</returns>
    Task<bool> IsReachableAsync(S3SyncTarget target, TimeSpan timeout, CancellationToken ct = default);
}
