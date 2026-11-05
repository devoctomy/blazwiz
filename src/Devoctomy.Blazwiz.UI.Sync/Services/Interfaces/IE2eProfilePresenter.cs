using Devoctomy.Blazwiz.UI.Sync.Models;

namespace Devoctomy.Blazwiz.UI.Sync.Services.Interfaces;

/// <summary>
/// Describes the user's profiles as the end to end encryption settings and the unlock panel show them: each one's
/// name, whether it has a passphrase yet, whether it is locked, and whether the passkey can unlock it.
/// </summary>
public interface IE2eProfilePresenter
{
    /// <summary>
    /// Describes the user's profiles: those set up in code, then those the user created. Empty until they are loaded.
    /// </summary>
    /// <returns>The profiles.</returns>
    Task<IReadOnlyList<E2eProfileRow>> GetProfilesAsync();

    /// <summary>
    /// Describes one profile.
    /// </summary>
    /// <param name="name">The sync profile's name.</param>
    /// <returns>The profile, or null when there is no such profile or the profiles are not loaded yet.</returns>
    Task<E2eProfileRow?> GetProfileAsync(string name);
}
