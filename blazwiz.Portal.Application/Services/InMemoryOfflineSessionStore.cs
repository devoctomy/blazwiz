using blazwiz.Core.Offline;
using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Models.Auth;

namespace blazwiz.Portal.Application.Services;

/// <summary>
/// Implementation of <see cref="IOfflineSessionStore"/> holding the session in a field.
/// </summary>
public class InMemoryOfflineSessionStore(TimeProvider timeProvider)
    : IOfflineSessionStore
{
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc/>
    public UserSession? Session { get; private set; }

    /// <inheritdoc/>
    public UserSession Start(string username, string displayName, string? email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        var now = timeProvider.GetUtcNow();
        Session = new UserSession
        {
            Username = username,
            DisplayName = displayName,
            Email = email,
            SignedInAt = now,
            ExpiresAt = now.Add(OfflineModeService.SessionLength),
            IsOffline = true,
        };
        return Session;
    }

    /// <inheritdoc/>
    public void Clear()
    {
        Session = null;
    }
}
