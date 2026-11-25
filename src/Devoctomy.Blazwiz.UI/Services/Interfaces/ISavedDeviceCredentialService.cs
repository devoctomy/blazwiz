namespace Devoctomy.Blazwiz.UI.Services.Interfaces;

/// <summary>
/// The copy of this device's credential that a user remembered with a passkey keeps on this device (it signs them in as
/// a remembered device, without a code from the authenticator app).
/// </summary>
public interface ISavedDeviceCredentialService
{
    /// <summary>
    /// Drops the signed in user's copy, once the sign-in service has forgotten this device: it is no use then, and the
    /// next sign in remembers the device again. Nothing happens if there is none.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task DropAsync();
}
