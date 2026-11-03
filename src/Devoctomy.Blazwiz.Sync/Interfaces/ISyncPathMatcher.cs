namespace Devoctomy.Blazwiz.Sync.Interfaces;

/// <summary>
/// Matches keys against a profile's remote pattern, e.g. <c>documents/**/*.md</c>.
/// </summary>
public interface ISyncPathMatcher
{
    /// <summary>
    /// Gets the literal part of the pattern before the first wildcard, used as the S3 listing prefix. Empty when the
    /// pattern starts with a wildcard.
    /// </summary>
    string Prefix { get; }

    /// <summary>
    /// Checks whether a key belongs to the profile. <c>*</c> does not cross <c>/</c>, <c>**</c> does. Folder markers
    /// (keys ending with <c>/</c>) match when empty folders are included and they are under <see cref="Prefix"/>.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>True if the key matches.</returns>
    bool IsMatch(string key);
}
