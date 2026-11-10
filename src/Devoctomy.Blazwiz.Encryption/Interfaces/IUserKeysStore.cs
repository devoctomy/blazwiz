using Devoctomy.Blazwiz.Encryption.Exceptions;
using Devoctomy.Blazwiz.Encryption.Models;

namespace Devoctomy.Blazwiz.Encryption.Interfaces;

/// <summary>
/// Keeps the signed in user's keys documents, one per key scope, somewhere only they can reach (the app keeps each in
/// the user's prefix of the data bucket, under <c>keys/</c>). The value is opaque to the store; callers decide its
/// format. Every write is conditional, so two devices changing the same document cannot both succeed.
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
    /// <returns>The stored value with its version, or null when the user has none yet.</returns>
    /// <exception cref="InvalidOperationException">The store cannot be used in this session.</exception>
    /// <exception cref="HttpRequestException">The store could not be reached.</exception>
    Task<StoredDocument?> GetAsync(E2eKeyScope scope, CancellationToken ct = default);

    /// <summary>
    /// Replaces the user's keys for a scope, only if they are still at the version read.
    /// </summary>
    /// <param name="scope">The key scope.</param>
    /// <param name="value">The value to store.</param>
    /// <param name="ifMatch">The version the value was read at (<see cref="StoredDocument.ETag"/>).</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The new version.</returns>
    /// <exception cref="E2eDocumentChangedException">The keys changed or were deleted since they were read; nothing
    /// was written.</exception>
    /// <exception cref="InvalidOperationException">The store cannot be used in this session.</exception>
    /// <exception cref="HttpRequestException">The store could not be reached.</exception>
    Task<string> SetAsync(E2eKeyScope scope, string value, string ifMatch, CancellationToken ct = default);

    /// <summary>
    /// Stores the user's keys for a scope only if it has none yet, never replacing existing ones, so two devices cannot
    /// both create them.
    /// </summary>
    /// <param name="scope">The key scope.</param>
    /// <param name="value">The value to store.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The new version when stored; null when the user already has keys, which are left as they are.</returns>
    /// <exception cref="InvalidOperationException">The store cannot be used in this session.</exception>
    /// <exception cref="HttpRequestException">The store could not be reached.</exception>
    Task<string?> TryCreateAsync(E2eKeyScope scope, string value, CancellationToken ct = default);

    /// <summary>
    /// Deletes the user's keys for a scope. Does nothing when there are none. A store may keep them restorable by an
    /// operator for a while (the data bucket keeps replaced and deleted versions for 30 days); the app treats them as
    /// gone.
    /// </summary>
    /// <param name="scope">The key scope.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A task that completes when the keys are deleted.</returns>
    /// <exception cref="InvalidOperationException">The store cannot be used in this session.</exception>
    /// <exception cref="HttpRequestException">The store could not be reached.</exception>
    Task DeleteAsync(E2eKeyScope scope, CancellationToken ct = default);
}
