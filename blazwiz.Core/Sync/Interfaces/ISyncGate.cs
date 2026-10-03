namespace blazwiz.Core.Sync.Interfaces;

/// <summary>
/// Lets a feature hold back a profile's passes until it is ready, e.g. end to end encryption until this device can
/// encrypt. <see cref="ISyncService"/> skips a profile while any registered gate holds it.
/// </summary>
public interface ISyncGate
{
    /// <summary>
    /// Works out whether the profile may sync now. Asked before every pass of the profile.
    /// </summary>
    /// <param name="profile">The profile.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>True to let it sync.</returns>
    Task<bool> CanSyncAsync(string profile, CancellationToken ct = default);
}
