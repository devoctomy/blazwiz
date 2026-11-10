using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Amazon.Extensions.CognitoAuthentication;
using Amazon.Runtime;
using Devoctomy.Blazwiz.Auth.Cognito.Exceptions;
using Devoctomy.Blazwiz.Auth.Cognito.Models;
using Devoctomy.Blazwiz.Auth.Cognito.Services.Interfaces;

namespace Devoctomy.Blazwiz.Auth.Cognito.Services;

/// <summary>
/// <see cref="ICognitoUserPoolClient"/> using AWS's Amazon.Extensions.CognitoAuthentication library for
/// SRP sign-in, the new password challenge, token refresh and password changes and resets. Only translates to and
/// from the library's types.
/// </summary>
public class CognitoUserPoolClient : ICognitoUserPoolClient
{
    private readonly IAmazonCognitoIdentityProvider provider;
    private readonly AwsCognitoConfig cognitoConfig;
    private readonly CognitoUserPool userPool;

    private CognitoUser? challengedUser;

    /// <summary>
    /// Initializes a new instance of the <see cref="CognitoUserPoolClient"/> class.
    /// </summary>
    /// <param name="provider">The Cognito user pool API client.</param>
    /// <param name="cognitoConfig">The Cognito settings.</param>
    public CognitoUserPoolClient(IAmazonCognitoIdentityProvider provider, AwsCognitoConfig cognitoConfig)
    {
        this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        this.cognitoConfig = cognitoConfig ?? throw new ArgumentNullException(nameof(cognitoConfig));
        userPool = new CognitoUserPool(cognitoConfig.UserPoolId, cognitoConfig.UserPoolClientId, provider);
    }

    /// <inheritdoc/>
    public Task<CognitoSignInResult> SignInAsync(string username, string password)
    {
        return CallAsync(async () =>
        {
            challengedUser = null;
            var user = CreateUser(username);
            var response = await user.StartWithSrpAuthAsync(new InitiateSrpAuthRequest { Password = password });
            if (response.AuthenticationResult == null)
            {
                challengedUser = user;
            }

            return ToResult(response);
        });
    }

    /// <inheritdoc/>
    public Task<CognitoSignInResult> CompleteNewPasswordAsync(string sessionId, string newPassword)
    {
        return CallAsync(async () =>
        {
            var user = challengedUser ?? throw new InvalidOperationException("There is no sign-in waiting for a new password.");
            var response = await user.RespondToNewPasswordRequiredAsync(new RespondToNewPasswordRequiredRequest
            {
                SessionID = sessionId,
                NewPassword = newPassword,
            });

            if (response.AuthenticationResult != null)
            {
                challengedUser = null;
            }

            return ToResult(response);
        });
    }

    /// <inheritdoc/>
    public Task<CognitoSignInResult> RefreshAsync(string username, string refreshToken)
    {
        return CallAsync(async () =>
        {
            var user = CreateUser(username);
            var now = DateTime.UtcNow;
            user.SessionTokens = new CognitoUserSession(null, null, refreshToken, now, now);
            var response = await user.StartWithRefreshTokenAuthAsync(new InitiateRefreshTokenAuthRequest
            {
                AuthFlowType = AuthFlowType.REFRESH_TOKEN_AUTH,
            });

            return ToResult(response);
        });
    }

    /// <inheritdoc/>
    public Task RevokeAsync(string refreshToken)
    {
        return CallAsync(async () =>
        {
            await provider.RevokeTokenAsync(new RevokeTokenRequest
            {
                ClientId = cognitoConfig.UserPoolClientId,
                Token = refreshToken,
            });

            return true;
        });
    }

    /// <inheritdoc/>
    public Task GlobalSignOutAsync(string accessToken)
    {
        return CallAsync(async () =>
        {
            await provider.GlobalSignOutAsync(new GlobalSignOutRequest { AccessToken = accessToken });
            return true;
        });
    }

    /// <inheritdoc/>
    public Task CheckAccessTokenAsync(string accessToken)
    {
        return CallAsync(async () =>
        {
            await provider.GetUserAsync(new GetUserRequest { AccessToken = accessToken });
            return true;
        });
    }

    /// <inheritdoc/>
    public Task ChangePasswordAsync(string username, string idToken, string accessToken, DateTimeOffset expiresAt, string currentPassword, string newPassword)
    {
        return CallAsync(async () =>
        {
            var user = CreateUser(username);
            user.SessionTokens = new CognitoUserSession(idToken, accessToken, null, DateTime.UtcNow, expiresAt.UtcDateTime);
            await user.ChangePasswordAsync(currentPassword, newPassword);
            return true;
        });
    }

    /// <inheritdoc/>
    public Task ForgotPasswordAsync(string username)
    {
        return CallAsync(async () =>
        {
            await CreateUser(username).ForgotPasswordAsync();
            return true;
        });
    }

    /// <inheritdoc/>
    public Task ConfirmForgotPasswordAsync(string username, string code, string newPassword)
    {
        return CallAsync(async () =>
        {
            await CreateUser(username).ConfirmForgotPasswordAsync(code, newPassword);
            return true;
        });
    }

    private static CognitoSignInResult ToResult(AuthFlowResponse response)
    {
        var tokens = response.AuthenticationResult;
        return new CognitoSignInResult
        {
            IdToken = tokens?.IdToken,
            AccessToken = tokens?.AccessToken,
            RefreshToken = tokens?.RefreshToken,
            ExpiresIn = tokens?.ExpiresIn ?? 0,
            ChallengeName = response.ChallengeName?.Value,
            SessionId = response.SessionID,
        };
    }

    private static async Task<T> CallAsync<T>(Func<Task<T>> call)
    {
        try
        {
            return await call();
        }
        catch (AmazonCognitoIdentityProviderException ex)
        {
            // E.g. NotAuthorizedException for a wrong password, InvalidPasswordException for a weak new password.
            throw new CognitoException(ex.ErrorCode ?? ex.GetType().Name, ex.Message);
        }
        catch (AmazonServiceException ex) when (ex.InnerException is HttpRequestException)
        {
            throw new HttpRequestException(ex.Message, ex);
        }
    }

    private CognitoUser CreateUser(string username)
    {
        return new CognitoUser(username, cognitoConfig.UserPoolClientId, userPool, provider);
    }
}
