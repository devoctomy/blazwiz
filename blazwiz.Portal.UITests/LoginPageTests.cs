namespace blazwiz.Portal.UITests;

[TestClass]
public class LoginPageTests : TestBase
{
    [TestMethod]
    public async Task GivenUnauthenticatedUser_WhenOpeningPortal_ThenLoginPageIsShown()
    {
        await GoToLoginAsync();

        await Expect(Page).ToHaveTitleAsync("Sign in - blazwiz");
        await Expect(Page.Locator("h1.auth-title")).ToHaveTextAsync("blazwiz");
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
    public async Task GivenLoginPage_WhenTogglingTheme_ThenDarkThemeIsApplied()
    {
        await GoToLoginAsync();
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-bs-theme", "light");

        await Page.Locator("#theme-toggle").ClickAsync();

        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-bs-theme", "dark");
        await ScreenshotAsync("LoginDark");
    }
}
