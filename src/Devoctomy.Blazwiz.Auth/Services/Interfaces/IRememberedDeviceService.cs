using Devoctomy.Blazwiz.Auth.Models;

namespace Devoctomy.Blazwiz.Auth.Services.Interfaces;

/// <summary>
/// The signed-in user's remembered devices: those that skip the code from the authenticator app (see
/// <see cref="IAuthenticationService.RememberDeviceAsync"/>). Needs the sign-in service: failures throw a
/// <see cref="Core.Exceptions.BlazwizException"/> with one of <see cref="Exceptions.AuthErrorCodes"/>.
/// </summary>
public interface IRememberedDeviceService
{
    /// <summary>
    /// Raised once a device has been forgotten, with its <see cref="RememberedDevice.Key"/>, so anything listing the
    /// devices can drop it.
    /// </summary>
    event EventHandler<string>? Forgotten;

    /// <summary>
    /// Lists the remembered devices, most recently used first.
    /// </summary>
    /// <returns>The devices.</returns>
    Task<IReadOnlyList<RememberedDevice>> ListAsync();

    /// <summary>
    /// Forgets a device: its next sign in asks for a code again.
    /// </summary>
    /// <param name="deviceKey">The device's <see cref="RememberedDevice.Key"/>.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task ForgetAsync(string deviceKey);
}
