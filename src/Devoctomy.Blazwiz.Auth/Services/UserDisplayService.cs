using Devoctomy.Blazwiz.Auth.Services.Interfaces;

namespace Devoctomy.Blazwiz.Auth.Services;

/// <inheritdoc/>
public class UserDisplayService
    : IUserDisplayService
{
    private static readonly char[] NameSeparators = [' ', '.', '_', '-'];

    /// <inheritdoc/>
    public string GetInitials(string? displayName)
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

    /// <inheritdoc/>
    public string GetDisplayNameFromUsername(string username)
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
