using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Auth.Sessions.Interfaces;
using Microsoft.Extensions.Logging;

namespace Devoctomy.Blazwiz.Events.AppSync.Sessions;

/// <summary>
/// Logs, once, that the app includes events but has no AppSync endpoints configured, so it runs without them: "Sign
/// out everywhere" is not offered, and other devices learn of a sign out only when they next check their session.
/// Registered instead of a channel when the settings are missing outside development.
/// </summary>
/// <param name="logger">The logger.</param>
public partial class MissingEventsConfigurationWarning(ILogger<MissingEventsConfigurationWarning> logger)
    : ISessionLifecycleHandler
{
    private readonly ILogger<MissingEventsConfigurationWarning> logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private bool warned;

    /// <inheritdoc/>
    public Task OnSessionStartedAsync(UserSession session)
    {
        if (!warned)
        {
            warned = true;
            LogMissing(logger);
        }

        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The app includes user events, but the Events settings (HttpEndpoint and RealtimeEndpoint) are missing, so it runs without them: \"Sign out everywhere\" is not offered. Deploy the stack with events enabled, which writes them to appsettings.Production.json.")]
    private static partial void LogMissing(ILogger logger);
}
