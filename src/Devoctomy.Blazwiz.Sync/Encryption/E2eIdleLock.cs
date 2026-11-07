using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;
using Devoctomy.Blazwiz.Sync.Interfaces;

namespace Devoctomy.Blazwiz.Sync.Encryption;

/// <summary>
/// Implementation of <see cref="IE2eIdleLock"/>: a timer per unlocked protected profile, from its settings in the
/// <see cref="ISyncProfileRegistry"/>, restarted by each read or write and stopped when it is locked.
/// </summary>
/// <param name="manager">Locks the profiles, and says when they are unlocked.</param>
/// <param name="registry">The profiles' settings.</param>
/// <param name="timeProvider">The timers.</param>
public sealed class E2eIdleLock(IE2eProtectedProfileManager manager, ISyncProfileRegistry registry, TimeProvider timeProvider)
    : IE2eIdleLock, IDisposable
{
    private readonly IE2eProtectedProfileManager manager = manager ?? throw new ArgumentNullException(nameof(manager));
    private readonly ISyncProfileRegistry registry = registry ?? throw new ArgumentNullException(nameof(registry));
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    private readonly Lock gate = new Lock();
    private readonly Dictionary<E2eKeyScope, (ITimer Timer, TimeSpan Timeout)> timers = [];
    private bool started;

    /// <inheritdoc/>
    public void Start()
    {
        lock (gate)
        {
            if (started)
            {
                return;
            }

            started = true;
        }

        manager.LockStateChanged += OnLockStateChanged;
        registry.ProfileAdded += OnProfileAdded;
        foreach (var scope in manager.Unlocked)
        {
            StartTimer(scope);
        }
    }

    /// <inheritdoc/>
    public void Touch(E2eKeyScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        lock (gate)
        {
            if (timers.TryGetValue(scope, out var entry))
            {
                entry.Timer.Change(entry.Timeout, Timeout.InfiniteTimeSpan);
            }
        }
    }

    /// <inheritdoc/>
    public void Refresh(E2eKeyScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        StopTimer(scope);
        if (manager.IsUnlocked(scope))
        {
            StartTimer(scope);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        manager.LockStateChanged -= OnLockStateChanged;
        registry.ProfileAdded -= OnProfileAdded;
        lock (gate)
        {
            foreach (var entry in timers.Values)
            {
                entry.Timer.Dispose();
            }

            timers.Clear();
        }
    }

    private void OnLockStateChanged(object? sender, E2eKeyScope scope)
    {
        if (manager.IsUnlocked(scope))
        {
            StartTimer(scope);
        }
        else
        {
            StopTimer(scope);
        }
    }

    private void OnProfileAdded(object? sender, string name)
    {
        // A new profile is unlocked before it is added, so its settings are only known now.
        if (registry.Find(name)?.KeyScope is { } scope && manager.IsUnlocked(scope))
        {
            StartTimer(scope);
        }
    }

    private void StartTimer(E2eKeyScope scope)
    {
        if (registry.Find(scope.Id)?.Settings.IdleTimeout is not { } timeout)
        {
            // No idle timeout: unlocked until locked or signed out.
            return;
        }

        lock (gate)
        {
            if (timers.Remove(scope, out var old))
            {
                old.Timer.Dispose();
            }

            timers[scope] = (timeProvider.CreateTimer(_ => manager.Lock(scope), null, timeout, Timeout.InfiniteTimeSpan), timeout);
        }
    }

    private void StopTimer(E2eKeyScope scope)
    {
        lock (gate)
        {
            if (timers.Remove(scope, out var entry))
            {
                entry.Timer.Dispose();
            }
        }
    }
}
