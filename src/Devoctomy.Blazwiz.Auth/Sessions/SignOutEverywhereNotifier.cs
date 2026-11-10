using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Auth.Sessions.Interfaces;
using Devoctomy.Blazwiz.Browser.Services.Interfaces;

namespace Devoctomy.Blazwiz.Auth.Sessions;

/// <inheritdoc/>
public class SignOutEverywhereNotifier(IEnumerable<ISignOutEverywhereHandler> handlers, IDeviceNameService deviceNameService)
    : ISignOutEverywhereNotifier
{
    private readonly IReadOnlyList<ISignOutEverywhereHandler> handlers =
        (handlers ?? throw new ArgumentNullException(nameof(handlers))).ToList();

    private readonly IDeviceNameService deviceNameService = deviceNameService ?? throw new ArgumentNullException(nameof(deviceNameService));

    /// <inheritdoc/>
    public bool HasHandlers => handlers.Count > 0;

    /// <inheritdoc/>
    public async Task NotifyAsync(UserSession session, SignOutReason reason)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (handlers.Count == 0)
        {
            return;
        }

        string deviceName;
        try
        {
            deviceName = await deviceNameService.GetNameAsync();
        }
        catch (Exception)
        {
            // Telling other devices matters more than naming this one.
            deviceName = string.Empty;
        }

        foreach (var handler in handlers)
        {
            try
            {
                await handler.OnSigningOutEverywhereAsync(session, reason, deviceName);
            }
            catch (Exception)
            {
                // Telling other devices is best effort: every session is already revoked, and they find out when they
                // next use it.
            }
        }
    }
}
