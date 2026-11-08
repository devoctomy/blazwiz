using Devoctomy.Blazwiz.Sync.Interfaces;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Devoctomy.Blazwiz.Sync;

/// <summary>
/// Matches keys against a glob using <see cref="Matcher"/> (Microsoft.Extensions.FileSystemGlobbing). Case sensitive,
/// like S3 keys.
/// </summary>
public class GlobSyncPathMatcher : ISyncPathMatcher
{
    private static readonly char[] WildcardCharacters = ['*', '?', '[', '{'];

    private readonly Matcher matcher = new Matcher(StringComparison.Ordinal);
    private readonly bool includeEmptyFolders;

    /// <summary>
    /// Initializes a new instance of the <see cref="GlobSyncPathMatcher"/> class.
    /// </summary>
    /// <param name="pattern">The glob, relative to the user's root, e.g. <c>documents/**/*.md</c>.</param>
    /// <param name="includeEmptyFolders">True to match folder markers under the pattern's prefix.</param>
    public GlobSyncPathMatcher(string pattern, bool includeEmptyFolders)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);

        var trimmed = pattern.TrimStart('/');
        matcher.AddInclude(trimmed);
        this.includeEmptyFolders = includeEmptyFolders;

        var wildcard = trimmed.IndexOfAny(WildcardCharacters);
        var literal = wildcard < 0 ? trimmed : trimmed[..wildcard];
        var prefixLength = literal.LastIndexOf('/') + 1;
        Prefix = literal[..prefixLength];
    }

    /// <inheritdoc/>
    public string Prefix { get; }

    /// <inheritdoc/>
    public bool IsMatch(string key)
    {
        if (string.IsNullOrEmpty(key) || key.StartsWith('/') || key.Contains('\\') || !HasPlainSegments(key))
        {
            return false;
        }

        if (key.EndsWith('/'))
        {
            return includeEmptyFolders && key.Length > Prefix.Length && key.StartsWith(Prefix, StringComparison.Ordinal);
        }

        return matcher.Match(key).HasMatches;
    }

    // The glob library normalises "." and ".." segments and ignores empty ones, while the file stores take keys as they
    // are, so two keys could name one path. Such keys are never written by the app, so they are not matched at all.
    private static bool HasPlainSegments(string key)
    {
        var path = key.EndsWith('/') ? key[..^1] : key;
        foreach (var segment in path.Split('/'))
        {
            if (segment.Length == 0 || segment == "." || segment == "..")
            {
                return false;
            }
        }

        return true;
    }
}
