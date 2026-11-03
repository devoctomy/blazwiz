namespace Devoctomy.Blazwiz.UI.Sync.Models;

/// <summary>
/// AWS S3 configuration, bound from the <c>AwsS3</c> section of <c>wwwroot/appsettings.json</c>.
/// </summary>
public class AwsS3Config
{
    /// <summary>
    /// Gets or sets the AWS region the data bucket lives in.
    /// </summary>
    public string Region { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the per-user data bucket. Signed-in users may only access objects
    /// under their own Cognito identity id prefix.
    /// </summary>
    public string DataBucketName { get; set; } = string.Empty;
}
