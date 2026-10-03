using blazwiz.Core.Encryption.Models;

namespace blazwiz.Core.Encryption.Interfaces;

/// <summary>
/// Keeps this device's <see cref="E2eLocalKeys"/> for the signed in user, separate from the keys document and the
/// synced files. Implemented by the app (e.g. in local storage), partitioned by user.
/// </summary>
public interface IE2eLocalKeyStore
{
    /// <summary>
    /// Gets this device's keys for the signed in user.
    /// </summary>
    /// <returns>The keys, or null when this device has none.</returns>
    Task<E2eLocalKeys?> GetAsync();

    /// <summary>
    /// Saves this device's keys for the signed in user, replacing any already saved.
    /// </summary>
    /// <param name="keys">The keys.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task SaveAsync(E2eLocalKeys keys);

    /// <summary>
    /// Deletes this device's keys for the signed in user. Does nothing when there are none.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task DeleteAsync();
}
