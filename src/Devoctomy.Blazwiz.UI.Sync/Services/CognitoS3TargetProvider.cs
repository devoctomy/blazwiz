using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Devoctomy.Blazwiz.Auth.Cognito.Exceptions;
using Devoctomy.Blazwiz.Auth.Cognito.Models;
using Devoctomy.Blazwiz.Auth.Cognito.Services;
using Devoctomy.Blazwiz.Auth.Cognito.Services.Interfaces;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;
using Devoctomy.Blazwiz.UI.Sync.Models;

namespace Devoctomy.Blazwiz.UI.Sync.Services;

/// <summary>
/// Supplies the signed in user's S3 target: the identity id from the session's <see cref="IAwsCredentialsProvider"/>
/// credentials, and an S3 client that works in the browser, kept until the credentials change. Returns null with the
/// placeholder sign-in, or when no identity pool or data bucket is configured, so the app simply has no cloud sync.
/// </summary>
public sealed class CognitoS3TargetProvider(
    IAwsCredentialsProvider credentialsProvider,
    AwsS3Config s3Config,
    BrowserHttpClientFactory httpClientFactory)
    : IS3SyncTargetProvider, IDisposable
{
    private readonly IAwsCredentialsProvider credentialsProvider = credentialsProvider ?? throw new ArgumentNullException(nameof(credentialsProvider));
    private readonly AwsS3Config s3Config = s3Config ?? throw new ArgumentNullException(nameof(s3Config));
    private readonly BrowserHttpClientFactory httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));

    private CachedTarget? cached;

    /// <summary>
    /// Gets a value indicating whether a data bucket is configured.
    /// </summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(s3Config.Region)
        && !string.IsNullOrWhiteSpace(s3Config.DataBucketName);

    /// <inheritdoc/>
    public async Task<S3SyncTarget?> GetTargetAsync(CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            return null;
        }

        AwsTemporaryCredentials? credentials;
        try
        {
            credentials = await credentialsProvider.GetCredentialsAsync(ct);
        }
        catch (CognitoException)
        {
            // The identity pool refused the token: no credentials (reported as offline, not as an error).
            credentials = null;
        }

        if (credentials == null)
        {
            DisposeTarget();
            return null;
        }

        if (cached != null && ReferenceEquals(cached.Credentials, credentials))
        {
            return cached.Target;
        }

        DisposeTarget();
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
        cached = new CachedTarget(credentials, target);
        return target;
    }

    /// <inheritdoc/>
    public void Invalidate()
    {
        // AWS rejected the credentials early, so everything sharing them needs new ones too.
        credentialsProvider.Clear();
        DisposeTarget();
    }

    /// <inheritdoc/>
    public void Dispose() => DisposeTarget();

    private void DisposeTarget()
    {
        cached?.Target.Client.Dispose();
        cached = null;
    }

    private sealed class CachedTarget(AwsTemporaryCredentials credentials, S3SyncTarget target)
    {
        public AwsTemporaryCredentials Credentials { get; } = credentials;

        public S3SyncTarget Target { get; } = target;
    }
}
