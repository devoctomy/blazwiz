using System.Net;

namespace Devoctomy.Blazwiz.DeployTests.StepDefinitions;

[Binding]
public sealed class PortalStepDefinitions(DeployTestContext context)
{
    private HttpStatusCode status;
    private string content = string.Empty;

    [When("the page {string} is requested")]
    public async Task WhenThePageIsRequested(string path)
    {
        using var response = await DeployTestContext.Http.GetAsync(new Uri(context.RequiredSettings.PortalUrl, path));
        status = response.StatusCode;
        content = await response.Content.ReadAsStringAsync();
    }

    [Then("the portal's index.html is returned")]
    public void ThenThePortalsIndexIsReturned()
    {
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains("blazor.webassembly", content, StringComparison.Ordinal);
    }

    [Then("the portal's settings name a user pool, an app client, an identity pool and a data bucket")]
    public void ThenThePortalsSettingsNameTheEnvironment()
    {
        var portal = context.RequiredPortal;
        Assert.StartsWith(portal.Region + "_", portal.UserPoolId, StringComparison.Ordinal);
        Assert.NotEmpty(portal.UserPoolClientId);
        Assert.StartsWith(portal.Region + ":", portal.IdentityPoolId, StringComparison.Ordinal);
        Assert.NotEmpty(portal.DataBucketName);
    }
}
