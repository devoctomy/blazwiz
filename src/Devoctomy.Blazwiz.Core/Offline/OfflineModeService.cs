using Devoctomy.Blazwiz.Core.Offline.Interfaces;
using Devoctomy.Blazwiz.Core.Offline.Models;

namespace Devoctomy.Blazwiz.Core.Offline;

/// <summary>
/// Implementation of <see cref="IOfflineModeService"/>, held in memory.
/// </summary>
public class OfflineModeService : IOfflineModeService
{
    /// <summary>
    /// How long a session lasts in offline mode, from signing in offline or turning offline mode on.
    /// </summary>
    public static readonly TimeSpan SessionLength = TimeSpan.FromMinutes(15);

    private readonly Lock gate = new Lock();
    private OfflineEntryReason? reason;

    /// <inheritdoc/>
    public event EventHandler<bool>? Changed;

    /// <inheritdoc/>
    public bool IsOffline
    {
        get
        {
            lock (gate)
            {
                return reason != null;
            }
        }
    }

    /// <inheritdoc/>
    public OfflineEntryReason? Reason
    {
        get
        {
            lock (gate)
            {
                return reason;
            }
        }
    }

    /// <inheritdoc/>
    public void Enter(OfflineEntryReason reason)
    {
        bool changed;
        lock (gate)
        {
            changed = this.reason == null;
            this.reason = reason;
        }

        if (changed)
        {
            Changed?.Invoke(this, true);
        }
    }

    /// <inheritdoc/>
    public void Exit()
    {
        bool changed;
        lock (gate)
        {
            changed = reason != null;
            reason = null;
        }

        if (changed)
        {
            Changed?.Invoke(this, false);
        }
    }
}
