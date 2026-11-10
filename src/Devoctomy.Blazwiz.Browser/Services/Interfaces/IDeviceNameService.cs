namespace Devoctomy.Blazwiz.Browser.Services.Interfaces;

/// <summary>
/// The name of this browser, so the user can tell their devices apart (e.g. "You were signed out from
/// Android-Edge-624834"). It is kept only in this browser's local storage. Until the user names it, it is made on
/// first use from the platform and browser (a best guess) and six random digits.
/// </summary>
public interface IDeviceNameService
{
    /// <summary>
    /// Gets the device's name, making and saving the default one on first use.
    /// </summary>
    /// <returns>The name.</returns>
    Task<string> GetNameAsync();

    /// <summary>
    /// Renames the device.
    /// </summary>
    /// <param name="name">The new name: trimmed, it must be 1 to <c>DeviceNameService.MaxLength</c> characters, with no control characters.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">The name is empty, too long or has control characters.</exception>
    Task SetNameAsync(string name);

    /// <summary>
    /// Makes a name safe to show: trimmed, control characters removed and cut to <c>DeviceNameService.MaxLength</c>.
    /// Use it on names from other devices, which this one cannot trust.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns>The name to show, or null when nothing is left of it.</returns>
    string? Sanitize(string? name);
}
