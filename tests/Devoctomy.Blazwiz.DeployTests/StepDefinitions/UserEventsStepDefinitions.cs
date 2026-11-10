using System.Net;
using System.Text;
using System.Text.Json;

namespace Devoctomy.Blazwiz.DeployTests.StepDefinitions;

/// <summary>
/// Publishes to the user events API over HTTPS with the user's access token, as the portal does. The event's type is
/// one the portal does not know, so open portals ignore it.
/// </summary>
[Binding]
public sealed class UserEventsStepDefinitions(DeployTestContext context)
{
    private HttpStatusCode status;
    private bool failedInBody;

    [When("they publish a test event to their own channel")]
    public Task WhenTheyPublishToTheirOwnChannel() => PublishAsync($"/users/{Subject(context.RequiredUser.AccessToken!)}");

    [When("they publish a test event to another user's channel")]
    public Task WhenTheyPublishToAnotherUsersChannel() => PublishAsync("/users/someone-else");

    [Then("the event is accepted")]
    public void ThenTheEventIsAccepted()
    {
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.False(failedInBody, "The API accepted the request but refused the event.");
    }

    [Then("the event is refused")]
    public void ThenTheEventIsRefused()
    {
        Assert.True(status != HttpStatusCode.OK || failedInBody, "Expected the channel handler to refuse it.");
    }

    private static string Subject(string token)
    {
        var payload = token.Split('.')[1].Replace('-', '+').Replace('_', '/');
        payload += new string('=', (4 - (payload.Length % 4)) % 4);
        using var claims = JsonDocument.Parse(Convert.FromBase64String(payload));
        return claims.RootElement.GetProperty("sub").GetString()!;
    }

    private async Task PublishAsync(string channel)
    {
        var body = JsonSerializer.Serialize(new
        {
            channel,
            events = new[] { JsonSerializer.Serialize(new { type = "deploy-test", from = "deploy-tests" }) },
        });
        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://{context.RequiredPortal.EventsHttpEndpoint}/event")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("Authorization", context.RequiredUser.AccessToken);
        using var response = await DeployTestContext.Http.SendAsync(request);
        status = response.StatusCode;
        if (status == HttpStatusCode.OK)
        {
            using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            failedInBody = result.RootElement.TryGetProperty("failed", out var failed)
                && failed.ValueKind == JsonValueKind.Array
                && failed.GetArrayLength() > 0;
        }
    }
}
