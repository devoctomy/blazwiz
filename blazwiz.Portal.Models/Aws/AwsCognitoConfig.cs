namespace blazwiz.Portal.Models.Aws;

/// <summary>
/// AWS Cognito configuration, bound from the <c>AwsCognito</c> section of <c>wwwroot/appsettings.json</c>.
/// </summary>
public class AwsCognitoConfig
{
    /// <summary>
    /// Gets or sets the AWS region the Cognito resources live in, e.g. <c>eu-west-2</c>.
    /// </summary>
    public string Region { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Cognito user pool identifier.
    /// </summary>
    public string UserPoolId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Cognito user pool app client identifier.
    /// </summary>
    public string UserPoolClientId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Cognito identity pool identifier, used to exchange tokens for temporary AWS credentials.
    /// </summary>
    public string IdentityPoolId { get; set; } = string.Empty;
}
