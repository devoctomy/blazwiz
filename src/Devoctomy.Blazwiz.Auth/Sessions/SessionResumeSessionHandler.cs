using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Auth.Sessions.Interfaces;

namespace Devoctomy.Blazwiz.Auth.Sessions;

/// <summary>
/// Starts the <see cref="ISessionResumeMonitor"/> with the session and stops it when the user signs out.
/// </summary>
/// <param name="monitor">The monitor.</param>
public class SessionResumeSessionHandler(ISessionResumeMonitor monitor)
    : ISessionLifecycleHandler
{
    private readonly ISessionResumeMonitor monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));

    /// <inheritdoc/>
    public Task OnSessionStartedAsync(UserSession session) => monitor.StartAsync();

    /// <inheritdoc/>
    public Task OnSigningOutAsync() => monitor.StopAsync();
}
