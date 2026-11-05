namespace Devoctomy.Blazwiz.Encryption.Interfaces;

/// <summary>
/// Keeps this device's copy of the signed in user's index of protected profiles, so they can be listed offline.
/// Implemented by the app (e.g. in local storage), partitioned by user.
/// </summary>
public interface IE2eLocalProfileIndexStore
{
    /// <summary>
    /// Gets this device's copy of the index.
    /// </summary>
    /// <returns>The copy, or null when this device has none.</returns>
    Task<string?> GetAsync();

    /// <summary>
    /// Saves this device's copy of the index, replacing any already saved.
    /// </summary>
    /// <param name="value">The index.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task SaveAsync(string value);
}
