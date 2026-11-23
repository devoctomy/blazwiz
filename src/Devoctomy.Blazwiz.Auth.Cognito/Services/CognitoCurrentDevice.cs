using Devoctomy.Blazwiz.Auth.Cognito.Services.Interfaces;

namespace Devoctomy.Blazwiz.Auth.Cognito.Services;

/// <summary>
/// <see cref="ICognitoCurrentDevice"/> held in memory, for the app's lifetime.
/// </summary>
public class CognitoCurrentDevice : ICognitoCurrentDevice
{
    /// <inheritdoc/>
    public string? DeviceKey { get; set; }
}
