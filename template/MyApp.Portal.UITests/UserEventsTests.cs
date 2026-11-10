using System.Text.RegularExpressions;

namespace MyApp.Portal.UITests;

/// <summary>
/// Signing out everywhere, with two pages of one browser signed in as the same user. Run against the placeholder sign
/// in, where the local event channel (the browser's other tabs) stands in for AppSync; the AppSync channel and Cognito's
/// global sign out are covered by the unit tests and by the deploy tests (tests/Devoctomy.Blazwiz.DeployTests).
/// </summary>
[TestClass]
public class UserEventsTests : TestBase
{
    private static readonly Regex LoginUrl = new("/login$");

    [TestMethod]
    public async Task GivenTwoSignedInPages_WhenOneSignsOutEverywhere_ThenBothReturnToSignIn_AndTheOtherSaysWhy()
    {
        // Arrange
        await LoginAsync();
        var other = await Context.NewPageAsync();
        await GoToAsync(other);
        await Expect(other.Locator("#dashboard-greeting")).ToBeVisibleAsync();
        await Page.Locator("#nav-profile").ClickAsync();

        // Act
        await Page.Locator("#sign-out-everywhere-button").ClickAsync();
        await Expect(Page.Locator("#sign-out-everywhere-dialog")).ToBeVisibleAsync();
        await Page.Locator("#sign-out-everywhere-confirm").ClickAsync();

        // Assert
        await Expect(Page).ToHaveURLAsync(LoginUrl);
        await Expect(Page.Locator("#session-ended-notice")).ToHaveCountAsync(0);
        await Expect(other).ToHaveURLAsync(LoginUrl);
        await Expect(other.Locator("#session-ended-notice")).ToContainTextAsync("You were signed out from ");
        await ScreenshotAsync("Signed out everywhere");
    }

    [TestMethod]
    public async Task GivenTheDeviceRenamed_WhenItSignsOutEverywhere_ThenTheOtherPageNamesIt()
    {
        // Arrange
        await LoginAsync();
        await Page.Locator("#nav-settings").ClickAsync();
        await Expect(Page.Locator("#device-name")).Not.ToHaveValueAsync(string.Empty);
        await Page.Locator("#device-name").FillAsync("Work laptop");
        await Page.Locator("#device-name-save").ClickAsync();
        await Expect(Page.Locator("#device-name-saved")).ToBeVisibleAsync();
        var other = await Context.NewPageAsync();
        await GoToAsync(other);
        await Expect(other.Locator("#dashboard-greeting")).ToBeVisibleAsync();
        await Page.Locator("#nav-profile").ClickAsync();

        // Act
        await Page.Locator("#sign-out-everywhere-button").ClickAsync();
        await Page.Locator("#sign-out-everywhere-confirm").ClickAsync();

        // Assert
        await Expect(other.Locator("#session-ended-notice")).ToHaveTextAsync("You were signed out from Work laptop.");
    }

    [TestMethod]
    public async Task GivenTheConfirmation_WhenCancelled_ThenStillSignedIn()
    {
        // Arrange
        await LoginAsync();
        await Page.Locator("#nav-profile").ClickAsync();
        await Page.Locator("#sign-out-everywhere-button").ClickAsync();

        // Act
        await Page.Locator("#sign-out-everywhere-cancel").ClickAsync();

        // Assert
        await Expect(Page.Locator("#sign-out-everywhere-dialog")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#profile-name")).ToBeVisibleAsync();
    }
}
