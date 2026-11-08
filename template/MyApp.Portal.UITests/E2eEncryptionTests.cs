namespace MyApp.Portal.UITests;

/// <summary>
/// The UI tests use the placeholder sign-in with no AWS, so the keys are kept in the browser and there is no passkey
/// PRF. Keeping them in Secrets Manager, and unlocking with the passkey, are tested on a deployed portal.
/// </summary>
[TestClass]
public class E2eEncryptionTests : TestBase
{
    [TestMethod]
    public async Task GivenNoPassphrase_WhenSignedIn_ThenTheBannerAndSettingsAskForOne()
    {
        await LoginAsync();

        await Expect(Page.Locator("#e2e-banner-text")).ToContainTextAsync("needs a passphrase before it can be used.");
        await Page.Locator("#e2e-banner-settings").ClickAsync();
        await Expect(Page.Locator("#e2e-app-profiles .e2e-profile-state").First).ToHaveTextAsync("No passphrase yet");
        await Expect(Page.Locator("#e2e-no-created")).ToBeVisibleAsync();
        await Expect(Page.Locator("#e2e-lock-indicator")).ToHaveCountAsync(0);
        await ScreenshotAsync("EncryptionSection");
    }

    [TestMethod]
    public async Task GivenNoPassphrase_WhenSet_ThenUnlockedWithTheLockIndicatorAndNoBanner()
    {
        await LoginAsync();

        await SetPassphraseAsync();

        await Expect(Page.Locator("#e2e-banner")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#e2e-lock-indicator")).ToContainTextAsync("1 unlocked");
        await Page.Locator("#nav-settings").ClickAsync();
        await Expect(Page.Locator("#e2e-app-profiles .e2e-profile-state").First).ToHaveTextAsync("Unlocked");
        await Expect(Page.Locator("#e2e-app-profiles .e2e-change").First).ToBeVisibleAsync();
        await ScreenshotAsync("Unlocked");
    }

    [TestMethod]
    public async Task GivenUnlocked_WhenLockAllFromTheIndicator_ThenLockedUntilItsPassphraseIsEntered()
    {
        await LoginAsync();
        await SetPassphraseAsync();

        await Page.Locator("#e2e-lock-indicator").ClickAsync();
        await Page.Locator("#e2e-lock-menu-lock-all").ClickAsync();

        await Expect(Page.Locator("#e2e-lock-indicator")).ToHaveCountAsync(0);
        await Page.Locator("#nav-settings").ClickAsync();
        await Expect(Page.Locator("#e2e-app-profiles .e2e-profile-state").First).ToHaveTextAsync("Locked");
        await Page.Locator("#e2e-app-profiles .e2e-unlock").First.ClickAsync();
        await Page.Locator("#e2e-passphrase").FillAsync("not the passphrase");
        await Page.Locator("#e2e-confirm").ClickAsync();
        await Expect(Page.Locator("#e2e-error")).ToHaveTextAsync("That passphrase is not correct. Try again.");
        await Page.Locator("#e2e-passphrase").FillAsync(Passphrase);
        await Page.Locator("#e2e-confirm").ClickAsync();
        await CloseFinishedDialogAsync();
        await Expect(Page.Locator("#e2e-app-profiles .e2e-profile-state").First).ToHaveTextAsync("Unlocked");
    }

    [TestMethod]
    public async Task GivenAPassphrase_WhenReloaded_ThenLockedWithNoBanner()
    {
        await LoginAsync();
        await SetPassphraseAsync();

        await ReloadAsync();

        await Expect(Page.Locator("#e2e-banner")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#e2e-lock-indicator")).ToHaveCountAsync(0);
        await Page.Locator("#nav-settings").ClickAsync();
        await Expect(Page.Locator("#e2e-app-profiles .e2e-profile-state").First).ToHaveTextAsync("Locked");
    }

    [TestMethod]
    public async Task GivenNoProtectedProfiles_WhenOneIsCreated_ThenItIsListedByIdAndCanBeDeleted()
    {
        await LoginAsync();
        await Page.Locator("#nav-settings").ClickAsync();

        await Page.Locator("#e2e-new").ClickAsync();
        await EnterNewPassphraseAsync();
        await Expect(Page.Locator("#e2e-finished")).ToContainTextAsync("Created ");
        await ScreenshotAsync("Created");
        await CloseFinishedDialogAsync();

        var profile = Page.Locator("#e2e-created-profiles .e2e-profile");
        await Expect(profile).ToHaveCountAsync(1);
        var id = await profile.GetAttributeAsync("data-profile");
        Assert.IsTrue(Guid.TryParseExact(id, "D", out _), $"'{id}' is not a GUID");
        await Expect(profile.Locator(".e2e-profile-name")).ToHaveTextAsync(id!);
        await Expect(profile.Locator(".e2e-profile-state")).ToHaveTextAsync("Unlocked");
        await Expect(Page.Locator("#e2e-lock-indicator")).ToContainTextAsync("1 unlocked");

        await profile.Locator(".e2e-delete").ClickAsync();
        await Page.Locator("#e2e-delete-confirmation").FillAsync("not the id");
        await Page.Locator("#e2e-confirm").ClickAsync();
        await Expect(Page.Locator("#e2e-error")).ToHaveTextAsync("Type the id exactly to confirm.");
        await Page.Locator("#e2e-delete-confirmation").FillAsync(id!);
        await Page.Locator("#e2e-confirm").ClickAsync();
        await CloseFinishedDialogAsync();
        await Expect(Page.Locator("#e2e-no-created")).ToBeVisibleAsync();
        await Expect(Page.Locator("#e2e-lock-indicator")).ToHaveCountAsync(0);
    }

    [TestMethod]
    public async Task GivenAProtectedProfile_WhenReloaded_ThenItIsStillListedAndLocked()
    {
        await LoginAsync();
        await Page.Locator("#nav-settings").ClickAsync();
        await Page.Locator("#e2e-new").ClickAsync();
        await EnterNewPassphraseAsync();
        await CloseFinishedDialogAsync();

        await ReloadAsync();

        var profile = Page.Locator("#e2e-created-profiles .e2e-profile");
        await Expect(profile).ToHaveCountAsync(1);
        await Expect(profile.Locator(".e2e-profile-state")).ToHaveTextAsync("Locked");
    }

    [TestMethod]
    public async Task GivenArgon2idConfigured_WhenOpeningSettings_ThenArgon2idPanelShowsTheConfiguredSettings()
    {
        // The tests serve the portal the lowest Argon2id costs (TestBase).
        await LoginAsync();
        await Page.Locator("#nav-settings").ClickAsync();

        await Expect(Page.Locator("#argon2id-card")).ToBeVisibleAsync();
        await Expect(Page.Locator("#argon2id-memory")).ToHaveTextAsync($"{TestArgon2idMemoryKib} KiB");
        await Expect(Page.Locator("#argon2id-iterations")).ToHaveTextAsync($"{TestArgon2idIterations}");
        await Expect(Page.Locator("#argon2id-parallelism")).ToHaveTextAsync("1");
        await Expect(Page.Locator("#argon2id-key-length")).ToHaveTextAsync("32 bytes");
        await Expect(Page.Locator("#argon2id-benchmark")).ToBeEnabledAsync();
        await ScreenshotAsync("Argon2idPanel");
    }
}
