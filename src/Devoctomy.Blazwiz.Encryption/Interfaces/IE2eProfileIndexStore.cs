namespace Devoctomy.Blazwiz.Encryption.Interfaces;

/// <summary>
/// Keeps the signed in user's index of protected profiles (<see cref="Models.E2eProfileIndex"/>) somewhere only they
/// can reach, beside their keys documents (the app stores it in their own Secrets Manager secret). The value is opaque
/// to the store.
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
    /// <returns>The stored value, or null when the user has none yet.</returns>
    /// <exception cref="InvalidOperationException">The store cannot be used in this session.</exception>
    /// <exception cref="HttpRequestException">The store could not be reached.</exception>
    Task<string?> GetIndexAsync(CancellationToken ct = default);

    /// <summary>
    /// Stores the user's index, creating it the first time or replacing it. The last write wins.
    /// </summary>
    /// <param name="value">The value to store.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A task that completes when the value is stored.</returns>
    /// <exception cref="InvalidOperationException">The store cannot be used in this session.</exception>
    /// <exception cref="HttpRequestException">The store could not be reached.</exception>
    Task SetIndexAsync(string value, CancellationToken ct = default);
}
