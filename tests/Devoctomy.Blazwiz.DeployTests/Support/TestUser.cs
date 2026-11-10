using Amazon;
using Amazon.CognitoIdentity;
using Amazon.CognitoIdentity.Model;
using Amazon.CognitoIdentityProvider;
using Amazon.Extensions.CognitoAuthentication;
using Amazon.Runtime;
using Amazon.S3;

namespace Devoctomy.Blazwiz.DeployTests.Support;

/// <summary>
/// The test user, signed in as the portal signs users in: SRP against the user pool (the password never leaves this
/// process), then AWS credentials from the identity pool for the ID token. Nothing here uses AWS credentials of its own.
/// </summary>
public sealed class TestUser(PortalSettings portal, string email, string password) : IDisposable
{
    private readonly RegionEndpoint region = RegionEndpoint.GetBySystemName(portal.Region);
    private AmazonCognitoIdentityProviderClient? userPoolClient;

    public string? IdToken { get; private set; }

    public string? AccessToken { get; private set; }

    public string? IdentityId { get; private set; }

    public Credentials? Credentials { get; private set; }

    /// <summary>
    /// Gets the user pool API, called anonymously as the browser calls it.
    /// </summary>
    public AmazonCognitoIdentityProviderClient UserPool =>
        userPoolClient ??= new AmazonCognitoIdentityProviderClient(new AnonymousAWSCredentials(), region);

    public async Task SignInAsync()
    {
        var pool = new CognitoUserPool(portal.UserPoolId, portal.UserPoolClientId, UserPool);
        var user = new CognitoUser(email, portal.UserPoolClientId, pool, UserPool);
        var response = await user.StartWithSrpAuthAsync(new InitiateSrpAuthRequest { Password = password });
        var result = response.AuthenticationResult
            ?? throw new InvalidOperationException($"Sign in asked for {response.ChallengeName}: set the test user up with deploy.ps1 first.");
        IdToken = result.IdToken;
        AccessToken = result.AccessToken;
    }

    public async Task GetCredentialsAsync()
    {
        var token = IdToken ?? throw new InvalidOperationException("Sign in first.");
        var logins = new Dictionary<string, string> { [$"cognito-idp.{portal.Region}.amazonaws.com/{portal.UserPoolId}"] = token };
        using var identity = new AmazonCognitoIdentityClient(new AnonymousAWSCredentials(), region);
        IdentityId = (await identity.GetIdAsync(new GetIdRequest { IdentityPoolId = portal.IdentityPoolId, Logins = logins })).IdentityId;
        Credentials = (await identity.GetCredentialsForIdentityAsync(new GetCredentialsForIdentityRequest { IdentityId = IdentityId, Logins = logins })).Credentials;
    }

    /// <summary>
    /// Creates an S3 client with the user's credentials.
    /// </summary>
    /// <returns>The client.</returns>
    public AmazonS3Client CreateS3Client()
    {
        var credentials = Credentials ?? throw new InvalidOperationException("Get the user's AWS credentials first.");
        return new AmazonS3Client(new SessionAWSCredentials(credentials.AccessKeyId, credentials.SecretKey, credentials.SessionToken), region);
    }

    public void Dispose() => userPoolClient?.Dispose();
}
