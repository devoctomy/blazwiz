using blazwiz.Core.Offline.Interfaces;
using blazwiz.Core.Sync.Configuration;
using blazwiz.Core.Sync.Interfaces;
using blazwiz.Core.Sync.Models;

namespace blazwiz.Core.Sync.Connectivity;

/// <summary>
/// Decides whether sync can reach the cloud. Checks run in order (offline mode, sync turned off, browser network,
/// credentials, S3 probe) and the first that fails sets the reason. Results are reused for
/// <see cref="ConnectivityTimings.CacheFor"/>, and while offline (other than offline mode or sync turned off) the
/// connection is rechecked with <see cref="ConnectivityTimings.RecheckBackoff"/>.
/// </summary>
public sealed class ConnectivityService : IConnectivityService, IDisposable
{
    private readonly IOfflineModeService offlineModeService;
    private readonly ISyncEnabledStore syncEnabledStore;
    private readonly IBrowserNetworkStatus browserNetworkStatus;
    private readonly IS3SyncTargetProvider targetProvider;
    private readonly IS3ReachabilityProbe reachabilityProbe;
    private readonly ISyncEventLog eventLog;
    private readonly ISyncUserContext userContext;
    private readonly ConnectivityTimings timings;
    private readonly TimeProvider timeProvider;

    private ConnectivityStatus status = new ConnectivityStatus();
    private bool? syncEnabled;
    private Task<ConnectivityStatus>? checkInProgress;
    private CancellationTokenSource? recheck;
    private int recheckAttempt;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectivityService"/> class.
    /// </summary>
    /// <param name="offlineModeService">The global offline mode: no AWS calls while it is on.</param>
    /// <param name="syncEnabledStore">Where whether sync is turned on is saved.</param>
    /// <param name="browserNetworkStatus">The browser's network status.</param>
    /// <param name="targetProvider">Supplies the S3 credentials.</param>
    /// <param name="reachabilityProbe">Probes S3.</param>
    /// <param name="eventLog">The sync log.</param>
    /// <param name="userContext">The signed in user.</param>
    /// <param name="options">The sync options.</param>
    /// <param name="timeProvider">The clock and timers.</param>
    public ConnectivityService(
        IOfflineModeService offlineModeService,
        ISyncEnabledStore syncEnabledStore,
        IBrowserNetworkStatus browserNetworkStatus,
        IS3SyncTargetProvider targetProvider,
        IS3ReachabilityProbe reachabilityProbe,
        ISyncEventLog eventLog,
        ISyncUserContext userContext,
        SyncOptions options,
        TimeProvider timeProvider)
    {
        this.offlineModeService = offlineModeService ?? throw new ArgumentNullException(nameof(offlineModeService));
        this.syncEnabledStore = syncEnabledStore ?? throw new ArgumentNullException(nameof(syncEnabledStore));
        this.browserNetworkStatus = browserNetworkStatus ?? throw new ArgumentNullException(nameof(browserNetworkStatus));
        this.targetProvider = targetProvider ?? throw new ArgumentNullException(nameof(targetProvider));
        this.reachabilityProbe = reachabilityProbe ?? throw new ArgumentNullException(nameof(reachabilityProbe));
        this.eventLog = eventLog ?? throw new ArgumentNullException(nameof(eventLog));
        this.userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
        timings = (options ?? throw new ArgumentNullException(nameof(options))).ConnectivityTimings;
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

        this.browserNetworkStatus.OnlineChanged += OnBrowserOnlineChanged;
        this.offlineModeService.Changed += OnOfflineModeChanged;
    }

    /// <inheritdoc/>
    public event EventHandler<ConnectivityStatus>? StatusChanged;

    /// <inheritdoc/>
    public ConnectivityStatus Status => status.Clone();

    /// <inheritdoc/>
    public bool IsSyncEnabled => syncEnabled != false;

