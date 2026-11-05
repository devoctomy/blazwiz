using Devoctomy.Blazwiz.Encryption.Models;

namespace Devoctomy.Blazwiz.Encryption.Interfaces;

/// <summary>
/// Keeps the signed in user's keys documents, one per key scope, somewhere only they can reach, outside the synced files
/// (the app stores each in their own Secrets Manager secret). The value is opaque to the store; callers decide its
/// format.
/// </summary>
/// <remarks>
/// Implementations throw <see cref="InvalidOperationException"/> when the store cannot be used in this session (e.g.
/// not signed in with a real identity provider), <see cref="HttpRequestException"/> when it cannot be reached, and
/// their own exceptions when the identity provider refuses the session.
/// </remarks>
public interface IUserKeysStore
{
    /// <summary>
    /// Gets the user's stored keys for a scope.
    /// </summary>
    /// <param name="scope">The key scope.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The stored value, or null when the user has none yet.</returns>
    /// <exception cref="InvalidOperationException">The store cannot be used in this session.</exception>
    /// <exception cref="HttpRequestException">The store could not be reached.</exception>
    Task<string?> GetAsync(E2eKeyScope scope, CancellationToken ct = default);

    /// <summary>
    /// Stores the user's keys for a scope, creating them the first time or replacing them.
    /// </summary>
    /// <param name="scope">The key scope.</param>
    /// <param name="value">The value to store.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A task that completes when the value is stored.</returns>
    /// <exception cref="InvalidOperationException">The store cannot be used in this session.</exception>
    /// <exception cref="HttpRequestException">The store could not be reached.</exception>
    Task SetAsync(E2eKeyScope scope, string value, CancellationToken ct = default);

    /// <summary>
    /// Stores the user's keys for a scope only if it has none yet, never replacing existing ones, so two devices cannot both
    /// create them.
    /// </summary>
    /// <param name="scope">The key scope.</param>
    /// <param name="value">The value to store.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>True when stored; false when the user already has keys, which are left as they are.</returns>
    /// <exception cref="InvalidOperationException">The store cannot be used in this session.</exception>
    /// <exception cref="HttpRequestException">The store could not be reached.</exception>
    Task<bool> TryCreateAsync(E2eKeyScope scope, string value, CancellationToken ct = default);

    /// <summary>
    /// Deletes the user's keys for a scope straight away, with no recovery window. Does nothing when there are none.
    /// </summary>
    /// <param name="scope">The key scope.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A task that completes when the keys are deleted.</returns>
    /// <exception cref="InvalidOperationException">The store cannot be used in this session.</exception>
    /// <exception cref="HttpRequestException">The store could not be reached.</exception>
    Task DeleteAsync(E2eKeyScope scope, CancellationToken ct = default);
}
