namespace MyApp.Portal.UITests;

/// <summary>
/// The UI tests use the placeholder sign-in with no AWS, so the keys are kept in the browser and there is no passkey
/// PRF. Keeping them in Secrets Manager is tested on a deployed portal.
/// </summary>
[TestClass]
public class E2eEncryptionTests : TestBase
{
    [TestMethod]
    public async Task GivenNoPassphrase_WhenOpeningSettings_ThenEncryptionIsOfferedAndNoBannerShown()
    {
        await LoginAsync();
        await Page.Locator("#nav-settings").ClickAsync();

        await Expect(Page.Locator("#e2e-section")).ToBeVisibleAsync();
        await Expect(Page.Locator("#e2e-status")).ToContainTextAsync("Off.");
        await Expect(Page.Locator("#e2e-enable")).ToBeVisibleAsync();
        await Expect(Page.Locator("#e2e-change")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#e2e-banner")).ToHaveCountAsync(0);
        await ScreenshotAsync("EncryptionSection");
    }

    [TestMethod]
    public async Task GivenNoPassphrase_WhenSet_ThenEncryptionIsOnAndThePassphraseCanBeChanged()
    {
        await LoginAsync();

        await SetPassphraseAsync();

        await Page.Locator("#nav-settings").ClickAsync();
        await Expect(Page.Locator("#e2e-status")).ToContainTextAsync("On.");
        await Expect(Page.Locator("#e2e-enable")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#e2e-change")).ToBeVisibleAsync();
        await ScreenshotAsync("EncryptionOn");
    }

    [TestMethod]
    public async Task GivenAPassphrase_WhenReloaded_ThenTheBannerAsksForItAndItUnlocks()
    {
        await LoginAsync();
        await SetPassphraseAsync();

        await ReloadAsync();

        await Expect(Page.Locator("#e2e-banner")).ToBeVisibleAsync();
        await ScreenshotAsync("Locked");
        await UnlockAsync();
        await Page.Locator("#nav-settings").ClickAsync();
        await Expect(Page.Locator("#e2e-status")).ToContainTextAsync("On.");
    }

    [TestMethod]
    public async Task GivenArgon2idConfigured_WhenOpeningSettings_ThenArgon2idPanelShowsTheConfiguredSettings()
    {
        // The UI tests run the portal in the Development environment, whose appsettings lighten the Release settings.
        await LoginAsync();
        await Page.Locator("#nav-settings").ClickAsync();

        await Expect(Page.Locator("#argon2id-card")).ToBeVisibleAsync();
        await Expect(Page.Locator("#argon2id-memory")).ToHaveTextAsync("19 MiB");
        await Expect(Page.Locator("#argon2id-iterations")).ToHaveTextAsync("2");
        await Expect(Page.Locator("#argon2id-parallelism")).ToHaveTextAsync("1");
        await Expect(Page.Locator("#argon2id-key-length")).ToHaveTextAsync("32 bytes");
        await Expect(Page.Locator("#argon2id-benchmark")).ToBeEnabledAsync();
        await ScreenshotAsync("Argon2idPanel");
    }
}
