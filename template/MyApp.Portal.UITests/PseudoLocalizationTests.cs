using System.Text.Json.Nodes;

namespace MyApp.Portal.UITests;

/// <summary>
/// Runs the app with every text from resources pseudo-localised (<c>Text:PseudoLocalization</c>, e.g.
/// <c>[!! Śéţţîñĝš !!]</c>) and checks that all visible framework text is: anything else is hard-coded, so it cannot be
/// reworded or translated.
/// </summary>
[TestClass]
public class PseudoLocalizationTests : TestBase
{
    private const string Username = "bob.hoskins@example.com";

    // The signed-in user's name and initials, worked out from the email address: data, not text.
    private static readonly string[] UserText = [Username, "Bob Hoskins", "BH"];

    // The app's own sidebar items: app text, not the framework's.
    private const string AppContent = "#nav-dashboard, #nav-showcase, #nav-files";

    private readonly List<string> appSettingsText = [];

    [TestInitialize]
    public async Task UsePseudoLocalization()
    {
        // The app reads Text:PseudoLocalization from its settings (wwwroot/appsettings*.json): add it as they are served,
        // and note the app's own text in them (title, description, copyright), which is not the framework's.
        await Context.RouteAsync("**/appsettings*.json", async route =>
        {
            var response = await route.FetchAsync();
            var settings = JsonNode.Parse(await response.TextAsync())!.AsObject();
            settings["Text"] = new JsonObject { ["PseudoLocalization"] = true };
            if (settings["AppSettings"] is JsonObject app)
            {
                appSettingsText.AddRange(app.Select(p => p.Value?.ToString() ?? string.Empty).Where(v => v.Length > 0));
            }

            await route.FulfillAsync(new() { Response = response, Body = settings.ToJsonString() });
        });
    }

    [TestMethod]
    public async Task GivenPseudoLocalization_WhenOpeningTheLoginPage_ThenAllTextIsFromResources()
    {
        // Act
        await GoToLoginAsync();

        // Assert
        await AssertNoHardCodedTextAsync("login page");
    }

    [TestMethod]
    public async Task GivenPseudoLocalization_WhenResettingAForgottenPassword_ThenAllTextIsFromResources()
    {
        // Arrange
        await Page.GotoAsync($"{ServerUrl}/forgot-password");
        await Expect(Page.Locator("#forgot-password-title")).ToContainTextAsync("[!!");
        await AssertNoHardCodedTextAsync("forgot password page");

        // Act
        await Page.Locator("#reset-email").FillAsync(Username);
        await Page.Locator("#send-code-button").ClickAsync();

        // Assert
        await Expect(Page.Locator("#reset-intro")).ToBeVisibleAsync();
        await AssertNoHardCodedTextAsync("reset password form");
    }

    [TestMethod]
    public async Task GivenPseudoLocalization_WhenOpeningEachFrameworkPage_ThenAllTextIsFromResources()
    {
        // Arrange
        await LoginAsync(Username);

        // Act & Assert: the shell (sidebar, top bar, banners) is checked on every framework page; the dashboard is the
        // app's own page.
        await Page.Locator("#nav-settings").ClickAsync();
        await Expect(Page.Locator("h1.page-title")).ToContainTextAsync("[!!");
        await AssertNoHardCodedTextAsync("settings page");

        await Page.GotoAsync($"{ServerUrl}/profile");
        await Expect(Page.Locator("h1.page-title")).ToContainTextAsync("[!!");
        await AssertNoHardCodedTextAsync("profile page");
//#if (IncludeSync)

        await Page.Locator("#nav-sync").ClickAsync();
        await Expect(Page.Locator("h1.page-title")).ToContainTextAsync("[!!");
        await AssertNoHardCodedTextAsync("sync page");
//#endif
    }

    // Version numbers, such as the framework's 0.1.0-preview.1: data, not text.
    private static bool IsVersion(string text) =>
        System.Text.RegularExpressions.Regex.IsMatch(text, @"^\d+\.\d+\.\d+(\.\d+)?(-[0-9A-Za-z.]+)?$");

    // Dates and times are formatted by the culture, so they are already translated: data, not text.
    private static bool IsDateOrTime(string text) =>
        DateTime.TryParse(text, System.Globalization.CultureInfo.GetCultureInfo("en-GB"), System.Globalization.DateTimeStyles.AllowWhiteSpaces, out _) ||
        DateTime.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AllowWhiteSpaces, out _);

    private async Task AssertNoHardCodedTextAsync(string where)
    {
        // Let the page settle: some text is filled in after the first render.
        await Page.WaitForTimeoutAsync(500);
        var texts = await Page.EvaluateAsync<string[]>(
            @"exempt => {
                const found = [];
                const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
                while (walker.nextNode()) {
                    const node = walker.currentNode;
                    const text = node.textContent.trim();
                    const element = node.parentElement;
                    if (!text || !element || element.closest('script, style, noscript, ' + exempt)) {
                        continue;
                    }

                    if (element.checkVisibility && !element.checkVisibility({ checkOpacity: true, checkVisibilityCSS: true })) {
                        continue;
                    }

                    found.push(text);
                }

                return found;
            }",
            AppContent);

        var allowed = new HashSet<string>(appSettingsText.Concat(UserText), StringComparer.Ordinal);
        var hardCoded = texts
            .Where(t => t.Any(char.IsLetter))
            .Where(t => !t.Contains("[!!", StringComparison.Ordinal) && !t.Contains("!!]", StringComparison.Ordinal))
            .Where(t => !allowed.Contains(t) && !allowed.Any(a => t.Contains(a, StringComparison.Ordinal)))
            .Where(t => !IsDateOrTime(t) && !IsVersion(t))
            .Distinct()
            .ToList();

        if (hardCoded.Count > 0)
        {
            await ScreenshotAsync(where.Replace(' ', '-'));
        }

        Assert.AreEqual(0, hardCoded.Count, $"Hard-coded text on the {where}:{Environment.NewLine}{string.Join(Environment.NewLine, hardCoded.Select(t => $"  \"{t}\""))}");
    }
}
