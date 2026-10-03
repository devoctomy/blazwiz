using Amazon.SecretsManager;
using blazwiz.Portal.Models.Aws;

namespace blazwiz.Portal.Application.Services.Interfaces;

/// <summary>
/// Creates Secrets Manager clients for a user's temporary credentials, so services using them can be unit tested
/// without AWS.
/// </summary>
public interface ISecretsManagerClientFactory
{
    /// <summary>
    /// Creates a client signed with the given credentials. The caller disposes it.
    /// </summary>
    /// <param name="credentials">The user's temporary credentials from the identity pool.</param>
    /// <returns>The client.</returns>
    IAmazonSecretsManager Create(AwsTemporaryCredentials credentials);
}
