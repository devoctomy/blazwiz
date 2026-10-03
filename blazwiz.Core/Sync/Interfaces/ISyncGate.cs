namespace blazwiz.Core.Sync.Interfaces;

/// <summary>
/// Lets a feature hold back a profile's passes until it is ready, e.g. end to end encryption until this device can
/// encrypt. <see cref="ISyncService"/> skips a profile while any registered gate holds it.
/// </summary>
public interface ISyncGate
{
    /// <summary>
    /// Gets whether the profile may sync now.
    /// </summary>
    /// <param name="profile">The profile.</param>
    /// <returns>True to let it sync.</returns>
    bool CanSync(string profile);
}
