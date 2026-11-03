namespace Devoctomy.Blazwiz.UI.Sync.Services.Interfaces;

/// <summary>
/// Uses the signed in user's saved passkey again, without signing in again, so the keys derived from its PRF are
/// back in memory, e.g. after a reload with a saved session (the keys are only ever held in memory).
/// </summary>
public interface IPasskeyUnlockService
{
    /// <summary>
    /// Gets whether the signed in user has a passkey saved on this device.
    /// </summary>
    /// <returns>True when there is one to use.</returns>
    Task<bool> CanUnlockAsync();

    /// <summary>
    /// Asks the user for their passkey and derives the PRF keys.
    /// </summary>
    /// <returns>True when the keys were derived.</returns>
    Task<bool> UnlockAsync();
}