    /// <summary>
    /// Gets a value indicating whether a recheck is scheduled.
    /// </summary>
    public bool IsRecheckScheduled => recheck != null;

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        syncEnabled ??= await syncEnabledStore.GetAsync();
    }

    /// <inheritdoc/>
    public async Task SetSyncEnabledAsync(bool enabled)
    {
        await InitializeAsync();
        var changed = syncEnabled != enabled;
        await syncEnabledStore.SetAsync(enabled);
        syncEnabled = enabled;
        if (changed)
        {
            await LogAsync(SyncEventKind.OfflineModeChanged, enabled ? "Sync turned on" : "Sync turned off");
        }

        StopRecheck();
        await RecheckAsync();
    }

    /// <inheritdoc/>
    public async Task<ConnectivityStatus> CheckAsync(CancellationToken ct = default)
    {
        await InitializeAsync();
        var last = status;
        var stillValid = last.LastCheckedAt != null
            && timeProvider.GetUtcNow() - last.LastCheckedAt.Value < timings.CacheFor
            && GetSwitchedOffReason(last.Reason) == GetSwitchedOffReason();
        return stillValid ? last.Clone() : await RecheckAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<ConnectivityStatus> RecheckAsync(CancellationToken ct = default)
    {
        await InitializeAsync();
        if (checkInProgress != null)
        {
            return await checkInProgress;
        }

        checkInProgress = RunChecksAsync(ct);
        try
        {
            return await checkInProgress;
        }
        finally
        {
            checkInProgress = null;
        }
    }

    /// <inheritdoc/>
    public void ReportNetworkFailure(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        _ = ApplyAsync(false, OfflineReason.Unreachable);
    }

    /// <inheritdoc/>
    public void Reset()
    {
        StopRecheck();
        status = new ConnectivityStatus();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        browserNetworkStatus.OnlineChanged -= OnBrowserOnlineChanged;
        offlineModeService.Changed -= OnOfflineModeChanged;
        StopRecheck();
    }

    private static OfflineReason? GetSwitchedOffReason(OfflineReason? reason) =>
        reason is OfflineReason.OfflineMode or OfflineReason.SyncDisabled ? reason : null;

    private async Task<ConnectivityStatus> RunChecksAsync(CancellationToken ct)
    {
        // 1 and 2. Offline mode, or sync turned off: no network calls at all.
        var switchedOff = GetSwitchedOffReason();
        if (switchedOff != null)
        {
            return await ApplyAsync(false, switchedOff);
        }

        // 3. The browser can only prove the app is offline.
        if (!await browserNetworkStatus.IsOnlineAsync())
        {
            return await ApplyAsync(false, OfflineReason.NoNetwork);
        }

        // 4. Credentials (a call to Cognito unless cached).
        S3SyncTarget? target;
        try
        {
            target = await targetProvider.GetTargetAsync(ct);
        }
        catch (HttpRequestException)
        {
            return await ApplyAsync(false, OfflineReason.Unreachable);
        }

        if (target == null)
        {
            return await ApplyAsync(false, OfflineReason.NoCredentials);
        }

        // 5. S3 itself.
        var reachable = await reachabilityProbe.IsReachableAsync(target, timings.ProbeTimeout, ct);
        return await ApplyAsync(reachable, reachable ? null : OfflineReason.Unreachable);
    }

    private async Task<ConnectivityStatus> ApplyAsync(bool isOnline, OfflineReason? reason)
    {
        var previous = status;
        status = new ConnectivityStatus
        {
            IsOnline = isOnline,
            Reason = reason,
            LastCheckedAt = timeProvider.GetUtcNow(),
        };

        if (isOnline || GetSwitchedOffReason(reason) != null)
        {
            StopRecheck();
        }
        else
        {
            ScheduleRecheck();
        }

        var changed = previous.LastCheckedAt == null || previous.IsOnline != isOnline || previous.Reason != reason;
        if (changed)
        {
            if (isOnline && previous is { LastCheckedAt: not null, IsOnline: false })
            {
                await LogAsync(SyncEventKind.WentOnline, "Back online");
            }
            else if (!isOnline && GetSwitchedOffReason(reason) == null)
            {
                await LogAsync(SyncEventKind.WentOffline, status.Describe());
            }

            StatusChanged?.Invoke(this, status.Clone());
        }

        return status.Clone();
    }

    private void ScheduleRecheck()
    {
        if (recheck != null)
        {
            return;
        }

        var backoff = timings.RecheckBackoff;
        var delay = backoff[Math.Min(recheckAttempt, backoff.Count - 1)];
        recheckAttempt++;
        recheck = new CancellationTokenSource();
        _ = RecheckAfterAsync(delay, recheck);
    }

    private async Task RecheckAfterAsync(TimeSpan delay, CancellationTokenSource source)
    {
        try
        {
            await Task.Delay(delay, timeProvider, source.Token);
            if (recheck == source)
            {
                recheck = null;
            }

            source.Dispose();
            await RecheckAsync();
        }
        catch (OperationCanceledException)
        {
            // Stopped: back online, offline mode, or signed out.
        }
        catch (Exception)
        {
            // A failed check leaves the status as it was; the next trigger checks again.
        }
    }

    private void StopRecheck()
    {
        recheckAttempt = 0;
        var source = recheck;
        recheck = null;
        if (source != null)
        {
            source.Cancel();
            source.Dispose();
        }
    }

    private async Task LogAsync(SyncEventKind kind, string message)
    {
        try
        {
            var owner = await userContext.GetUserIdAsync();
            if (owner == null)
            {
                return;
            }

            await eventLog.WriteAsync(
            [
                new SyncEvent
                {
                    Owner = owner,
                    Timestamp = timeProvider.GetUtcNow(),
                    Kind = kind,
                    Message = message,
                },
            ]);
        }
        catch (Exception)
        {
            // The log is diagnostic only.
        }
    }

    // Why sync is switched off, if it is: offline mode first, then sync turned off.
    private OfflineReason? GetSwitchedOffReason()
    {
        if (offlineModeService.IsOffline)
        {
            return OfflineReason.OfflineMode;
        }

        return IsSyncEnabled ? null : OfflineReason.SyncDisabled;
    }

    private async void OnOfflineModeChanged(object? sender, bool isOffline)
    {
        try
        {
            await LogAsync(SyncEventKind.OfflineModeChanged, isOffline ? "Offline mode turned on" : "Offline mode turned off");
            StopRecheck();
            await RecheckAsync();
        }
        catch (Exception)
        {
            // Event handlers must not throw; the next trigger checks again.
        }
    }

    private async void OnBrowserOnlineChanged(object? sender, bool isOnline)
    {
        try
        {
            await RecheckAsync();
        }
        catch (Exception)
        {
            // Event handlers must not throw; the next trigger checks again.
        }
    }
}
