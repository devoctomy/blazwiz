using Amazon.S3;

namespace blazwiz.Core.Sync.Models;

/// <summary>
/// Where the signed in user's files are kept in S3.
/// </summary>
/// <param name="client">An S3 client with the user's credentials.</param>
/// <param name="bucket">The data bucket.</param>
/// <param name="rootPrefix">The user's root, e.g. <c>&lt;identity id&gt;/</c>. Ends with <c>/</c>.</param>
public class S3SyncTarget(IAmazonS3 client, string bucket, string rootPrefix)
{
    /// <summary>
    /// Gets an S3 client with the user's credentials.
    /// </summary>
    public IAmazonS3 Client { get; } = client ?? throw new ArgumentNullException(nameof(client));

    /// <summary>
    /// Gets the data bucket.
    /// </summary>
    public string Bucket { get; } = bucket ?? throw new ArgumentNullException(nameof(bucket));

    /// <summary>
    /// Gets the user's root prefix, ending with <c>/</c>.
    /// </summary>
    public string RootPrefix { get; } = rootPrefix ?? throw new ArgumentNullException(nameof(rootPrefix));
}
