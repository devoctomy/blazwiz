using Devoctomy.Blazwiz.Sync.Configuration;
using Devoctomy.Blazwiz.Sync.Connectivity;
using Devoctomy.Blazwiz.Sync.Interfaces;

namespace Devoctomy.Blazwiz.Sync;

/// <summary>
/// Runs passes when they are needed: on start (a full verify pass), after local changes (debounced and coalesced),
/// when the app regains focus and when it comes back online. Every pass checks connectivity first and holds the
/// profile's cross tab lock while it runs. A request during a pass runs the profile again afterwards.
/// </summary>
public sealed class SyncScheduler : ISyncScheduler, IDisposable
{
    private readonly ISyncService syncService;
    private readonly IConnectivityService connectivityService;
    private readonly IBrowserNetworkStatus browserNetworkStatus;
    private readonly ISyncLockService lockService;
    private readonly SyncOptions options;
    private readonly TimeProvider timeProvider;
    private readonly Dictionary<string, ProfileState> states = [];

    private CancellationTokenSource? lifetime;
    private bool wasOffline;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncScheduler"/> class.
    /// </summary>
    /// <param name="syncService">Runs the passes.</param>
    /// <param name="connectivityService">Decides whether the app is online.</param>
    /// <param name="browserNetworkStatus">Raises the focus events.</param>
    /// <param name="lockService">The cross tab lock.</param>
    /// <param name="options">The sync options.</param>
    /// <param name="timeProvider">The timers.</param>
    public SyncScheduler(
        ISyncService syncService,
        IConnectivityService connectivityService,
        IBrowserNetworkStatus browserNetworkStatus,
        ISyncLockService lockService,
        SyncOptions options,
        TimeProvider timeProvider)
    {
        this.syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));
        this.connectivityService = connectivityService ?? throw new ArgumentNullException(nameof(connectivityService));
        this.browserNetworkStatus = browserNetworkStatus ?? throw new ArgumentNullException(nameof(browserNetworkStatus));
        this.lockService = lockService ?? throw new ArgumentNullException(nameof(lockService));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <inheritdoc/>
    public bool IsStarted => lifetime != null;

    /// <inheritdoc/>
    public async Task StartAsync()
    {
        if (lifetime != null)
        {
            return;
        }

        lifetime = new CancellationTokenSource();
        states.Clear();
        foreach (var profile in syncService.Profiles)
        {
            // The first pass after sign in hashes every local file against the index.
            states[profile] = new ProfileState { VerifyPending = true };
        }

        wasOffline = false;
        connectivityService.StatusChanged += OnConnectivityChanged;
        browserNetworkStatus.BecameVisible += OnBecameVisible;
        await connectivityService.InitializeAsync();
        await browserNetworkStatus.StartAsync();
        await RunAllAsync();
    }

    /// <inheritdoc/>
    public async Task StopAsync()
    {
        var source = lifetime;
        if (source == null)
        {
            return;
        }

        lifetime = null;
        connectivityService.StatusChanged -= OnConnectivityChanged;
        browserNetworkStatus.BecameVisible -= OnBecameVisible;
        foreach (var state in states.Values)
        {
            state.Debounce?.Cancel();
            state.Debounce = null;
            state.RunAgain = false;
        }

        source.Cancel();
        source.Dispose();
        connectivityService.Reset();
        syncService.Reset();
        await browserNetworkStatus.StopAsync();
    }

    /// <inheritdoc/>
    public void RequestSync(string profile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profile);
        if (lifetime == null || !states.TryGetValue(profile, out var state))
        {
            return;
        }

        state.Debounce?.Cancel();
        var debounce = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        state.Debounce = debounce;
        _ = RunAfterDebounceAsync(profile, state, debounce);
    }

    /// <inheritdoc/>
    public Task SyncNowAsync()
    {
        return lifetime == null ? Task.CompletedTask : RunAllAsync();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        connectivityService.StatusChanged -= OnConnectivityChanged;
        browserNetworkStatus.BecameVisible -= OnBecameVisible;
        lifetime?.Cancel();
        lifetime?.Dispose();
        lifetime = null;
    }

    private async Task RunAfterDebounceAsync(string profile, ProfileState state, CancellationTokenSource debounce)
    {
        try
        {
            await Task.Delay(options.ChangeDebounce, timeProvider, debounce.Token);
        }
        catch (OperationCanceledException)
        {
            // A newer change restarted the debounce, or sync stopped.
            return;
        }
        finally
        {
            if (state.Debounce == debounce)
            {
                state.Debounce = null;
            }

            debounce.Dispose();
        }

        await RunProfileAsync(profile);
    }

    private async Task RunAllAsync()
    {
        foreach (var profile in states.Keys.ToList())
        {
            await RunProfileAsync(profile);
        }
    }

    private async Task RunProfileAsync(string profile)
    {
        var token = lifetime?.Token ?? new CancellationToken(canceled: true);
        if (token.IsCancellationRequested || !states.TryGetValue(profile, out var state))
        {
            return;
        }

        if (state.Running)
        {
            // Single flight: run again once the current pass finishes.
            state.RunAgain = true;
            return;
        }

        state.Running = true;
        try
        {
            do
            {
                state.RunAgain = false;
                var connectivity = await connectivityService.CheckAsync(token);
                if (!connectivity.IsOnline)
                {
                    // Changes keep being recorded while offline; just show how many are waiting.
                    await syncService.RefreshPendingCountAsync();
                    break;
                }

                await using (await lockService.AcquireAsync(SyncDefaults.LockNamePrefix + profile, token))
                {
                    var verify = state.VerifyPending;
                    state.VerifyPending = false;
                    await syncService.SyncAsync(profile, verify, token);
                }
            }
            while (state.RunAgain && !token.IsCancellationRequested);
        }
        catch (OperationCanceledException)
        {
            // Stopped.
        }
        catch (Exception)
        {
            // Failures are reported through the sync status and log; the next trigger tries again.
        }
        finally
        {
            state.Running = false;
        }
    }

    private async void OnConnectivityChanged(object? sender, ConnectivityStatus status)
    {
        var backOnline = status.IsOnline && wasOffline;
        wasOffline = !status.IsOnline;
        if (!backOnline)
        {
            return;
        }

        try
        {
            await RunAllAsync();
        }
        catch (Exception)
        {
            // Event handlers must not throw.
        }
    }

    private async void OnBecameVisible(object? sender, EventArgs e)
    {
        try
        {
            await RunAllAsync();
        }
        catch (Exception)
        {
            // Event handlers must not throw.
        }
    }

    private sealed class ProfileState
    {
        public bool Running { get; set; }

        public bool RunAgain { get; set; }

        public bool VerifyPending { get; set; }

        public CancellationTokenSource? Debounce { get; set; }
    }
}
