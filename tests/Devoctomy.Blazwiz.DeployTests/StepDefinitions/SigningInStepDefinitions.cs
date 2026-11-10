using Amazon.CognitoIdentityProvider.Model;
using Devoctomy.Blazwiz.DeployTests.Support;

namespace Devoctomy.Blazwiz.DeployTests.StepDefinitions;

[Binding]
public sealed class SigningInStepDefinitions(DeployTestContext context)
{
    [When("the test user signs in")]
    [Given("the test user is signed in")]
    public async Task WhenTheTestUserSignsIn()
    {
        var settings = context.RequiredSettings;
        context.User = new TestUser(context.RequiredPortal, settings.Email, settings.Password);
        await context.User.SignInAsync();
    }

    [Given("the test user is signed in with AWS credentials")]
    public async Task GivenTheTestUserIsSignedInWithAwsCredentials()
    {
        await WhenTheTestUserSignsIn();
        await context.RequiredUser.GetCredentialsAsync();
    }

    [When("they get AWS credentials from the identity pool")]
    public Task WhenTheyGetAwsCredentials() => context.RequiredUser.GetCredentialsAsync();

    [Then("they have an ID token and an access token")]
    public void ThenTheyHaveTokens()
    {
        Assert.False(string.IsNullOrEmpty(context.RequiredUser.IdToken));
        Assert.False(string.IsNullOrEmpty(context.RequiredUser.AccessToken));
    }

    [Then("they have an identity id and temporary AWS credentials")]
    public void ThenTheyHaveCredentials()
    {
        var user = context.RequiredUser;
        Assert.StartsWith(context.RequiredPortal.Region + ":", user.IdentityId, StringComparison.Ordinal);
        Assert.NotNull(user.Credentials);
        Assert.False(string.IsNullOrEmpty(user.Credentials.AccessKeyId));
        Assert.False(string.IsNullOrEmpty(user.Credentials.SessionToken));
    }

    [When("they sign out everywhere")]
    public Task WhenTheySignOutEverywhere() =>
        context.RequiredUser.UserPool.GlobalSignOutAsync(new GlobalSignOutRequest { AccessToken = context.RequiredUser.AccessToken });

    [Then("their access token is refused")]
    public async Task ThenTheirAccessTokenIsRefused()
    {
        await Assert.ThrowsAsync<NotAuthorizedException>(() =>
            context.RequiredUser.UserPool.GetUserAsync(new GetUserRequest { AccessToken = context.RequiredUser.AccessToken }));
    }
}
