namespace blazwiz.Core.Sync.Interfaces;

/// <summary>
/// Saves the offline mode setting, so it is remembered across reloads and sign outs. Implemented by the app.
/// </summary>
public interface IOfflineModeStore
{
    /// <summary>
    /// Gets whether offline mode is on.
    /// </summary>
    /// <returns>True if offline mode is on.</returns>
    Task<bool> GetAsync();

    /// <summary>
    /// Saves whether offline mode is on.
    /// </summary>
    /// <param name="forcedOffline">True if offline mode is on.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task SetAsync(bool forcedOffline);
}
