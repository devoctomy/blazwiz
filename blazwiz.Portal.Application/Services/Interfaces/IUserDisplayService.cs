namespace blazwiz.Portal.Application.Services.Interfaces;

/// <summary>
/// Presents user details, e.g. initials for the avatar.
/// </summary>
public interface IUserDisplayService
{
    /// <summary>
    /// Gets up to two initials for the given display name, e.g. "Bob Hoskins" becomes "BH".
    /// </summary>
    /// <param name="displayName">The display name.</param>
    /// <returns>The upper case initials, or "?" when no name is available.</returns>
    string GetInitials(string? displayName);

    /// <summary>
    /// Derives a friendly display name from a username or email address, e.g. "bob.hoskins@example.com" becomes "Bob Hoskins".
    /// </summary>
    /// <param name="username">The username or email address.</param>
    /// <returns>The display name.</returns>
    string GetDisplayNameFromUsername(string username);
}
