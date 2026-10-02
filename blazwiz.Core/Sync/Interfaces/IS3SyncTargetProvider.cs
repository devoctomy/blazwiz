using blazwiz.Core.Sync.Models;

namespace blazwiz.Core.Sync.Interfaces;

/// <summary>
/// Supplies the S3 client, bucket and root prefix for the signed in user. Implemented by the app, so Core does not
/// know how credentials are obtained (e.g. a Cognito identity pool).
/// </summary>
public interface IS3SyncTargetProvider
{
    /// <summary>
    /// Gets the target, using cached credentials while they are valid.
    /// </summary>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The target, or null when there are no credentials (e.g. not signed in, or no cloud configured).</returns>
    /// <exception cref="HttpRequestException">The credentials service could not be reached.</exception>
    Task<S3SyncTarget?> GetTargetAsync(CancellationToken ct = default);

    /// <summary>
    /// Forgets the cached credentials, so the next <see cref="GetTargetAsync"/> gets new ones (e.g. after a 403).
    /// </summary>
    void Invalidate();
}
