using Devoctomy.Blazwiz.Auth.Exceptions;
using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.Core.Exceptions;

namespace Devoctomy.Blazwiz.Auth.Services;

/// <summary>
/// Placeholder password changes and resets, used with <see cref="LocalAuthenticationService"/> (no identity provider,
/// e.g. local development): checks what was filled in, then accepts anything, including any verification code.
/// </summary>
public class LocalPasswordService(IAuthenticationService authenticationService)
    : IPasswordService
{
    private readonly IAuthenticationService authenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));

    /// <inheritdoc/>
    public async Task ChangePasswordAsync(string currentPassword, string newPassword)
    {
        if (string.IsNullOrEmpty(currentPassword) || string.IsNullOrEmpty(newPassword))
        {
            throw new BlazwizException(AuthErrorCodes.PasswordsRequired);
        }

        if (await authenticationService.GetCurrentUserAsync() == null)
        {
            throw new BlazwizException(AuthErrorCodes.NotSignedIn);
        }
    }

    /// <inheritdoc/>
    public Task RequestPasswordResetAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new BlazwizException(AuthErrorCodes.EmailRequired);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ResetPasswordAsync(string email, string code, string newPassword)
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

        return Task.CompletedTask;
    }
}
