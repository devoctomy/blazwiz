using Devoctomy.Blazwiz.Auth.Models;

namespace Devoctomy.Blazwiz.Auth.Services.Interfaces;

/// <summary>
/// Holds the offline session in memory, so a reload always ends it. Shared by the authentication services.
/// </summary>
public interface IOfflineSessionStore
{
    /// <summary>
    /// Gets the offline session, or null. Its expiry is not checked here.
    /// </summary>
    UserSession? Session { get; }

    /// <summary>
    /// Starts an offline session lasting <see cref="Core.Offline.OfflineModeService.SessionLength"/> from now.
    /// </summary>
    /// <param name="username">The username.</param>
    /// <param name="displayName">The display name.</param>
    /// <param name="email">The email address, if known.</param>
    /// <returns>The session.</returns>
    UserSession Start(string username, string displayName, string? email);

    /// <summary>
    /// Forgets the offline session.
    /// </summary>
    void Clear();
}
