using System.Text.Json.Nodes;

namespace MyApp.Portal.UITests;

/// <summary>
/// Choosing the language in Settings, with a second language turned on for the test (<c>Text:SupportedCultures</c>
/// added to the app's settings as they are served). The framework only has English text, so French shows in what
/// the culture formats (dates) and in the document's language.
/// </summary>
[TestClass]
public class LanguageTests : TestBase
{
    [TestInitialize]
    public async Task OfferTwoLanguages()
    {
        await Context.RouteAsync("**/appsettings.json", async route =>
        {
            var response = await route.FetchAsync();
            var settings = JsonNode.Parse(await response.TextAsync())!.AsObject();
            settings["Text"] = new JsonObject { ["SupportedCultures"] = new JsonArray("en-GB", "fr-FR") };
            await route.FulfillAsync(new() { Response = response, Body = settings.ToJsonString() });
        });
    }

    [TestMethod]
    public async Task GivenTwoLanguages_WhenFrenchChosenInSettings_ThenTheAppReloadsInFrenchAndRemembersIt()
    {
        // Arrange
        await LoginAsync();
        await Page.Locator("#nav-settings").ClickAsync();
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("lang", "en-GB");

        // Act
        await Page.Locator("#language-select").SelectOptionAsync("fr-FR");

        // Assert
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("lang", "fr-FR");
        await Expect(Page.Locator("#language-select")).ToHaveValueAsync("fr-FR");
        await Page.GotoAsync($"{ServerUrl}/profile");
        await Expect(Page.Locator("dd").Nth(1)).ToContainTextAsync("novembre");
        await ScreenshotAsync("French");

        await Page.Locator("#nav-settings").ClickAsync();
        await Page.Locator("#language-select").SelectOptionAsync("en-GB");
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("lang", "en-GB");
    }

    [TestMethod]
    public async Task GivenOneLanguage_WhenOpeningSettings_ThenNoLanguageChoice()
    {
        // Arrange
        await Context.UnrouteAsync("**/appsettings.json");
        await LoginAsync();

        // Act
        await Page.Locator("#nav-settings").ClickAsync();

        // Assert
        await Expect(Page.Locator("h1.page-title")).ToBeVisibleAsync();
        await Expect(Page.Locator("#language-settings")).ToHaveCountAsync(0);
    }
}
