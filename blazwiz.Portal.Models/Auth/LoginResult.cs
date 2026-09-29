namespace blazwiz.Portal.Models.Auth;

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
    /// Gets or sets the error message when the login failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Gets or sets the session created by a successful login.
    /// </summary>
    public UserSession? Session { get; set; }
}
