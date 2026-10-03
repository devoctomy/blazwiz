namespace blazwiz.Portal.Application.Services.Interfaces;

/// <summary>
/// Keeps the signed in user's keys in their own Secrets Manager secret, named <c>&lt;user pool sub&gt;/keys</c>.
/// The value is opaque to the store; callers decide its format.
/// </summary>
public interface IUserKeysStore
{
    /// <summary>
    /// Gets the user's stored keys.
    /// </summary>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The stored value, or null when the user has none yet.</returns>
    /// <exception cref="InvalidOperationException">Not signed in with Cognito.</exception>
    /// <exception cref="Exceptions.CognitoException">The identity pool refused the token.</exception>
    /// <exception cref="HttpRequestException">AWS could not be reached.</exception>
    Task<string?> GetAsync(CancellationToken ct = default);

    /// <summary>
    /// Stores the user's keys, creating their secret the first time.
    /// </summary>
    /// <param name="value">The value to store.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A task that completes when the value is stored.</returns>
    /// <exception cref="InvalidOperationException">Not signed in with Cognito.</exception>
    /// <exception cref="Exceptions.CognitoException">The identity pool refused the token.</exception>
    /// <exception cref="HttpRequestException">AWS could not be reached.</exception>
    Task SetAsync(string value, CancellationToken ct = default);

    /// <summary>
    /// Deletes the user's keys straight away, with no recovery window. Does nothing when there are none.
    /// </summary>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A task that completes when the keys are deleted.</returns>
    /// <exception cref="InvalidOperationException">Not signed in with Cognito.</exception>
    /// <exception cref="Exceptions.CognitoException">The identity pool refused the token.</exception>
    /// <exception cref="HttpRequestException">AWS could not be reached.</exception>
    Task DeleteAsync(CancellationToken ct = default);
}
