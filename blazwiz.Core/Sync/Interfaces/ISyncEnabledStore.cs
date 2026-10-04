namespace blazwiz.Core.Sync.Interfaces;

/// <summary>
/// Saves whether sync is turned on, so it is remembered across reloads and sign outs. Implemented by the app.
/// </summary>
public interface ISyncEnabledStore
{
    /// <summary>
    /// Gets whether sync is turned on.
    /// </summary>
    /// <returns>True if sync is turned on.</returns>
    Task<bool> GetAsync();

    /// <summary>
    /// Saves whether sync is turned on.
    /// </summary>
    /// <param name="enabled">True if sync is turned on.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task SetAsync(bool enabled);
}
