namespace Devoctomy.Blazwiz.Auth.Cognito.Models;

/// <summary>
/// The outcome of a Cognito sign-in step: either the tokens, or the challenge the user must answer next.
/// </summary>
public class CognitoSignInResult
{
    /// <summary>
    /// Gets or sets the ID token (a JWT with the user's claims, e.g. email), once signed in.
    /// </summary>
    public string? IdToken { get; set; }

    /// <summary>
    /// Gets or sets the access token, once signed in.
    /// </summary>
    public string? AccessToken { get; set; }

    /// <summary>
    /// Gets or sets the refresh token. Not returned when refreshing, the original one stays valid.
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Gets or sets how many seconds the ID and access tokens are valid for.
    /// </summary>
    public int ExpiresIn { get; set; }

    /// <summary>
    /// Gets or sets the challenge the user must answer next, e.g. <c>NEW_PASSWORD_REQUIRED</c>, when not signed in yet.
    /// </summary>
    public string? ChallengeName { get; set; }

    /// <summary>
    /// Gets or sets the session to pass back when answering the challenge.
    /// </summary>
    public string? SessionId { get; set; }
}
