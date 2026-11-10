namespace Devoctomy.Blazwiz.Events.AppSync.Models;

/// <summary>
/// The AppSync Events API's endpoints, from the <c>Events</c> configuration section. <c>cloudformation/deploy.ps1</c>
/// writes them to <c>appsettings.Production.json</c> from the stack's <c>EventsHttpEndpoint</c> and
/// <c>EventsRealtimeEndpoint</c> outputs.
/// </summary>
public sealed class AppSyncEventsConfig
{
    /// <summary>
    /// Gets or sets the API's HTTP host, which events are published to, e.g.
    /// <c>abc123.appsync-api.eu-west-2.amazonaws.com</c>.
    /// </summary>
    public string HttpEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the API's real-time host, which the channel's WebSocket connects to, e.g.
    /// <c>abc123.appsync-realtime-api.eu-west-2.amazonaws.com</c>.
    /// </summary>
    public string RealtimeEndpoint { get; set; } = string.Empty;
}
