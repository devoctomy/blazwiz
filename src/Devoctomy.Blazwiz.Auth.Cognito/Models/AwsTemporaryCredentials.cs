namespace Devoctomy.Blazwiz.Auth.Cognito.Models;

/// <summary>
/// Temporary AWS credentials for the signed in user, from the Cognito identity pool.
/// </summary>
public class AwsTemporaryCredentials
{
    /// <summary>
    /// Gets or sets the user's identity id, which names their prefix in the data bucket.
    /// </summary>
    public string IdentityId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the access key id.
    /// </summary>
    public string AccessKeyId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the secret access key.
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the session token.
    /// </summary>
    public string SessionToken { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the credentials expire.
    /// </summary>
    public DateTimeOffset Expiration { get; set; }
}
