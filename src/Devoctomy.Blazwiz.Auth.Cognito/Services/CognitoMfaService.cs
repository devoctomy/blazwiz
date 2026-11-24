using Devoctomy.Blazwiz.Auth.Cognito.Exceptions;
using Devoctomy.Blazwiz.Auth.Cognito.Services.Interfaces;
using Devoctomy.Blazwiz.Auth.Exceptions;
using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.Core.Exceptions;
using Devoctomy.Blazwiz.Core.Offline.Interfaces;

namespace Devoctomy.Blazwiz.Auth.Cognito.Services;

/// <summary>
/// Two-step verification with an authenticator app (Cognito's software token MFA) for the signed-in user. Offline mode
/// makes no network calls, so every change needs it off.
/// </summary>
public class CognitoMfaService(
    ICognitoUserPoolClient userPoolClient,
    IAuthenticationService authenticationService,
    IOfflineModeService offlineModeService)
    : IMfaService
{
    private readonly ICognitoUserPoolClient userPoolClient = userPoolClient ?? throw new ArgumentNullException(nameof(userPoolClient));
    private readonly IAuthenticationService authenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));
    private readonly IOfflineModeService offlineModeService = offlineModeService ?? throw new ArgumentNullException(nameof(offlineModeService));

    /// <inheritdoc/>
    public async Task<bool> IsEnabledAsync()
    {
        var accessToken = await GetAccessTokenAsync();
        return await CallAsync(() => userPoolClient.IsSoftwareTokenMfaEnabledAsync(accessToken));
    }

    /// <inheritdoc/>
    public async Task<string> BeginSetupAsync()
    {
        var accessToken = await GetAccessTokenAsync();
        return await CallAsync(() => userPoolClient.AssociateSoftwareTokenAsync(accessToken));
    }

    /// <inheritdoc/>
    public async Task CompleteSetupAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new BlazwizException(AuthErrorCodes.MfaCodeRequired);
        }

        var accessToken = await GetAccessTokenAsync();
        await CallAsync(async () =>
        {
            await userPoolClient.VerifySoftwareTokenAsync(accessToken, code.Trim());
            await userPoolClient.SetSoftwareTokenMfaAsync(accessToken, true);
            return true;
        });
    }

    /// <inheritdoc/>
    public async Task DisableAsync()
    {
        var accessToken = await GetAccessTokenAsync();
        await CallAsync(async () =>
        {
            await userPoolClient.SetSoftwareTokenMfaAsync(accessToken, false);
            return true;
        });
    }

    private static async Task<T> CallAsync<T>(Func<Task<T>> call)
    {
        try
        {
            return await call();
        }
        catch (CognitoException ex)
        {
            var code = ex.ErrorType switch
            {
                // A wrong first code is reported by VerifySoftwareToken as either of these.
                "CodeMismatchException" or "EnableSoftwareTokenMFAException" => AuthErrorCodes.CodeMismatch,
                "TooManyRequestsException" or "LimitExceededException" => AuthErrorCodes.TooManyAttempts,
                "NotAuthorizedException" => AuthErrorCodes.SignInAgain,
                _ => AuthErrorCodes.MfaChangeFailed,
            };
            throw new BlazwizException(code, ex);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new BlazwizException(AuthErrorCodes.Unreachable, ex);
        }
    }

    private async Task<string> GetAccessTokenAsync()
    {
        if (offlineModeService.IsOffline)
        {
            throw new BlazwizException(AuthErrorCodes.AccountChangeOffline);
        }

        // Reading the session renews its tokens when they are about to expire.
        var session = await authenticationService.GetCurrentUserAsync();
        if (session == null || session.IsOffline || string.IsNullOrEmpty(session.AccessToken))
        {
            throw new BlazwizException(AuthErrorCodes.NotSignedIn);
        }

        return session.AccessToken;
    }
}
