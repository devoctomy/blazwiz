namespace blazwiz.Core.Sync.Interfaces;

/// <summary>
/// A lock shared by every tab and window of the app, so two never sync the same profile at once.
/// </summary>
public interface ISyncLockService
{
    /// <summary>
    /// Waits for and takes a lock. The browser releases it if the tab closes.
    /// </summary>
    /// <param name="name">The lock name.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>Dispose to release the lock.</returns>
    Task<IAsyncDisposable> AcquireAsync(string name, CancellationToken ct = default);
}
