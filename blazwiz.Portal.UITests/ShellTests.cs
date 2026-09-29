using System.Text.RegularExpressions;

namespace blazwiz.Portal.UITests;

[TestClass]
public class ShellTests : TestBase
{
    [TestMethod]
    public async Task GivenExpandedSidebar_WhenCollapsing_ThenOnlyIconsAreShownAndStateIsRemembered()
    {
        await LoginAsync();
        var shell = Page.Locator(".app-shell");
        await Expect(shell).Not.ToHaveClassAsync(new Regex("sidebar-collapsed"));

        await Page.Locator("#sidebar-toggle").ClickAsync();

        await Expect(shell).ToHaveClassAsync(new Regex("sidebar-collapsed"));
        await Expect(Page.Locator("#sidebar")).ToHaveCSSAsync("width", "76px");
        await ScreenshotAsync("Collapsed");

        await Page.ReloadAsync();
        await Expect(Page.Locator(".app-shell")).ToHaveClassAsync(new Regex("sidebar-collapsed"));
    }

    [TestMethod]
    public async Task GivenSignedInUser_WhenTogglingTheme_ThenDarkThemeIsAppliedAndRemembered()
    {
        await LoginAsync();

        await Page.Locator("#theme-toggle").ClickAsync();
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-bs-theme", "dark");
        await Page.WaitForTimeoutAsync(500); // let the colour transition finish before capturing
        await ScreenshotAsync("DashboardDark");

        await Page.ReloadAsync();
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-bs-theme", "dark");
    }

    [TestMethod]
    public async Task GivenSignedInUser_WhenUsingNavigation_ThenPagesAreShown()
    {
        await LoginAsync();

        await Page.Locator("#nav-showcase").ClickAsync();
        await Expect(Page.Locator("h1.page-title")).ToContainTextAsync("Showcase");
        await Expect(Page.Locator("#nav-showcase")).ToHaveClassAsync(new Regex("active"));
        await ScreenshotAsync("Showcase");

        await Page.Locator("#nav-settings").ClickAsync();
        await Expect(Page.Locator("h1.page-title")).ToContainTextAsync("Settings");

        await Page.Locator("#nav-profile").ClickAsync();
        await Expect(Page.Locator("#profile-name")).ToHaveTextAsync("Bob Hoskins");

        await Page.Locator("#nav-dashboard").ClickAsync();
        await Expect(Page.Locator("#dashboard-greeting")).ToBeVisibleAsync();
    }

    [TestMethod]
    public async Task GivenSignedInUser_WhenLoggingOut_ThenLoginPageIsShownAndSessionIsCleared()
    {
        await LoginAsync();

        await Page.Locator("#logout-button").ClickAsync();
        await ExpectLoginPageAsync();

        // A protected page should bounce back to login now the session has gone.
        await Page.GotoAsync($"{ServerUrl}/settings");
        await ExpectLoginPageAsync();
    }

    [TestMethod]
    public async Task GivenMobileViewport_WhenOpeningMenu_ThenSidebarSlidesIn()
    {
        await Page.SetViewportSizeAsync(390, 844);
        await LoginAsync();
        var shell = Page.Locator(".app-shell");
        await Expect(Page.Locator("#mobile-menu-toggle")).ToBeVisibleAsync();
        await Expect(shell).Not.ToHaveClassAsync(new Regex("mobile-menu-open"));

        await Page.Locator("#mobile-menu-toggle").ClickAsync();
        await Expect(shell).ToHaveClassAsync(new Regex("mobile-menu-open"));
        await Expect(Page.Locator("#nav-settings")).ToBeVisibleAsync();
        await Page.WaitForTimeoutAsync(400);
        await ScreenshotAsync("MobileMenu");

        await Page.Locator("#nav-settings").ClickAsync();
        await Expect(Page.Locator("h1.page-title")).ToContainTextAsync("Settings");
        await Expect(shell).Not.ToHaveClassAsync(new Regex("mobile-menu-open"));
    }
}
