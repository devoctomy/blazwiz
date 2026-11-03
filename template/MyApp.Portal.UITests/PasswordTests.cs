namespace MyApp.Portal.UITests;

/// <summary>
/// Changing the password on the Profile page, and resetting a forgotten one from the sign-in page. Run against the
/// placeholder sign in, which accepts any password and code; the Cognito calls are covered by the unit tests of
/// Devoctomy.Blazwiz.Auth.Cognito.
/// </summary>
[TestClass]
public class PasswordTests : TestBase
{
    [TestMethod]
    public async Task GivenSignedInUser_WhenChangingThePassword_ThenItIsChangedAndTheFormCleared()
    {
        // Arrange
        await LoginAsync();
        await Page.Locator("#nav-profile").ClickAsync();
        await Page.Locator("#current-password").FillAsync("password123");
        await Page.Locator("#change-new-password").FillAsync("a-new-password-1");
        await Page.Locator("#change-confirm-password").FillAsync("a-new-password-1");

        // Act
        await Page.Locator("#change-password-button").ClickAsync();

        // Assert
        await Expect(Page.Locator("#change-password-done")).ToBeVisibleAsync();
        await Expect(Page.Locator("#current-password")).ToHaveValueAsync(string.Empty);
        await Expect(Page.Locator("#change-password-error")).ToHaveCountAsync(0);
        await ScreenshotAsync("Changed");
    }

    [TestMethod]
    public async Task GivenDifferentConfirmation_WhenChangingThePassword_ThenToldTheyDoNotMatch()
    {
        // Arrange
        await LoginAsync();
        await Page.Locator("#nav-profile").ClickAsync();
        await Page.Locator("#current-password").FillAsync("password123");
        await Page.Locator("#change-new-password").FillAsync("a-new-password-1");
        await Page.Locator("#change-confirm-password").FillAsync("something-else");

        // Act
        await Page.Locator("#change-password-button").ClickAsync();

        // Assert
        await Expect(Page.Locator("#change-password-error")).ToBeVisibleAsync();
        await Expect(Page.Locator("#change-password-done")).ToHaveCountAsync(0);
    }

    [TestMethod]
    public async Task GivenForgottenPassword_WhenResettingItFromTheSignInPage_ThenDoneAndBackToSignIn()
    {
        // Arrange
        await GoToLoginAsync();
        await Page.Locator("#forgot-password-link").ClickAsync();
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/forgot-password$"));

        // Act
        await Page.Locator("#reset-email").FillAsync("bob.hoskins@example.com");
        await Page.Locator("#send-code-button").ClickAsync();
        await Expect(Page.Locator("#reset-intro")).ToContainTextAsync("bob.hoskins@example.com");
        await Page.Locator("#reset-code").FillAsync("123456");
        await Page.Locator("#reset-new-password").FillAsync("a-new-password-1");
        await Page.Locator("#reset-confirm-password").FillAsync("a-new-password-1");
        await Page.Locator("#reset-password-button").ClickAsync();

        // Assert
        await Expect(Page.Locator("#reset-done")).ToBeVisibleAsync();
        await ScreenshotAsync("Reset");
        await Page.Locator("#back-to-sign-in").ClickAsync();
        await ExpectLoginPageAsync();
    }

    [TestMethod]
    public async Task GivenNoEmail_WhenAskingForACode_ThenToldToEnterIt()
    {
        // Arrange
        await GoToAsync("/forgot-password");

        // Act
        await Page.Locator("#send-code-button").ClickAsync();

        // Assert
        await Expect(Page.Locator("#form-error")).ToBeVisibleAsync();
        await Expect(Page.Locator("#reset-email")).ToBeVisibleAsync();
    }
}
