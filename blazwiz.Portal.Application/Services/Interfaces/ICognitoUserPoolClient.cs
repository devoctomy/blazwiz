using blazwiz.Portal.Models.Aws;

namespace blazwiz.Portal.Application.Services.Interfaces;

/// <summary>
/// Signs in to the Cognito user pool. Wraps AWS's Amazon.Extensions.CognitoAuthentication library,
/// which implements the SRP protocol, so the rest of the app can be unit tested without AWS.
/// </summary>
public interface ICognitoUserPoolClient
{
    /// <summary>
    /// Starts a sign-in with the user's password (SRP, the password itself is never sent).
    /// </summary>
    /// <param name="username">The username (email).</param>
    /// <param name="password">The password.</param>
    /// <returns>The tokens, or the next challenge.</returns>
    /// <exception cref="Exceptions.CognitoException">Cognito rejected the sign-in.</exception>
    /// <exception cref="HttpRequestException">Cognito could not be reached.</exception>
    Task<CognitoSignInResult> SignInAsync(string username, string password);

    /// <summary>
    /// Answers the <c>NEW_PASSWORD_REQUIRED</c> challenge from the last <see cref="SignInAsync"/>.
    /// </summary>
    /// <param name="sessionId">The session from the challenge.</param>
    /// <param name="newPassword">The new password.</param>
    /// <returns>The tokens, or the next challenge.</returns>
    /// <exception cref="Exceptions.CognitoException">Cognito rejected the new password or the session has expired.</exception>
    /// <exception cref="HttpRequestException">Cognito could not be reached.</exception>
    Task<CognitoSignInResult> CompleteNewPasswordAsync(string sessionId, string newPassword);

    /// <summary>
    /// Gets new ID and access tokens using a refresh token.
    /// </summary>
    /// <param name="username">The username the refresh token belongs to.</param>
    /// <param name="refreshToken">The refresh token.</param>
    /// <returns>The new tokens.</returns>
    /// <exception cref="Exceptions.CognitoException">The refresh token has expired or been revoked.</exception>
    /// <exception cref="HttpRequestException">Cognito could not be reached.</exception>
    Task<CognitoSignInResult> RefreshAsync(string username, string refreshToken);

    /// <summary>
    /// Revokes a refresh token and the tokens issued from it.
    /// </summary>
    /// <param name="refreshToken">The refresh token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="Exceptions.CognitoException">Cognito rejected the request.</exception>
    /// <exception cref="HttpRequestException">Cognito could not be reached.</exception>
    Task RevokeAsync(string refreshToken);
}
