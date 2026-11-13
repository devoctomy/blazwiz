namespace MyApp.Portal.UITests;

/// <summary>
/// The indicator shown while a new version of the app downloads. A real update cannot be made in a test, so the
/// progress messages the service worker posts while installing are sent to the page by hand.
/// </summary>
[TestClass]
public class AppUpdateTests : TestBase
{
    [TestMethod]
    public async Task GivenAnUpdateDownloading_WhenProgressIsReported_ThenTheIndicatorShowsIt_AndGoesWhenDone()
    {
        await LoginAsync();

        // The page is controlled by the service worker from its second load: on the first the worker is installing.
        await ReloadAsync();
        await Page.WaitForFunctionAsync("() => navigator.serviceWorker.controller !== null");

        var indicator = Page.Locator("#app-update-progress");
        await PostInstallProgressAsync(3, 10);
        await Expect(indicator).ToBeVisibleAsync();
        await Expect(indicator).ToContainTextAsync("Downloading update… 30%");
        await ScreenshotAsync("Downloading");

        await PostInstallProgressAsync(10, 10);
        await Expect(indicator).ToBeHiddenAsync();
    }

    [TestMethod]
    public async Task GivenTheSignInPage_WhenAnUpdateDownloads_ThenTheIndicatorIsShownThereToo()
    {
        await GoToLoginAsync();
        await ReloadAsync();
        await Page.WaitForFunctionAsync("() => navigator.serviceWorker.controller !== null");

        await PostInstallProgressAsync(1, 4);

        await Expect(Page.Locator("#app-update-progress")).ToContainTextAsync("Downloading update… 25%");
    }

    /// <summary>
    /// Sends the page the message the service worker posts after caching each file, until the indicator's watcher has
    /// picked it up (it starts as the layout renders, so the first message can arrive before it listens).
    /// </summary>
    private async Task PostInstallProgressAsync(int done, int total)
    {
        var expectShown = done < total;
        for (var attempt = 0; attempt <= 20; attempt++)
        {
            await Page.EvaluateAsync(
                "([done, total]) => navigator.serviceWorker.dispatchEvent(new MessageEvent('message', { data: { type: 'INSTALL_PROGRESS', done, total } }))",
                new[] { done, total });
            await Page.WaitForTimeoutAsync(250);
            if (await Page.Locator("#app-update-progress").IsVisibleAsync() == expectShown)
            {
                return;
            }
        }
    }
}
