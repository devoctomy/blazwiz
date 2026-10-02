using blazwiz.Portal.Models.Aws;

namespace blazwiz.Portal.Application.Services.Interfaces;

/// <summary>
/// Exchanges a user pool ID token for temporary AWS credentials through the Cognito identity pool. Wraps
/// AWSSDK.CognitoIdentity, so the rest of the app can be unit tested without AWS.
/// </summary>
public interface ICognitoIdentityClient
{
    /// <summary>
    /// Gets the user's identity id and temporary credentials.
    /// </summary>
    /// <param name="idToken">The user pool ID token.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The credentials.</returns>
    /// <exception cref="Exceptions.CognitoException">The identity pool refused the token.</exception>
    /// <exception cref="HttpRequestException">Cognito could not be reached.</exception>
    Task<AwsTemporaryCredentials> GetCredentialsAsync(string idToken, CancellationToken ct = default);
}
