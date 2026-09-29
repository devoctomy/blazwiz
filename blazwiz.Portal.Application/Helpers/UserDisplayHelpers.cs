namespace blazwiz.Portal.Application.Helpers;

/// <summary>
/// Helpers for presenting user details.
/// </summary>
public static class UserDisplayHelpers
{
    private static readonly char[] NameSeparators = [' ', '.', '_', '-'];

    /// <summary>
    /// Gets up to two initials for the given display name, e.g. "Bob Hoskins" becomes "BH".
    /// </summary>
    /// <param name="displayName">The display name.</param>
    /// <returns>The upper case initials, or "?" when no name is available.</returns>
    public static string GetInitials(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return "?";
        }

        var parts = displayName.Split(NameSeparators, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return "?";
        }

        var initials = parts.Length == 1
            ? parts[0][..1]
            : $"{parts[0][0]}{parts[^1][0]}";

        return initials.ToUpperInvariant();
    }

    /// <summary>
    /// Derives a friendly display name from a username or email address, e.g. "bob.hoskins@example.com" becomes "Bob Hoskins".
    /// </summary>
    /// <param name="username">The username or email address.</param>
    /// <returns>The display name.</returns>
    public static string GetDisplayNameFromUsername(string username)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        var localPart = username.Trim().Split('@')[0];
        var words = localPart
            .Split(NameSeparators, StringSplitOptions.RemoveEmptyEntries)
            .Select(w => char.ToUpperInvariant(w[0]) + w[1..]);

        var displayName = string.Join(' ', words);
        return string.IsNullOrEmpty(displayName) ? username.Trim() : displayName;
    }
}
