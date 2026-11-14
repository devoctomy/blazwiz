namespace MyApp.Portal.UITests;

[TestClass]
public class LoginPageTests : TestBase
{
    [TestMethod]
    public async Task GivenUnauthenticatedUser_WhenOpeningPortal_ThenLoginPageIsShown()
    {
        await GoToLoginAsync();

        await Expect(Page).ToHaveTitleAsync("Sign in - MyApp");
        await Expect(Page.Locator("h1.auth-title")).ToHaveTextAsync("MyApp");
        await Expect(Page.Locator("#username")).ToBeVisibleAsync();
        await Expect(Page.Locator("#password")).ToBeVisibleAsync();
        await Expect(Page.Locator("#theme-toggle")).ToBeVisibleAsync();

        await ScreenshotAsync("Login");
    }

    [TestMethod]
    public async Task GivenEmptyCredentials_WhenSigningIn_ThenErrorIsShown()
    {
        await GoToLoginAsync();

        await Page.Locator("#login-button").ClickAsync();

        await Expect(Page.Locator("#login-error")).ToContainTextAsync("Please enter your email and password.");
        await ExpectLoginPageAsync();
    }

    [TestMethod]
    public async Task GivenAnyCredentials_WhenSigningIn_ThenDashboardGreetsUser()
    {
        await LoginAsync("bob.hoskins@example.com");

        await Expect(Page.Locator("#dashboard-greeting")).ToContainTextAsync("Bob Hoskins");
        await Expect(Page.Locator("#nav-profile")).ToContainTextAsync("Bob Hoskins");

        await ScreenshotAsync("Dashboard");
    }

    [TestMethod]
    public async Task GivenRememberMeWithAName_WhenSignedOut_ThenTheRememberedUserIsShownByThatName()
    {
        // Arrange
        await GoToLoginAsync();
        await Expect(Page.Locator("#remember-me-name")).ToHaveCountAsync(0);
        await Page.Locator("#username").FillAsync("bob.hoskins@example.com");
        await Page.Locator("#password").FillAsync("password123");
        await Page.Locator("#remember-me").CheckAsync();
        await Page.Locator("#remember-me-name").FillAsync("Bob");
        await ScreenshotAsync("RememberMeName");
        await Page.Locator("#login-button").ClickAsync();
        await Page.Locator("#webauthn-skip").ClickAsync();
        await Expect(Page.Locator("#dashboard-greeting")).ToBeVisibleAsync();

        // Act
        await Page.Locator("#logout-button").ClickAsync();

        // Assert
        await Expect(Page.Locator("#saved-users .saved-user-name")).ToHaveTextAsync("Bob");
        await Expect(Page.Locator("#saved-users .saved-user-name")).ToHaveAttributeAsync("title", "bob.hoskins@example.com");
    }

    [TestMethod]
    public async Task GivenLoginPage_WhenTogglingTheme_ThenDarkThemeIsApplied()
    {
        await GoToLoginAsync();
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-bs-theme", "light");

        await Page.Locator("#theme-toggle").ClickAsync();

        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-bs-theme", "dark");
        await ScreenshotAsync("LoginDark");
    }
}
