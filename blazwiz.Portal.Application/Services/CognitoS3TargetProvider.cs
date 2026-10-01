using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using blazwiz.Core.Sync.Interfaces;
using blazwiz.Core.Sync.Models;
using blazwiz.Core.Time.Interfaces;
using blazwiz.Portal.Application.Exceptions;
using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Models.Aws;

namespace blazwiz.Portal.Application.Services;

/// <summary>
/// Supplies the signed in user's S3 target: temporary credentials and the identity id from the Cognito identity pool,
/// and an S3 client that works in the browser. Cached until the credentials are close to expiry. Returns null with
/// the placeholder sign-in, or when no identity pool or data bucket is configured, so the app simply has no cloud sync.
/// </summary>
public sealed class CognitoS3TargetProvider(
    IAuthenticationService authService,
    ICognitoIdentityClient identityClient,
    AwsCognitoConfig cognitoConfig,
    AwsS3Config s3Config,
    BrowserHttpClientFactory httpClientFactory,
    ISystemClock systemClock)
    : IS3SyncTargetProvider, IDisposable
{
    /// <summary>
    /// How long before the credentials expire new ones are fetched.
    /// </summary>
    public static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(5);

    private readonly IAuthenticationService authService = authService ?? throw new ArgumentNullException(nameof(authService));
    private readonly ICognitoIdentityClient identityClient = identityClient ?? throw new ArgumentNullException(nameof(identityClient));
    private readonly AwsCognitoConfig cognitoConfig = cognitoConfig ?? throw new ArgumentNullException(nameof(cognitoConfig));
    private readonly AwsS3Config s3Config = s3Config ?? throw new ArgumentNullException(nameof(s3Config));
    private readonly BrowserHttpClientFactory httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    private readonly ISystemClock systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));

    private CachedTarget? cached;

    /// <summary>
    /// Gets a value indicating whether an identity pool and data bucket are configured.
    /// </summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(cognitoConfig.Region)
        && !string.IsNullOrWhiteSpace(cognitoConfig.UserPoolId)
        && !string.IsNullOrWhiteSpace(cognitoConfig.IdentityPoolId)
        && !string.IsNullOrWhiteSpace(s3Config.Region)
        && !string.IsNullOrWhiteSpace(s3Config.DataBucketName);

    /// <inheritdoc/>
    public async Task<S3SyncTarget?> GetTargetAsync(CancellationToken ct = default)
    {
        if (authService.IsPlaceholder || !IsConfigured)
        {
            return null;
        }

        var session = await authService.GetCurrentUserAsync();
        if (string.IsNullOrEmpty(session?.IdToken))
        {
            return null;
        }

        if (cached != null && cached.Username == session.Username && cached.Expiration - RefreshMargin > systemClock.UtcNow)
        {
            return cached.Target;
        }

        AwsTemporaryCredentials credentials;
        try
        {
            credentials = await identityClient.GetCredentialsAsync(session.IdToken, ct);
        }
        catch (CognitoException)
        {
            // The identity pool refused the token: no credentials (reported as offline, not as an error).
            return null;
        }

        Invalidate();
        var client = new AmazonS3Client(
            new SessionAWSCredentials(credentials.AccessKeyId, credentials.SecretKey, credentials.SessionToken),
            new AmazonS3Config
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(s3Config.Region),

                // The SDK reads responses synchronously, which the browser only allows from a buffer.
                HttpClientFactory = httpClientFactory,

                // Browsers cannot send the aws-chunked trailing checksums the SDK adds by default.
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
            });
        var target = new S3SyncTarget(client, s3Config.DataBucketName, credentials.IdentityId + "/");
        cached = new CachedTarget(session.Username, target, credentials.Expiration);
        return target;
    }

    /// <inheritdoc/>
    public void Invalidate()
    {
        cached?.Target.Client.Dispose();
        cached = null;
    }

    /// <inheritdoc/>
    public void Dispose() => Invalidate();

    private sealed class CachedTarget(string username, S3SyncTarget target, DateTimeOffset expiration)
    {
        public string Username { get; } = username;

        public S3SyncTarget Target { get; } = target;

        public DateTimeOffset Expiration { get; } = expiration;
    }
}
