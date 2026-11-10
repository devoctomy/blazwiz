using System.Text.Json;

namespace Devoctomy.Blazwiz.DeployTests.Support;

/// <summary>
/// The deployed portal's own settings (<c>appsettings.Production.json</c>, served publicly), which <c>deploy.ps1</c>
/// writes from the stack's outputs: everything the tests need to reach the environment as the portal does.
/// </summary>
public sealed class PortalSettings
{
    public required string Region { get; init; }

    public required string UserPoolId { get; init; }

    public required string UserPoolClientId { get; init; }

    public required string IdentityPoolId { get; init; }

    public required string DataBucketName { get; init; }

    /// <summary>
    /// Gets the user events API's HTTP host, or null when the stack has none.
    /// </summary>
    public string? EventsHttpEndpoint { get; init; }

    /// <summary>
    /// Fetches and reads the settings.
    /// </summary>
    /// <param name="http">The HTTP client.</param>
    /// <param name="portalUrl">The portal's address.</param>
    /// <returns>The settings.</returns>
    public static async Task<PortalSettings> FetchAsync(HttpClient http, Uri portalUrl)
    {
        using var document = JsonDocument.Parse(await http.GetStringAsync(new Uri(portalUrl, "appsettings.Production.json")));
        var root = document.RootElement;
        var cognito = root.GetProperty("AwsCognito");
        var s3 = root.GetProperty("AwsS3");
        string? events = null;
        if (root.TryGetProperty("Events", out var eventsSection) && eventsSection.TryGetProperty("HttpEndpoint", out var host))
        {
            events = host.GetString();
        }

        return new PortalSettings
        {
            Region = Required(cognito, "Region"),
            UserPoolId = Required(cognito, "UserPoolId"),
            UserPoolClientId = Required(cognito, "UserPoolClientId"),
            IdentityPoolId = Required(cognito, "IdentityPoolId"),
            DataBucketName = Required(s3, "DataBucketName"),
            EventsHttpEndpoint = string.IsNullOrWhiteSpace(events) ? null : events,
        };
    }

    private static string Required(JsonElement section, string name)
    {
        var value = section.TryGetProperty(name, out var property) ? property.GetString() : null;
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"The portal's appsettings.Production.json has no {name}.")
            : value;
    }
}
