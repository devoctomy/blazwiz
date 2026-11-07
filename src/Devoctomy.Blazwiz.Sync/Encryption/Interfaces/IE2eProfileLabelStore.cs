namespace Devoctomy.Blazwiz.Sync.Encryption.Interfaces;

/// <summary>
/// The labels the signed in user's protected profiles are shown by on this device, by profile id. Kept only on the
/// device, never synced, so a profile is shown by its id on a new device until it is unlocked there and the app labels
/// it again. A label can say what a profile holds, so an implementation must not keep it in plain text.
/// </summary>
public interface IE2eProfileLabelStore
{
    /// <summary>
    /// Gets every label of the user's profiles on this device.
    /// </summary>
    /// <returns>The labels, by profile id.</returns>
    Task<IReadOnlyDictionary<string, string>> GetAllAsync();

    /// <summary>
    /// Sets a profile's label.
    /// </summary>
    /// <param name="profileId">The profile's id.</param>
    /// <param name="label">The label.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task SetAsync(string profileId, string label);

    /// <summary>
    /// Removes a profile's label.
    /// </summary>
    /// <param name="profileId">The profile's id.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task RemoveAsync(string profileId);
}
