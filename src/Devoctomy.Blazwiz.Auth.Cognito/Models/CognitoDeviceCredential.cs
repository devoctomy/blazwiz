namespace Devoctomy.Blazwiz.Auth.Cognito.Models;

/// <summary>
/// What a remembered device signs in with (Cognito's device SRP): with the password, it skips the code from the
/// authenticator app, so it is kept as safely as the password.
/// </summary>
public class CognitoDeviceCredential
{
    /// <summary>
    /// Gets or sets the device key.
    /// </summary>
    public string DeviceKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the device group key.
    /// </summary>
    public string DeviceGroupKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the random password made for the device when it was confirmed.
    /// </summary>
    public string DevicePassword { get; set; } = string.Empty;
}
