using Amazon.CognitoIdentity;
using Amazon.CognitoIdentity.Model;
using Amazon.Runtime;
using Devoctomy.Blazwiz.Auth.Cognito.Exceptions;
using Devoctomy.Blazwiz.Auth.Cognito.Models;
using Devoctomy.Blazwiz.Auth.Cognito.Services.Interfaces;

namespace Devoctomy.Blazwiz.Auth.Cognito.Services;

/// <summary>
/// <see cref="ICognitoIdentityClient"/> using AWS's AWSSDK.CognitoIdentity: <c>GetId</c> then
/// <c>GetCredentialsForIdentity</c> with the ID token as the login. Only translates to and from the library's types.
/// </summary>
public class CognitoIdentityClient(IAmazonCognitoIdentity identity, AwsCognitoConfig cognitoConfig)
    : ICognitoIdentityClient
{
    private readonly IAmazonCognitoIdentity identity = identity ?? throw new ArgumentNullException(nameof(identity));
    private readonly AwsCognitoConfig cognitoConfig = cognitoConfig ?? throw new ArgumentNullException(nameof(cognitoConfig));

    /// <inheritdoc/>
    public async Task<AwsTemporaryCredentials> GetCredentialsAsync(string idToken, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(idToken);

        var logins = new Dictionary<string, string>
        {
            [$"cognito-idp.{cognitoConfig.Region}.amazonaws.com/{cognitoConfig.UserPoolId}"] = idToken,
        };

        try
        {
            var id = await identity.GetIdAsync(new GetIdRequest { IdentityPoolId = cognitoConfig.IdentityPoolId, Logins = logins }, ct);
            var response = await identity.GetCredentialsForIdentityAsync(new GetCredentialsForIdentityRequest { IdentityId = id.IdentityId, Logins = logins }, ct);
            var credentials = response.Credentials;
            return new AwsTemporaryCredentials
            {
                IdentityId = response.IdentityId ?? id.IdentityId,
                AccessKeyId = credentials.AccessKeyId,
                SecretKey = credentials.SecretKey,
                SessionToken = credentials.SessionToken,
                Expiration = new DateTimeOffset(DateTime.SpecifyKind(credentials.Expiration ?? DateTime.UtcNow, DateTimeKind.Utc)),
            };
        }
        catch (AmazonCognitoIdentityException ex)
        {
            throw new CognitoException(ex.ErrorCode ?? ex.GetType().Name, ex.Message);
        }
        catch (AmazonServiceException ex) when (ex.InnerException is HttpRequestException)
        {
            throw new HttpRequestException(ex.Message, ex);
        }
    }
}
