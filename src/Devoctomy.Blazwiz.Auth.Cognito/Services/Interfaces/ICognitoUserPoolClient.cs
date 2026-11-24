using Devoctomy.Blazwiz.Auth.Cognito.Models;
using Devoctomy.Blazwiz.Auth.Models;

namespace Devoctomy.Blazwiz.Auth.Cognito.Services.Interfaces;

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
    /// Starts a sign-in with the user's password from a remembered device, which answers Cognito's device challenges
    /// so no code from the authenticator app is asked for.
    /// </summary>
    /// <param name="username">The username (email).</param>
    /// <param name="password">The password.</param>
    /// <param name="device">The remembered device, or null to sign in as <see cref="SignInAsync(string, string)"/>.</param>
    /// <returns>The tokens, or the next challenge.</returns>
    /// <exception cref="Exceptions.CognitoException">Cognito rejected the sign-in, e.g. <c>ResourceNotFoundException</c> for a forgotten device.</exception>
    /// <exception cref="HttpRequestException">Cognito could not be reached.</exception>
    Task<CognitoSignInResult> SignInAsync(string username, string password, CognitoDeviceCredential? device);

    /// <summary>
    /// Answers the <c>SOFTWARE_TOKEN_MFA</c> challenge from the last <see cref="SignInAsync(string, string)"/>.
    /// </summary>
    /// <param name="sessionId">The session from the challenge.</param>
    /// <param name="code">The code from the authenticator app.</param>
    /// <returns>The tokens, or the next challenge.</returns>
    /// <exception cref="Exceptions.CognitoException">Cognito rejected the code (<c>CodeMismatchException</c>) or the session has expired.</exception>
    /// <exception cref="HttpRequestException">Cognito could not be reached.</exception>
    Task<CognitoSignInResult> RespondToMfaAsync(string sessionId, string code);

    /// <summary>
    /// Starts answering the <c>MFA_SETUP</c> challenge from the last <see cref="SignInAsync(string, string)"/>: gets a
    /// secret for the user's authenticator app.
    /// </summary>
    /// <param name="sessionId">The session from the challenge.</param>
    /// <returns>The secret, and the session to finish with.</returns>
    /// <exception cref="Exceptions.CognitoException">Cognito rejected the request or the session has expired.</exception>
    /// <exception cref="HttpRequestException">Cognito could not be reached.</exception>
    Task<(string SecretCode, string SessionId)> BeginMfaSetupAsync(string sessionId);

    /// <summary>
    /// Finishes the <c>MFA_SETUP</c> challenge: confirms the authenticator app with a code it shows, and signs in.
    /// </summary>
    /// <param name="sessionId">The session from <see cref="BeginMfaSetupAsync"/>.</param>
    /// <param name="code">The code from the authenticator app.</param>
    /// <returns>The tokens, or the next challenge, with the session to answer it with.</returns>
    /// <exception cref="Exceptions.CognitoException">Cognito rejected the code or the session has expired.</exception>
    /// <exception cref="HttpRequestException">Cognito could not be reached.</exception>
    Task<CognitoSignInResult> CompleteMfaSetupAsync(string sessionId, string code);

    /// <summary>
    /// Confirms the device a sign in came from (its <see cref="CognitoSignInResult.NewDeviceKey"/>), so Cognito
    /// remembers it: makes up a random password for it and registers its verifier.
    /// </summary>
    /// <param name="accessToken">The session's access token.</param>
    /// <param name="deviceKey">The new device's key.</param>
    /// <param name="deviceGroupKey">The new device's group key.</param>
    /// <param name="deviceName">The name to list the device under.</param>
    /// <returns>The device's credential, to sign in from it later.</returns>
    /// <exception cref="Exceptions.CognitoException">Cognito rejected the request.</exception>
    /// <exception cref="HttpRequestException">Cognito could not be reached.</exception>
    Task<CognitoDeviceCredential> ConfirmDeviceAsync(string accessToken, string deviceKey, string deviceGroupKey, string deviceName);

    /// <summary>
    /// Lists the user's remembered devices.
    /// </summary>
    /// <param name="accessToken">The session's access token.</param>
    /// <returns>The devices, none of them marked as this one.</returns>
    /// <exception cref="Exceptions.CognitoException">Cognito rejected the request.</exception>
    /// <exception cref="HttpRequestException">Cognito could not be reached.</exception>
    Task<IReadOnlyList<RememberedDevice>> ListDevicesAsync(string accessToken);

    /// <summary>
    /// Forgets one of the user's devices.
    /// </summary>
    /// <param name="accessToken">The session's access token.</param>
    /// <param name="deviceKey">The device's key.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="Exceptions.CognitoException">Cognito rejected the request.</exception>
    /// <exception cref="HttpRequestException">Cognito could not be reached.</exception>
    Task ForgetDeviceAsync(string accessToken, string deviceKey);

    /// <summary>
    /// Gets whether the user has an authenticator app (TOTP) turned on.
    /// </summary>
    /// <param name="accessToken">The session's access token.</param>
    /// <returns>True when it is on.</returns>
    /// <exception cref="Exceptions.CognitoException">Cognito rejected the request.</exception>
    /// <exception cref="HttpRequestException">Cognito could not be reached.</exception>
    Task<bool> IsSoftwareTokenMfaEnabledAsync(string accessToken);

    /// <summary>
    /// Gets a new secret for the user's authenticator app.
    /// </summary>
    /// <param name="accessToken">The session's access token.</param>
    /// <returns>The secret.</returns>
    /// <exception cref="Exceptions.CognitoException">Cognito rejected the request.</exception>
    /// <exception cref="HttpRequestException">Cognito could not be reached.</exception>
    Task<string> AssociateSoftwareTokenAsync(string accessToken);

    /// <summary>
    /// Confirms the authenticator app set up with the last <see cref="AssociateSoftwareTokenAsync"/>, with a code it
    /// shows.
    /// </summary>
    /// <param name="accessToken">The session's access token.</param>
    /// <param name="code">The code.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="Exceptions.CognitoException">Cognito rejected the code.</exception>
    /// <exception cref="HttpRequestException">Cognito could not be reached.</exception>
    Task VerifySoftwareTokenAsync(string accessToken, string code);

    /// <summary>
    /// Turns the authenticator app on (as the preferred second factor) or off.
    /// </summary>
    /// <param name="accessToken">The session's access token.</param>
    /// <param name="enabled">Whether it is on.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="Exceptions.CognitoException">Cognito rejected the request.</exception>
    /// <exception cref="HttpRequestException">Cognito could not be reached.</exception>
    Task SetSoftwareTokenMfaAsync(string accessToken, bool enabled);

    /// <summary>
    /// Answers the <c>NEW_PASSWORD_REQUIRED</c> challenge from the last <see cref="SignInAsync(string, string)"/>.
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

    /// <summary>
    /// Signs the user out on every device: revokes all their refresh tokens, and the access and ID tokens issued from
    /// them.
    /// </summary>
    /// <param name="accessToken">The session's access token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="Exceptions.CognitoException">Cognito rejected the request, e.g. the token is already revoked.</exception>
    /// <exception cref="HttpRequestException">Cognito could not be reached.</exception>
    Task GlobalSignOutAsync(string accessToken);

    /// <summary>
    /// Checks an access token is still accepted (Cognito's <c>GetUser</c>): refused once the user signed out
    /// everywhere.
    /// </summary>
    /// <param name="accessToken">The session's access token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="Exceptions.CognitoException">Cognito refused the token (<c>NotAuthorizedException</c>) or the request.</exception>
    /// <exception cref="HttpRequestException">Cognito could not be reached.</exception>
    Task CheckAccessTokenAsync(string accessToken);

    /// <summary>
    /// Changes a signed-in user's password.
    /// </summary>
    /// <param name="username">The user's username.</param>
    /// <param name="idToken">The session's ID token.</param>
    /// <param name="accessToken">The session's access token.</param>
    /// <param name="expiresAt">When the tokens expire.</param>
    /// <param name="currentPassword">The current password.</param>
    /// <param name="newPassword">The new password.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task ChangePasswordAsync(string username, string idToken, string accessToken, DateTimeOffset expiresAt, string currentPassword, string newPassword);

    /// <summary>
    /// Sends the user a verification code for resetting their password.
    /// </summary>
    /// <param name="username">The user's username.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task ForgotPasswordAsync(string username);

    /// <summary>
    /// Sets a new password with the verification code sent by <see cref="ForgotPasswordAsync"/>.
    /// </summary>
    /// <param name="username">The user's username.</param>
    /// <param name="code">The verification code.</param>
    /// <param name="newPassword">The new password.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task ConfirmForgotPasswordAsync(string username, string code, string newPassword);
}
