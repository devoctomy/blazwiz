using Amazon;
using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Amazon.Extensions.CognitoAuthentication;
using Amazon.Runtime;
using blazwiz.Portal.Application.Exceptions;
using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Models.Aws;

namespace blazwiz.Portal.Application.Services;

/// <summary>
/// <see cref="ICognitoUserPoolClient"/> using AWS's Amazon.Extensions.CognitoAuthentication library for
/// SRP sign-in, the new password challenge and token refresh. Only translates to and from the library's types.
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

    /// <summary>
    /// Creates the Cognito user pool API client. The sign-in APIs are public, so no AWS credentials are used.
    /// </summary>
    /// <param name="cognitoConfig">The Cognito settings.</param>
    /// <returns>The API client.</returns>
    public static IAmazonCognitoIdentityProvider CreateProvider(AwsCognitoConfig cognitoConfig)
    {
        ArgumentNullException.ThrowIfNull(cognitoConfig);
        return new AmazonCognitoIdentityProviderClient(
            new AnonymousAWSCredentials(),
            new AmazonCognitoIdentityProviderConfig
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(cognitoConfig.Region),
                HttpClientFactory = new BrowserHttpClientFactory(),
            });
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

    /// <summary>
    /// Gives the AWS SDK an <see cref="HttpClient"/> that works in the browser. The SDK's default handler sets
    /// options (e.g. connection limits) the browser's fetch based handler does not support, and the SDK reads
    /// responses synchronously, which the browser does not allow, so responses are buffered first.
    /// </summary>
    private sealed class BrowserHttpClientFactory : HttpClientFactory
    {
        private static readonly HttpClient SharedClient = new HttpClient(new BufferingHandler(new HttpClientHandler()));

        public override HttpClient CreateHttpClient(IClientConfig clientConfig) => SharedClient;

        public override bool UseSDKHttpClientCaching(IClientConfig clientConfig) => false;

        public override bool DisposeHttpClientsAfterUse(IClientConfig clientConfig) => false;
    }

    /// <summary>
    /// Reads each response body into memory asynchronously, so later synchronous reads come from the buffer.
    /// </summary>
    private sealed class BufferingHandler : DelegatingHandler
    {
        public BufferingHandler(HttpMessageHandler innerHandler)
            : base(innerHandler)
        {
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            await response.Content.LoadIntoBufferAsync(cancellationToken);
            return response;
        }
    }
}
