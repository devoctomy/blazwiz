using blazwiz.Portal.Models.Aws;

namespace blazwiz.Portal.Application.Services.Interfaces;

/// <summary>
/// Supplies the signed in user's temporary AWS credentials from the Cognito identity pool, held in memory for the
/// session and shared by everything that calls AWS, so Cognito is only asked again when they are close to expiry.
/// </summary>
public interface IAwsCredentialsProvider
{
    /// <summary>
    /// Gets the user's credentials, fetching them the first time and again shortly before they expire.
    /// </summary>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The credentials, or null with the placeholder sign-in, when signed out, or when no identity pool is configured.</returns>
    /// <exception cref="Exceptions.CognitoException">The identity pool refused the token.</exception>
    /// <exception cref="HttpRequestException">Cognito could not be reached.</exception>
    Task<AwsTemporaryCredentials?> GetCredentialsAsync(CancellationToken ct = default);

    /// <summary>
    /// Forgets the held credentials, e.g. on sign out or when AWS rejected them early.
    /// </summary>
    void Clear();
}
