using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Auth.Services.Interfaces;

namespace Devoctomy.Blazwiz.Auth.Services;

/// <summary>
/// Placeholder remembered devices, used with <see cref="LocalAuthenticationService"/> (no identity provider, e.g.
/// local development), which never remembers a device: there are none to list or forget.
/// </summary>
public class LocalRememberedDeviceService : IRememberedDeviceService
{
    /// <inheritdoc/>
    /// <remarks>Never raised: no device is remembered, so none is forgotten.</remarks>
    public event EventHandler<string>? Forgotten
    {
        add { }
        remove { }
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<RememberedDevice>> ListAsync()
    {
        return Task.FromResult<IReadOnlyList<RememberedDevice>>([]);
    }

    /// <inheritdoc/>
    public Task ForgetAsync(string deviceKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceKey);
        return Task.CompletedTask;
    }
}
