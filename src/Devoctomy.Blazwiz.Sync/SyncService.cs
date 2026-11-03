using Devoctomy.Blazwiz.Sync.Exceptions;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;

namespace Devoctomy.Blazwiz.Sync;

/// <summary>
/// Runs passes over the configured profiles, keeps the status, raises <see cref="FilesChanged"/> for downloads and
/// remote deletes, and prunes the log afterwards. A network failure marks the app offline; a refused credential is
/// refreshed once, then the pass fails. A profile is skipped while an <see cref="ISyncGate"/> holds it back.
/// </summary>
public class SyncService(
    IEnumerable<SyncProfile> profiles,
    ISyncEngine engine,
    IS3SyncTargetProvider targetProvider,
    IConnectivityService connectivityService,
    ISyncUserContext userContext,
    ISyncEventLog eventLog,
    TimeProvider timeProvider,
    IEnumerable<ISyncGate> gates)
    : ISyncService
{
    private readonly IReadOnlyList<SyncProfile> profiles = (profiles ?? throw new ArgumentNullException(nameof(profiles))).ToList();
    private readonly ISyncEngine engine = engine ?? throw new ArgumentNullException(nameof(engine));
    private readonly IS3SyncTargetProvider targetProvider = targetProvider ?? throw new ArgumentNullException(nameof(targetProvider));
    private readonly IConnectivityService connectivityService = connectivityService ?? throw new ArgumentNullException(nameof(connectivityService));
    private readonly ISyncUserContext userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
    private readonly ISyncEventLog eventLog = eventLog ?? throw new ArgumentNullException(nameof(eventLog));
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    private readonly IReadOnlyList<ISyncGate> gates = (gates ?? throw new ArgumentNullException(nameof(gates))).ToList();

    private SyncStatus status = new SyncStatus();

    /// <inheritdoc/>
    public event EventHandler<SyncStatus>? StatusChanged;

    /// <inheritdoc/>
    public event EventHandler<SyncFilesChangedEventArgs>? FilesChanged;

    /// <inheritdoc/>
    public SyncStatus Status => status.Clone();

    /// <inheritdoc/>
    public IReadOnlyList<string> Profiles => profiles.Select(p => p.Name).ToList();

    /// <inheritdoc/>
    public async Task<SyncResult> SyncAsync(string? profile = null, bool verify = false, CancellationToken ct = default)
    {
        var selected = profile == null
            ? profiles
            : [profiles.FirstOrDefault(p => p.Name == profile) ?? throw new ArgumentException($"There is no sync profile called '{profile}'.", nameof(profile))];

        var total = new SyncResult { Profile = profile };
        UpdateStatus(s => s.State = SyncState.Syncing);
        string? error = null;
        IReadOnlyList<object> errorArguments = [];
        var ran = false;
        try
        {
            foreach (var item in selected)
            {
                if (!await CanSyncAsync(item.Name, ct))
                {
                    continue;
                }

                ran = true;
                var result = await RunProfileAsync(item, verify, ct);
                total.Add(result);
                if (result.ChangedLocalKeys.TryGetValue(item.Name, out var keys) && keys.Count > 0)
                {
                    FilesChanged?.Invoke(this, new SyncFilesChangedEventArgs(item.Name, keys));
                }
            }

            if (total.Failed > 0)
            {
                error = SyncErrorCodes.FilesFailed;
                errorArguments = [total.Failed];
            }
        }
        catch (SyncNetworkException ex)
        {
            connectivityService.ReportNetworkFailure(ex);
            error = SyncErrorCodes.RemoteUnreachable;
            total.Errors.Add(ex.Message);
        }
        catch (SyncAccessDeniedException ex)
        {
            error = SyncErrorCodes.RemoteAccessDenied;
            total.Errors.Add(ex.Message);
        }
        catch (OperationCanceledException)
        {
            // Stopped (e.g. sign out): not an error.
        }
        finally
        {
            var pending = await CountPendingAsync();
            UpdateStatus(s =>
            {
                s.State = error == null ? SyncState.Idle : SyncState.Error;
                s.LastErrorCode = error;
                s.LastErrorArguments = errorArguments;
                s.PendingCount = pending;
                s.ConflictsResolved = total.ConflictsResolved;
                if (error == null && ran && !ct.IsCancellationRequested)
                {
                    s.LastSyncedAt = timeProvider.GetUtcNow();
                }
            });
            await PruneLogAsync();
        }

        return total;
    }

    /// <inheritdoc/>
    public async Task RefreshPendingCountAsync()
    {
        var pending = await CountPendingAsync();
        if (pending != status.PendingCount)
        {
            UpdateStatus(s => s.PendingCount = pending);
        }
    }

    /// <inheritdoc/>
    public void Reset()
    {
        status = new SyncStatus();
        StatusChanged?.Invoke(this, status.Clone());
    }

    private async Task<bool> CanSyncAsync(string profile, CancellationToken ct)
    {
        foreach (var gate in gates)
        {
            if (!await gate.CanSyncAsync(profile, ct))
            {
                return false;
            }
        }

        return true;
    }

    private async Task<SyncResult> RunProfileAsync(SyncProfile profile, bool verify, CancellationToken ct)
    {
        try
        {
            return await engine.SyncAsync(profile, verify, ct);
        }
        catch (SyncAccessDeniedException)
        {
            // The credentials may have expired early: get new ones and try once more.
            targetProvider.Invalidate();
            return await engine.SyncAsync(profile, verify, ct);
        }
    }

    private async Task<int> CountPendingAsync()
    {
        try
        {
            var owner = await userContext.GetUserIdAsync();
            if (owner == null)
            {
                return 0;
            }

            var count = 0;
            foreach (var profile in profiles)
            {
                var records = await profile.IndexStore.GetAllAsync(profile.Name, owner);
                count += records.Count(r => r.IsPending && profile.Matcher.IsMatch(r.Key));
            }

            return count;
        }
        catch (Exception)
        {
            return status.PendingCount;
        }
    }

    private async Task PruneLogAsync()
    {
        try
        {
            await eventLog.PruneAsync();
        }
        catch (Exception)
        {
            // The log is diagnostic only.
        }
    }

    private void UpdateStatus(Action<SyncStatus> change)
    {
        var updated = status.Clone();
        change(updated);
        status = updated;
        StatusChanged?.Invoke(this, status.Clone());
    }
}
