namespace Devoctomy.Blazwiz.UI.Models;

/// <summary>
/// The steps of resetting a forgotten password.
/// </summary>
public enum ForgotPasswordStep
{
    /// <summary>
    /// Asking for the email address to send a code to.
    /// </summary>
    Request,

    /// <summary>
    /// Asking for the code and the new password.
    /// </summary>
    Reset,

    /// <summary>
    /// The password has been reset.
    /// </summary>
    Done,
}
