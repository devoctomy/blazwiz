using blazwiz.Core.Offline;
using blazwiz.Core.Time.Interfaces;
using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Models.Auth;

namespace blazwiz.Portal.Application.Services;

/// <summary>
/// Implementation of <see cref="IOfflineSessionStore"/> holding the session in a field.
/// </summary>
public class InMemoryOfflineSessionStore(ISystemClock systemClock)
    : IOfflineSessionStore
{
    private readonly ISystemClock systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));

    /// <inheritdoc/>
    public UserSession? Session { get; private set; }

    /// <inheritdoc/>
    public UserSession Start(string username, string displayName, string? email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        var now = systemClock.UtcNow;
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
