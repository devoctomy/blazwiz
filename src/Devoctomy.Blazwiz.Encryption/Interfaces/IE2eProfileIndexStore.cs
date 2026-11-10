using Devoctomy.Blazwiz.Encryption.Exceptions;
using Devoctomy.Blazwiz.Encryption.Models;

namespace Devoctomy.Blazwiz.Encryption.Interfaces;

/// <summary>
/// Keeps the signed in user's index of protected profiles (<see cref="E2eProfileIndex"/>) somewhere only they can
/// reach, beside their keys documents (the app keeps it in the user's prefix of the data bucket, at
/// <c>keys/index</c>). The value is opaque to the store. Every write is conditional, so two devices changing the index
/// cannot both succeed.
/// </summary>
/// <remarks>
/// Implementations throw <see cref="InvalidOperationException"/> when the store cannot be used in this session and
/// <see cref="HttpRequestException"/> when it cannot be reached, as <see cref="IUserKeysStore"/> does.
/// </remarks>
public interface IE2eProfileIndexStore
{
    /// <summary>
    /// Gets the user's index.
    /// </summary>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The stored value with its version, or null when the user has none yet.</returns>
    /// <exception cref="InvalidOperationException">The store cannot be used in this session.</exception>
    /// <exception cref="HttpRequestException">The store could not be reached.</exception>
    Task<StoredDocument?> GetIndexAsync(CancellationToken ct = default);

    /// <summary>
    /// Stores the user's index, only if it is still as it was read: at version <paramref name="ifMatch"/>, or, when
    /// that is null, not there at all.
    /// </summary>
    /// <param name="value">The value to store.</param>
    /// <param name="ifMatch">The version the index was read at (<see cref="StoredDocument.ETag"/>), or null when there
    /// was none, to create it.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The new version.</returns>
    /// <exception cref="E2eDocumentChangedException">The index changed, was created or was deleted since it was read;
    /// nothing was written.</exception>
    /// <exception cref="InvalidOperationException">The store cannot be used in this session.</exception>
    /// <exception cref="HttpRequestException">The store could not be reached.</exception>
    Task<string> SetIndexAsync(string value, string? ifMatch, CancellationToken ct = default);
}
