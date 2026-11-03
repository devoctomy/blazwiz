using Devoctomy.Blazwiz.Auth.Cognito.Exceptions;
using Devoctomy.Blazwiz.Auth.Cognito.Services.Interfaces;
using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Auth.Sessions.Interfaces;

namespace Devoctomy.Blazwiz.Auth.Cognito.Services;

/// <summary>
/// Fetches the session's AWS credentials once signed in, in the background, and forgets them on sign out.
/// </summary>
public class AwsCredentialsSessionHandler(IAwsCredentialsProvider credentialsProvider)
    : ISessionLifecycleHandler
{
    private readonly IAwsCredentialsProvider credentialsProvider = credentialsProvider ?? throw new ArgumentNullException(nameof(credentialsProvider));

    /// <inheritdoc/>
    public Task OnSessionStartedAsync(UserSession session)
    {
        // Not awaited: whatever needs AWS asks again when it is used, so nothing waits for this.
        _ = LoadAsync();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task OnSignedOutAsync()
    {
        credentialsProvider.Clear();
        return Task.CompletedTask;
    }

    private async Task LoadAsync()
    {
        try
        {
            await credentialsProvider.GetCredentialsAsync();
        }
        catch (Exception ex) when (ex is CognitoException or HttpRequestException)
        {
            // Offline or refused: whatever needs AWS asks again when it is used.
        }
    }
}
