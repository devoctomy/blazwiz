using Devoctomy.Blazwiz.Sync.Connectivity;

namespace Devoctomy.Blazwiz.Sync.Interfaces;

/// <summary>
/// Decides whether sync can reach the cloud, checking (in order) offline mode, whether sync is turned on, the
/// browser's network, AWS credentials and whether S3 is reachable. The cheapest checks come first, and offline mode or
/// sync turned off make no network calls at all.
/// </summary>
public interface IConnectivityService
{
    /// <summary>
    /// Raised when <see cref="Status"/> changes (online / offline, or the reason).
    /// </summary>
    event EventHandler<ConnectivityStatus>? StatusChanged;

    /// <summary>
    /// Gets the last known status.
    /// </summary>
    ConnectivityStatus Status { get; }

    /// <summary>
    /// Gets a value indicating whether sync is turned on. Call <see cref="InitializeAsync"/> first.
    /// </summary>
    bool IsSyncEnabled { get; }

    /// <summary>
    /// Loads the saved sync setting. Safe to call more than once.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task InitializeAsync();

    /// <summary>
    /// Turns sync on or off, saves it, and checks the connection again.
    /// </summary>
    /// <param name="enabled">True to turn sync on.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task SetSyncEnabledAsync(bool enabled);

    /// <summary>
    /// Checks the connection, reusing a recent result.
    /// </summary>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The status.</returns>
    Task<ConnectivityStatus> CheckAsync(CancellationToken ct = default);

    /// <summary>
    /// Checks the connection now, ignoring any recent result.
    /// </summary>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The status.</returns>
    Task<ConnectivityStatus> RecheckAsync(CancellationToken ct = default);

    /// <summary>
    /// Records that a sync pass could not reach S3: marks the app offline (unreachable) and schedules a recheck.
    /// </summary>
    /// <param name="exception">The failure.</param>
    void ReportNetworkFailure(Exception exception);

    /// <summary>
    /// Stops rechecking and forgets the status, e.g. on sign out.
    /// </summary>
    void Reset();
}
