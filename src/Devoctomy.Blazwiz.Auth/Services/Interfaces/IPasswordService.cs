namespace Devoctomy.Blazwiz.Auth.Services.Interfaces;

/// <summary>
/// Changes and resets passwords with the identity provider. Failures throw a
/// <see cref="Core.Exceptions.BlazwizException"/> with one of <see cref="Exceptions.AuthErrorCodes"/>.
/// </summary>
public interface IPasswordService
{
    /// <summary>
    /// Changes the signed-in user's password.
    /// </summary>
    /// <param name="currentPassword">The user's current password.</param>
    /// <param name="newPassword">The new password.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task ChangePasswordAsync(string currentPassword, string newPassword);

    /// <summary>
    /// Sends a verification code for resetting the password of the account with this email address. Succeeds whether
    /// or not there is such an account, so nobody can find out which addresses have one.
    /// </summary>
    /// <param name="email">The account's email address.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task RequestPasswordResetAsync(string email);

    /// <summary>
    /// Sets a new password with the verification code sent by <see cref="RequestPasswordResetAsync"/>.
    /// </summary>
    /// <param name="email">The account's email address.</param>
    /// <param name="code">The verification code.</param>
    /// <param name="newPassword">The new password.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task ResetPasswordAsync(string email, string code, string newPassword);
}
