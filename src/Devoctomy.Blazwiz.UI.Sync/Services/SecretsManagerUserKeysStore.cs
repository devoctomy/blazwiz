using Amazon.Runtime;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Devoctomy.Blazwiz.Auth.Cognito.Services.Interfaces;
using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.Core.Offline.Interfaces;
using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.UI.Sync.Services.Interfaces;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Devoctomy.Blazwiz.UI.Sync.Services;

/// <summary>
/// <see cref="IUserKeysStore"/> and <see cref="IE2eProfileIndexStore"/> calling Secrets Manager from the browser with
/// the session's identity pool credentials. Each secret is named after the user pool sub, not the identity id, because
/// identity ids contain ':', which secret names cannot: <c>&lt;sub&gt;/keys/&lt;scope&gt;</c> for each profile's keys,
/// and <c>&lt;sub&gt;/keys/index</c> for the index of the user's profiles. The stack only lets each user reach the secrets matching their own sub (cloudformation/stack.yaml).
/// </summary>
public sealed class SecretsManagerUserKeysStore(
    IAuthenticationService authService,
    IAwsCredentialsProvider credentialsProvider,
    ISecretsManagerClientFactory clientFactory,
    IOfflineModeService offlineModeService)
    : IUserKeysStore, IE2eProfileIndexStore
{
    private const string IndexSecretSuffix = "keys/index";

    private readonly IAuthenticationService authService = authService ?? throw new ArgumentNullException(nameof(authService));
    private readonly IAwsCredentialsProvider credentialsProvider = credentialsProvider ?? throw new ArgumentNullException(nameof(credentialsProvider));
    private readonly ISecretsManagerClientFactory clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
    private readonly IOfflineModeService offlineModeService = offlineModeService ?? throw new ArgumentNullException(nameof(offlineModeService));

    /// <inheritdoc/>
    public Task<string?> GetAsync(E2eKeyScope scope, CancellationToken ct = default) =>
        GetSecretAsync(KeysSecretSuffix(scope), ct);

    /// <inheritdoc/>
    public Task SetAsync(E2eKeyScope scope, string value, CancellationToken ct = default) =>
        SetSecretAsync(KeysSecretSuffix(scope), value, ct);

    /// <inheritdoc/>
    public Task<string?> GetIndexAsync(CancellationToken ct = default) =>
        GetSecretAsync(IndexSecretSuffix, ct);

    /// <inheritdoc/>
    public Task SetIndexAsync(string value, CancellationToken ct = default) =>
        SetSecretAsync(IndexSecretSuffix, value, ct);

    /// <inheritdoc/>
    public Task<bool> TryCreateAsync(E2eKeyScope scope, string value, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(value);

        return WithClientAsync(KeysSecretSuffix(scope), ct, async (client, secretName) =>
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
    public Task DeleteAsync(E2eKeyScope scope, CancellationToken ct = default) =>
        WithClientAsync<object?>(KeysSecretSuffix(scope), ct, async (client, secretName) =>
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

    private static string KeysSecretSuffix(E2eKeyScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        return $"keys/{scope.Id}";
    }

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

    private Task<string?> GetSecretAsync(string suffix, CancellationToken ct) =>
        WithClientAsync(suffix, ct, async (client, secretName) =>
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

    private Task SetSecretAsync(string suffix, string value, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(value);

        return WithClientAsync<object?>(suffix, ct, async (client, secretName) =>
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

    private async Task<T> WithClientAsync<T>(string suffix, CancellationToken ct, Func<IAmazonSecretsManager, string, Task<T>> action)
    {
        if (!authService.IsPlaceholder && offlineModeService.IsOffline)
        {
            // Not "cannot be used in this session" (InvalidOperationException), which would turn encryption off
            // here: offline, the keys are just out of reach, so this device's own copy is used.
            throw new HttpRequestException("Offline mode: the user's keys cannot be fetched.");
        }

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
            return await action(client, $"{sub}/{suffix}");
        }
        catch (AmazonServiceException ex) when (ex.InnerException is HttpRequestException)
        {
            throw new HttpRequestException(ex.Message, ex);
        }
    }
}
