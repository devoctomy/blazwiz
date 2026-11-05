using Devoctomy.Blazwiz.Encryption.Models;

namespace Devoctomy.Blazwiz.Encryption.Interfaces;

/// <summary>
/// Keeps this device's <see cref="E2eLocalKeys"/> for the signed in user, one per key scope, separate from the keys
/// documents and the synced files. Implemented by the app (e.g. in local storage), partitioned by user and scope.
/// </summary>
public interface IE2eLocalKeyStore
{
    /// <summary>
    /// Gets this device's keys for the signed in user.
    /// </summary>
    /// <param name="scope">The key scope.</param>
    /// <returns>The keys, or null when this device has none.</returns>
    Task<E2eLocalKeys?> GetAsync(E2eKeyScope scope);

    /// <summary>
    /// Saves this device's keys for the signed in user, replacing any already saved.
    /// </summary>
    /// <param name="scope">The key scope.</param>
    /// <param name="keys">The keys.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task SaveAsync(E2eKeyScope scope, E2eLocalKeys keys);

    /// <summary>
    /// Deletes this device's keys for the signed in user. Does nothing when there are none.
    /// </summary>
    /// <param name="scope">The key scope.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task DeleteAsync(E2eKeyScope scope);
}
