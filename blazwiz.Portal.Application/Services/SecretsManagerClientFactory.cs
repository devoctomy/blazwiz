using Amazon;
using Amazon.Runtime;
using Amazon.SecretsManager;
using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Models.Aws;

namespace blazwiz.Portal.Application.Services;

/// <summary>
/// <see cref="ISecretsManagerClientFactory"/> creating clients that work in the browser, in the stack's region.
/// </summary>
public sealed class SecretsManagerClientFactory(AwsCognitoConfig cognitoConfig, BrowserHttpClientFactory httpClientFactory)
    : ISecretsManagerClientFactory
{
    private readonly AwsCognitoConfig cognitoConfig = cognitoConfig ?? throw new ArgumentNullException(nameof(cognitoConfig));
    private readonly BrowserHttpClientFactory httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));

    /// <inheritdoc/>
    public IAmazonSecretsManager Create(AwsTemporaryCredentials credentials)
    {
        ArgumentNullException.ThrowIfNull(credentials);

        return new AmazonSecretsManagerClient(
            new SessionAWSCredentials(credentials.AccessKeyId, credentials.SecretKey, credentials.SessionToken),
            new AmazonSecretsManagerConfig
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(cognitoConfig.Region),

                // The SDK reads responses synchronously, which the browser only allows from a buffer.
                HttpClientFactory = httpClientFactory,
            });
    }
}
