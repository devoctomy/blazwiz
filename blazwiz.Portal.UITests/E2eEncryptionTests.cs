namespace blazwiz.Portal.UITests;

/// <summary>
/// The UI tests use the placeholder sign-in with no AWS, so there is no Secrets Manager and no passkey PRF: these
/// cover what the user sees without them. Turning encryption on and off is tested on a deployed portal.
/// </summary>
[TestClass]
public class E2eEncryptionTests : TestBase
{
    [TestMethod]
    public async Task GivenPlaceholderSignIn_WhenOpeningSettings_ThenEncryptionIsNotOfferedAndNoBannerShown()
    {
        await LoginAsync();
        await Page.Locator("#nav-settings").ClickAsync();

        await Expect(Page.Locator("#e2e-section")).ToBeVisibleAsync();
        await Expect(Page.Locator("#e2e-status")).ToContainTextAsync("Not available with this sign in.");
        await Expect(Page.Locator("#e2e-enable")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#e2e-change")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#e2e-disable")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#e2e-banner")).ToHaveCountAsync(0);
        await ScreenshotAsync("EncryptionSection");
    }

    [TestMethod]
    public async Task GivenReleaseBuild_WhenOpeningSettings_ThenArgon2idPanelShowsTheReleaseSettings()
    {
        // The UI tests run the portal in Release, as deployments are.
        await LoginAsync();
        await Page.Locator("#nav-settings").ClickAsync();

        await Expect(Page.Locator("#argon2id-card")).ToBeVisibleAsync();
        await Expect(Page.Locator("#argon2id-memory")).ToHaveTextAsync("512 MiB");
        await Expect(Page.Locator("#argon2id-iterations")).ToHaveTextAsync("2");
        await Expect(Page.Locator("#argon2id-parallelism")).ToHaveTextAsync("1");
        await Expect(Page.Locator("#argon2id-key-length")).ToHaveTextAsync("32 bytes");
        await Expect(Page.Locator("#argon2id-benchmark")).ToBeEnabledAsync();
        await ScreenshotAsync("Argon2idPanel");
    }

    [TestMethod]
    public async Task GivenPlainNotes_WhenListingFiles_ThenNoLocksShown()
    {
        await LoginAsync();
        await Page.Locator("#nav-files").ClickAsync();

        await Expect(Page.Locator(".file-row", new() { HasText = "Welcome.md" })).ToBeVisibleAsync();
        await Expect(Page.Locator(".file-lock")).ToHaveCountAsync(0);
    }
}
