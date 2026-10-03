using Amazon.Runtime;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using blazwiz.Core.Cryptography.Interfaces;
using blazwiz.Portal.Application.Services.Interfaces;
using Microsoft.IdentityModel.JsonWebTokens;

namespace blazwiz.Portal.Application.Services;

/// <summary>
/// <see cref="IUserKeysStore"/> calling Secrets Manager from the browser with the session's identity pool credentials. The secret
/// is named after the user pool sub, not the identity id, because identity ids contain ':', which secret names cannot;
/// the stack only lets each user reach the secret matching their own sub (cloudformation/stack.yaml).
/// </summary>
public sealed class SecretsManagerUserKeysStore(
    IAuthenticationService authService,
    IAwsCredentialsProvider credentialsProvider,
    ISecretsManagerClientFactory clientFactory)
    : IUserKeysStore
{
    private readonly IAuthenticationService authService = authService ?? throw new ArgumentNullException(nameof(authService));
    private readonly IAwsCredentialsProvider credentialsProvider = credentialsProvider ?? throw new ArgumentNullException(nameof(credentialsProvider));
    private readonly ISecretsManagerClientFactory clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));

    /// <inheritdoc/>
    public Task<string?> GetAsync(CancellationToken ct = default) =>
        WithClientAsync(ct, async (client, secretName) =>
        {
            try
            {
                var response = await client.GetSecretValueAsync(new GetSecretValueRequest { SecretId = secretName }, ct);
                return response.SecretString;
            }
            catch (ResourceNotFoundException)
            {
                return null;
            }
        });

    /// <inheritdoc/>
    public Task SetAsync(string value, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(value);

        return WithClientAsync<object?>(ct, async (client, secretName) =>
        {
            try
            {
                await client.PutSecretValueAsync(new PutSecretValueRequest { SecretId = secretName, SecretString = value }, ct);
            }
            catch (ResourceNotFoundException)
            {
                try
                {
                    await client.CreateSecretAsync(new CreateSecretRequest { Name = secretName, SecretString = value }, ct);
                }
                catch (ResourceExistsException)
                {
                    // Created in between, e.g. from another tab.
                    await client.PutSecretValueAsync(new PutSecretValueRequest { SecretId = secretName, SecretString = value }, ct);
                }
            }

            return null;
        });
    }

    /// <inheritdoc/>
    public Task<bool> TryCreateAsync(string value, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(value);

        return WithClientAsync(ct, async (client, secretName) =>
        {
            try
            {
                await client.CreateSecretAsync(new CreateSecretRequest { Name = secretName, SecretString = value }, ct);
                return true;
            }
            catch (ResourceExistsException)
            {
                return false;
            }
        });
    }

    /// <inheritdoc/>
    public Task DeleteAsync(CancellationToken ct = default) =>
        WithClientAsync<object?>(ct, async (client, secretName) =>
        {
            try
            {
                await client.DeleteSecretAsync(new DeleteSecretRequest { SecretId = secretName, ForceDeleteWithoutRecovery = true }, ct);
            }
            catch (ResourceNotFoundException)
            {
                // Nothing to delete.
            }

            return null;
        });

    private static string? GetSub(string? idToken)
    {
        if (string.IsNullOrEmpty(idToken))
        {
            return null;
        }

        try
        {
            // The token came straight from Cognito, and AWS validates it when granting the credentials.
            return new JsonWebToken(idToken).TryGetPayloadValue<string>("sub", out var sub) && !string.IsNullOrEmpty(sub) ? sub : null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private async Task<T> WithClientAsync<T>(CancellationToken ct, Func<IAmazonSecretsManager, string, Task<T>> action)
    {
        var session = authService.IsPlaceholder ? null : await authService.GetCurrentUserAsync();
        var sub = GetSub(session?.IdToken);
        var credentials = sub == null ? null : await credentialsProvider.GetCredentialsAsync(ct);
        if (credentials == null)
        {
            throw new InvalidOperationException("The user's keys are only available when signed in with Cognito.");
        }

        using var client = clientFactory.Create(credentials);
        try
        {
            return await action(client, $"{sub}/keys");
        }
        catch (AmazonServiceException ex) when (ex.InnerException is HttpRequestException)
        {
            throw new HttpRequestException(ex.Message, ex);
        }
    }
}
