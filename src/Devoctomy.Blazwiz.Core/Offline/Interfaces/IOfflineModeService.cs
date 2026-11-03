using Devoctomy.Blazwiz.Core.Offline.Models;

namespace Devoctomy.Blazwiz.Core.Offline.Interfaces;

/// <summary>
/// The global offline mode: while it is on, nothing calls the sign-in service or AWS, and sessions last
/// <see cref="OfflineModeService.SessionLength"/>. Held in memory only, so a reload always leaves offline mode.
/// </summary>
public interface IOfflineModeService
{
    /// <summary>
    /// Raised when offline mode is turned on or off, with whether it is now on.
    /// </summary>
    event EventHandler<bool>? Changed;

    /// <summary>
    /// Gets a value indicating whether offline mode is on.
    /// </summary>
    bool IsOffline { get; }

    /// <summary>
    /// Gets why offline mode was turned on, or null while it is off.
    /// </summary>
    OfflineEntryReason? Reason { get; }

    /// <summary>
    /// Turns offline mode on.
    /// </summary>
    /// <param name="reason">Why.</param>
    void Enter(OfflineEntryReason reason);

    /// <summary>
    /// Turns offline mode off.
    /// </summary>
    void Exit();
}
