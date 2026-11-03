using Devoctomy.Blazwiz.Auth.Cognito.Exceptions;
using Devoctomy.Blazwiz.Auth.Cognito.Services.Interfaces;
using Devoctomy.Blazwiz.Auth.Exceptions;
using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.Core.Exceptions;
using Devoctomy.Blazwiz.Core.Offline.Interfaces;

namespace Devoctomy.Blazwiz.Auth.Cognito.Services;

/// <summary>
/// Changes and resets passwords in the Cognito user pool. Offline mode makes no network calls, so both need it off.
/// </summary>
public class CognitoPasswordService(
    ICognitoUserPoolClient userPoolClient,
    IAuthenticationService authenticationService,
    IOfflineModeService offlineModeService)
    : IPasswordService
{
    private readonly ICognitoUserPoolClient userPoolClient = userPoolClient ?? throw new ArgumentNullException(nameof(userPoolClient));
    private readonly IAuthenticationService authenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));
    private readonly IOfflineModeService offlineModeService = offlineModeService ?? throw new ArgumentNullException(nameof(offlineModeService));

    /// <inheritdoc/>
    public async Task ChangePasswordAsync(string currentPassword, string newPassword)
    {
        if (string.IsNullOrEmpty(currentPassword) || string.IsNullOrEmpty(newPassword))
        {
            throw new BlazwizException(AuthErrorCodes.PasswordsRequired);
        }

        EnsureOnline();

        // Reading the session renews its tokens when they are about to expire.
        var session = await authenticationService.GetCurrentUserAsync();
        if (session == null || session.IsOffline || string.IsNullOrEmpty(session.IdToken) || string.IsNullOrEmpty(session.AccessToken))
        {
            throw new BlazwizException(AuthErrorCodes.NotSignedIn);
        }

        await CallAsync(
            () => userPoolClient.ChangePasswordAsync(session.Username, session.IdToken, session.AccessToken, session.ExpiresAt, currentPassword, newPassword),
            AuthErrorCodes.PasswordChangeFailed,
            ex => ex.ErrorType == "NotAuthorizedException" ? AuthErrorCodes.CurrentPasswordWrong : null);
    }

    /// <inheritdoc/>
    public async Task RequestPasswordResetAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new BlazwizException(AuthErrorCodes.EmailRequired);
        }

        EnsureOnline();
        try
        {
            await CallAsync(() => userPoolClient.ForgotPasswordAsync(email.Trim()), AuthErrorCodes.PasswordResetFailed, _ => null);
        }
        catch (BlazwizException ex) when (ex.InnerException is CognitoException { ErrorType: "UserNotFoundException" })
        {
            // Say nothing: whether an address has an account is not for anyone asking to know.
        }
    }

    /// <inheritdoc/>
    public async Task ResetPasswordAsync(string email, string code, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new BlazwizException(AuthErrorCodes.EmailRequired);
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new BlazwizException(AuthErrorCodes.CodeRequired);
        }

        if (string.IsNullOrEmpty(newPassword))
        {
            throw new BlazwizException(AuthErrorCodes.PasswordsRequired);
        }

        EnsureOnline();

        // A wrong address is reported as a wrong code, for the same reason as above.
        await CallAsync(
            () => userPoolClient.ConfirmForgotPasswordAsync(email.Trim(), code.Trim(), newPassword),
            AuthErrorCodes.PasswordResetFailed,
            ex => ex.ErrorType is "UserNotFoundException" ? AuthErrorCodes.CodeMismatch : null);
    }

    private static async Task CallAsync(Func<Task> call, string failedCode, Func<CognitoException, string?> specificCode)
    {
        try
        {
            await call();
        }
        catch (CognitoException ex)
        {
            var code = specificCode(ex) ?? ex.ErrorType switch
            {
                "InvalidPasswordException" => AuthErrorCodes.InvalidPassword,
                "CodeMismatchException" => AuthErrorCodes.CodeMismatch,
                "ExpiredCodeException" => AuthErrorCodes.CodeExpired,
                "TooManyRequestsException" or "LimitExceededException" or "TooManyFailedAttemptsException" => AuthErrorCodes.TooManyAttempts,
                _ => failedCode,
            };
            throw new BlazwizException(code, ex);
        }
        catch (HttpRequestException ex)
        {
            throw new BlazwizException(AuthErrorCodes.Unreachable, ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new BlazwizException(AuthErrorCodes.Unreachable, ex);
        }
    }

    private void EnsureOnline()
    {
        if (offlineModeService.IsOffline)
        {
            throw new BlazwizException(AuthErrorCodes.PasswordChangeOffline);
        }
    }
}
