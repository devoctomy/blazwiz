namespace Devoctomy.Blazwiz.Auth.Models;

/// <summary>
/// The result of a login attempt.
/// </summary>
public class LoginResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the login succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets why the sign in failed, as an error code (see <c>AuthErrorCodes</c>), or null if it did not. The
    /// UI shows it with the text resource <c>Error.&lt;code&gt;</c>.
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Gets or sets the values the error's text needs.
    /// </summary>
    public IReadOnlyList<object> ErrorArguments { get; set; } = [];

    /// <summary>
    /// Gets or sets the session created by a successful login.
    /// </summary>
    public UserSession? Session { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user must choose a new password before they can sign in,
    /// e.g. when they were invited with a temporary password.
    /// </summary>
    public bool NewPasswordRequired { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user must enter the code from their authenticator app to finish
    /// signing in (see <c>IAuthenticationService.CompleteMfaAsync</c>).
    /// </summary>
    public bool MfaCodeRequired { get; set; }

    /// <summary>
    /// Gets or sets the secret for the authenticator app, when the user must set one up to finish signing in (the app
    /// requires two-step verification and they have none yet). Show it, then pass the app's first code to
    /// <c>IAuthenticationService.CompleteMfaSetupAsync</c>. Null when no set-up is needed.
    /// </summary>
    public string? MfaSetupSecret { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the remembered device given to the sign in is no longer known (it was
    /// forgotten, e.g. from another device), so whatever stored it should drop it. The sign in went ahead without it.
    /// </summary>
    public bool RememberedDeviceRejected { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the sign-in service could not be reached (no network, or no answer).
    /// Not set when it answered and refused the sign in.
    /// </summary>
    public bool ServiceUnreachable { get; set; }
}
