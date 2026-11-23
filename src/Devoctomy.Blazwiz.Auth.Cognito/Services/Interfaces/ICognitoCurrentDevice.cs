namespace Devoctomy.Blazwiz.Auth.Cognito.Services.Interfaces;

/// <summary>
/// The key of the remembered device the current session signed in from, if any, held in memory: set by
/// <see cref="CognitoAuthenticationService"/>, and read to mark this device in the list of remembered devices.
/// </summary>
public interface ICognitoCurrentDevice
{
    /// <summary>
    /// Gets or sets the device key, or null when the session did not sign in from a remembered device.
    /// </summary>
    string? DeviceKey { get; set; }
}
