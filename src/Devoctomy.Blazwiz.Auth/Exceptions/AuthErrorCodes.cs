namespace Devoctomy.Blazwiz.Auth.Exceptions;

/// <summary>
/// The codes of sign-in errors (see <c>LoginResult.ErrorCode</c>). The UI shows each with the text resource
/// <c>Error.&lt;code&gt;</c>.
/// </summary>
public static class AuthErrorCodes
{
    /// <summary>
    /// The email or password was left empty.
    /// </summary>
    public const string CredentialsRequired = "Auth.CredentialsRequired";

    /// <summary>
    /// The email or password is not correct, or there were too many failed attempts.
    /// </summary>
    public const string NotAuthorized = "Auth.NotAuthorized";

    /// <summary>
    /// The sign-in service could not be reached.
    /// </summary>
    public const string Unreachable = "Auth.Unreachable";

    /// <summary>
    /// The password must be reset by an administrator.
    /// </summary>
    public const string PasswordResetRequired = "Auth.PasswordResetRequired";

    /// <summary>
    /// The account has not been confirmed.
    /// </summary>
    public const string UserNotConfirmed = "Auth.UserNotConfirmed";

    /// <summary>
    /// Too many attempts in a short time.
    /// </summary>
    public const string TooManyAttempts = "Auth.TooManyAttempts";

    /// <summary>
    /// The sign-in service refused the sign in for another reason.
    /// </summary>
    public const string SignInFailed = "Auth.SignInFailed";

    /// <summary>
    /// Signing in needs a step the app does not support. Argument: the step's name.
    /// </summary>
    public const string ChallengeUnsupported = "Auth.ChallengeUnsupported";

    /// <summary>
    /// Something needs a signed-in user, and nobody is signed in.
    /// </summary>
    public const string NotSignedIn = "Auth.NotSignedIn";

    /// <summary>
    /// The sign in that asked for a new password is no longer going on; sign in again.
    /// </summary>
    public const string SignInAgain = "Auth.SignInAgain";

    /// <summary>
    /// The sign in that asked for a new password has expired.
    /// </summary>
    public const string SignInExpired = "Auth.SignInExpired";

    /// <summary>
    /// The new password was left empty.
    /// </summary>
    public const string NewPasswordRequired = "Auth.NewPasswordRequired";

    /// <summary>
    /// The new password does not meet the password rules.
    /// </summary>
    public const string InvalidPassword = "Auth.InvalidPassword";

    /// <summary>
    /// A password field was left empty.
    /// </summary>
    public const string PasswordsRequired = "Auth.PasswordsRequired";

    /// <summary>
    /// The new password and its confirmation are different.
    /// </summary>
    public const string PasswordsDoNotMatch = "Auth.PasswordsDoNotMatch";

    /// <summary>
    /// The email address was left empty.
    /// </summary>
    public const string EmailRequired = "Auth.EmailRequired";

    /// <summary>
    /// The verification code was left empty.
    /// </summary>
    public const string CodeRequired = "Auth.CodeRequired";

    /// <summary>
    /// The current password given to change it is not correct.
    /// </summary>
    public const string CurrentPasswordWrong = "Auth.CurrentPasswordWrong";

    /// <summary>
    /// The verification code is not correct.
    /// </summary>
    public const string CodeMismatch = "Auth.CodeMismatch";

    /// <summary>
    /// The verification code has expired.
    /// </summary>
    public const string CodeExpired = "Auth.CodeExpired";

    /// <summary>
    /// The password cannot be changed or reset in offline mode.
    /// </summary>
    public const string PasswordChangeOffline = "Auth.PasswordChangeOffline";

    /// <summary>
    /// The sign-in service refused to change the password for another reason.
    /// </summary>
    public const string PasswordChangeFailed = "Auth.PasswordChangeFailed";

    /// <summary>
    /// The sign-in service refused to reset the password for another reason.
    /// </summary>
    public const string PasswordResetFailed = "Auth.PasswordResetFailed";
}
