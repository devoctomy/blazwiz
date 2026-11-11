using Devoctomy.Blazwiz.Sync.Exceptions;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;

namespace Devoctomy.Blazwiz.Sync;

/// <summary>
/// Runs passes over the configured profiles, keeps the status, raises <see cref="FilesChanged"/> for downloads and
/// remote deletes, and prunes the log afterwards. A network failure marks the app offline; a refused credential is
/// refreshed once, then the pass fails. A profile is skipped while an <see cref="ISyncGate"/> holds it back.
/// </summary>
/// <remarks>
/// Each profile's last pass is kept, and the status is worked out from all of them, because the scheduler syncs one
/// profile at a time: one profile's pass failing is still shown after another's succeeds. A profile held back by a gate,
/// or no longer registered, no longer counts.
/// </remarks>
public class SyncService(
    ISyncProfileRegistry registry,
    ISyncEngine engine,
    IS3SyncTargetProvider targetProvider,
    IConnectivityService connectivityService,
    ISyncUserContext userContext,
    ISyncEventLog eventLog,
    TimeProvider timeProvider,
    IEnumerable<ISyncGate> gates)
    : ISyncService
{
    private readonly ISyncProfileRegistry registry = registry ?? throw new ArgumentNullException(nameof(registry));
    private readonly ISyncEngine engine = engine ?? throw new ArgumentNullException(nameof(engine));
    private readonly IS3SyncTargetProvider targetProvider = targetProvider ?? throw new ArgumentNullException(nameof(targetProvider));
    private readonly IConnectivityService connectivityService = connectivityService ?? throw new ArgumentNullException(nameof(connectivityService));
    private readonly ISyncUserContext userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
    private readonly ISyncEventLog eventLog = eventLog ?? throw new ArgumentNullException(nameof(eventLog));
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    private readonly IReadOnlyList<ISyncGate> gates = (gates ?? throw new ArgumentNullException(nameof(gates))).ToList();

    private readonly Dictionary<string, ProfileOutcome> outcomes = new Dictionary<string, ProfileOutcome>(StringComparer.Ordinal);

    private SyncStatus status = new SyncStatus();

    /// <inheritdoc/>
    public event EventHandler<SyncStatus>? StatusChanged;

    /// <inheritdoc/>
    public event EventHandler<SyncFilesChangedEventArgs>? FilesChanged;

    /// <inheritdoc/>
    public SyncStatus Status => status.Clone();

    /// <inheritdoc/>
    public IReadOnlyList<string> Profiles => registry.Profiles.Select(p => p.Name).ToList();

    /// <inheritdoc/>
    public async Task<SyncResult> SyncAsync(string? profile = null, bool verify = false, IProgress<SyncPassProgress>? progress = null, CancellationToken ct = default)
    {
        var selected = profile == null
            ? registry.Profiles.Select(p => registry.GetSyncProfile(p.Name)).ToList()
            : [registry.Find(profile) is not null ? registry.GetSyncProfile(profile) : throw new ArgumentException($"There is no sync profile called '{profile}'.", nameof(profile))];

        var total = new SyncResult { Profile = profile };
        UpdateStatus(s => s.State = SyncState.Syncing);
        string? current = null;
        try
        {
            foreach (var item in selected)
            {
                if (!await CanSyncAsync(item.Name, ct))
                {
                    outcomes.Remove(item.Name);
                    continue;
                }

                current = item.Name;
                var result = await RunProfileAsync(item, verify, progress, ct);
                total.Add(result);
                Record(item.Name, result.Failed > 0 ? SyncErrorCodes.FilesFailed : null, [result.Failed], result.ConflictsResolved);
                current = null;
                if (result.ChangedLocalKeys.TryGetValue(item.Name, out var keys) && keys.Count > 0)
                {
                    FilesChanged?.Invoke(this, new SyncFilesChangedEventArgs(item.Name, keys));
                }
            }
        }
        catch (SyncNetworkException ex)
        {
            connectivityService.ReportNetworkFailure(ex);
            Record(current, SyncErrorCodes.RemoteUnreachable, [], 0);
            total.Errors.Add(ex.Message);
        }
        catch (SyncAccessDeniedException ex)
        {
            Record(current, SyncErrorCodes.RemoteAccessDenied, [], 0);
            total.Errors.Add(ex.Message);
        }
        catch (OperationCanceledException)
        {
            // Stopped (e.g. sign out): not an error, and the profile keeps its last pass.
        }
        finally
        {
            var pending = await CountPendingAsync();
            UpdateStatus(s =>
            {
                ApplyOutcomes(s);
                s.PendingCount = pending;
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
        outcomes.Clear();
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

    private async Task<SyncResult> RunProfileAsync(SyncProfile profile, bool verify, IProgress<SyncPassProgress>? progress, CancellationToken ct)
    {
        try
        {
            return await engine.SyncAsync(profile, verify, progress, ct);
        }
        catch (SyncAccessDeniedException)
        {
            // The credentials may have expired early: get new ones and try once more.
            targetProvider.Invalidate();
            return await engine.SyncAsync(profile, verify, progress, ct);
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
            foreach (var profile in registry.Profiles.Select(p => registry.GetSyncProfile(p.Name)))
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

    /// <summary>
    /// Keeps a profile's pass: its error, if it failed, or when it succeeded.
    /// </summary>
    private void Record(string? profile, string? errorCode, IReadOnlyList<object> errorArguments, int conflictsResolved)
    {
        if (profile == null)
        {
            return;
        }

        var previous = outcomes.GetValueOrDefault(profile);
        outcomes[profile] = new ProfileOutcome(
            errorCode,
            errorCode == null ? [] : errorArguments,
            conflictsResolved,
            errorCode == null ? timeProvider.GetUtcNow() : previous?.LastSyncedAt);
    }

    /// <summary>
    /// Works the status out from every registered profile's last pass: an error if any failed (the first, in the
    /// registry's order), the conflicts they resolved, and when the least recently synced of them last synced, or none
    /// while any of them has yet to.
    /// </summary>
    private void ApplyOutcomes(SyncStatus target)
    {
        var current = registry.Profiles
            .Select(p => outcomes.GetValueOrDefault(p.Name))
            .OfType<ProfileOutcome>()
            .ToList();
        var failed = current.FirstOrDefault(o => o.ErrorCode != null);
        target.State = failed == null ? SyncState.Idle : SyncState.Error;
        target.LastErrorCode = failed?.ErrorCode;
        target.LastErrorArguments = failed?.ErrorArguments ?? [];
        target.ConflictsResolved = current.Sum(o => o.ConflictsResolved);
        target.LastSyncedAt = current.Count == 0 || current.Any(o => o.LastSyncedAt == null)
            ? null
            : current.Min(o => o.LastSyncedAt);
    }

    private void UpdateStatus(Action<SyncStatus> change)
    {
        var updated = status.Clone();
        change(updated);
        status = updated;
        StatusChanged?.Invoke(this, status.Clone());
    }

    /// <summary>
    /// A profile's last pass.
    /// </summary>
    private sealed class ProfileOutcome(
        string? errorCode,
        IReadOnlyList<object> errorArguments,
        int conflictsResolved,
        DateTimeOffset? lastSyncedAt)
    {
        public string? ErrorCode { get; } = errorCode;

        public IReadOnlyList<object> ErrorArguments { get; } = errorArguments;

        public int ConflictsResolved { get; } = conflictsResolved;

        public DateTimeOffset? LastSyncedAt { get; } = lastSyncedAt;
    }
}
