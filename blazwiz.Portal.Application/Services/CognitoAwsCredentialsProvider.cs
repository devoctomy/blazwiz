using blazwiz.Core.Offline.Interfaces;
using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Models.Aws;

namespace blazwiz.Portal.Application.Services;

/// <summary>
/// <see cref="IAwsCredentialsProvider"/> holding the identity pool credentials in memory per user, until they are
/// close to expiry or cleared. Concurrent callers share a single fetch. None are fetched, or handed out, in offline mode.
/// </summary>
public sealed class CognitoAwsCredentialsProvider(
    IAuthenticationService authService,
    ICognitoIdentityClient identityClient,
    AwsCognitoConfig cognitoConfig,
    IOfflineModeService offlineModeService,
    TimeProvider timeProvider)
    : IAwsCredentialsProvider, IDisposable
{
    /// <summary>
    /// How long before the credentials expire new ones are fetched.
    /// </summary>
    public static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(5);

    private readonly IAuthenticationService authService = authService ?? throw new ArgumentNullException(nameof(authService));
    private readonly ICognitoIdentityClient identityClient = identityClient ?? throw new ArgumentNullException(nameof(identityClient));
    private readonly AwsCognitoConfig cognitoConfig = cognitoConfig ?? throw new ArgumentNullException(nameof(cognitoConfig));
    private readonly IOfflineModeService offlineModeService = offlineModeService ?? throw new ArgumentNullException(nameof(offlineModeService));
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);

    private CachedCredentials? cached;

    /// <summary>
    /// Gets a value indicating whether an identity pool is configured.
    /// </summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(cognitoConfig.Region)
        && !string.IsNullOrWhiteSpace(cognitoConfig.UserPoolId)
        && !string.IsNullOrWhiteSpace(cognitoConfig.IdentityPoolId);

    /// <inheritdoc/>
    public async Task<AwsTemporaryCredentials?> GetCredentialsAsync(CancellationToken ct = default)
    {
        if (authService.IsPlaceholder || !IsConfigured)
        {
            return null;
        }

        if (offlineModeService.IsOffline)
        {
            // Offline mode makes no AWS calls, and credentials fetched before it must not be used during it.
            Clear();
            return null;
        }

        var session = await authService.GetCurrentUserAsync();
        if (string.IsNullOrEmpty(session?.IdToken))
        {
            // Signed out or the session ended: nothing should be held.
            Clear();
            return null;
        }

        await gate.WaitAsync(ct);
        try
        {
            if (cached != null && cached.Username == session.Username && cached.Credentials.Expiration - RefreshMargin > timeProvider.GetUtcNow())
            {
                return cached.Credentials;
            }

            cached = null;
            var credentials = await identityClient.GetCredentialsAsync(session.IdToken, ct);
            cached = new CachedCredentials(session.Username, credentials);
            return credentials;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <inheritdoc/>
    public void Clear() => cached = null;

    /// <inheritdoc/>
    public void Dispose() => gate.Dispose();

    private sealed class CachedCredentials(string username, AwsTemporaryCredentials credentials)
    {
        public string Username { get; } = username;

        public AwsTemporaryCredentials Credentials { get; } = credentials;
    }
}
