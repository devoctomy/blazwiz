namespace MyApp.Portal.UITests;

/// <summary>
/// Notebooks: the notes profile is the default notebook, and each notebook the user creates is a protected profile
/// with its own passphrase, keys and pages.
/// </summary>
[TestClass]
public class NotebooksTests : TestBase
{
    [TestMethod]
    public async Task GivenNoPassphrase_WhenOpeningNotebooks_ThenTheDefaultNotebookIsListedNeedingOne()
    {
        await LoginAsync();

        await Page.Locator("#nav-notebooks").ClickAsync();

        var notes = Notebook("notes");
        await Expect(notes.Locator(".notebook-title")).ToHaveTextAsync("Notes");
        await Expect(notes.Locator(".notebook-state")).ToHaveTextAsync("No passphrase yet");
        await Expect(Page.Locator("#notebooks .notebook")).ToHaveCountAsync(1);
        await ScreenshotAsync("Notebooks");
    }

    [TestMethod]
    public async Task GivenANewNotebook_WhenCreated_ThenItOpensWithItsOwnPages()
    {
        await LoginAsync();
        await SetPassphraseAsync();

        await CreateNotebookAsync("Work");
        await Expect(Page.Locator("#files-empty")).ToBeVisibleAsync();
        await AddPageAsync("Plan", "work plan");

        await Page.Locator("#nav-notebooks").ClickAsync();
        var work = Page.Locator("#notebooks .notebook", new() { HasText = "Work" });
        await Expect(work.Locator(".notebook-state")).ToHaveTextAsync("Unlocked");
        await ScreenshotAsync("TwoNotebooks");
        await OpenNotesAsync();
        await Expect(Row("Welcome.md")).ToBeVisibleAsync();
        await Expect(Row("Plan.md")).ToHaveCountAsync(0);
    }

    [TestMethod]
    public async Task GivenANotebookAfterAPasswordSignIn_WhenReloaded_ThenItIsLockedAndShownById_AndItsNameWasNotKept()
    {
        // Names are kept on the device only encrypted with the key derived from the passkey; signed in with a password
        // there is no such key, so the name lasts only until the page is closed.
        await LoginAsync();
        await CreateNotebookAsync("Work");
        await AddPageAsync("Plan", "work plan");
        await Page.Locator("#nav-settings").ClickAsync();
        await Expect(Page.Locator("#e2e-created-profiles .e2e-profile-name")).ToHaveTextAsync("Work");
        var kept = await Page.EvaluateAsync<int>("() => Object.keys(localStorage).filter(k => k.includes('e2e_labels_')).length");
        Assert.AreEqual(0, kept);

        await ReloadAsync();
        await Page.Locator("#nav-notebooks").ClickAsync();

        var created = Created();
        var id = await created.GetAttributeAsync("data-notebook");
        await Expect(created.Locator(".notebook-title")).ToHaveTextAsync(id!);
        await Expect(created.Locator(".notebook-state")).ToHaveTextAsync("Locked");
    }

    [TestMethod]
    public async Task GivenADeviceThatNeverUnlockedIt_WhenListed_ThenItIsShownByIdUntilUnlocked()
    {
        await LoginAsync();
        await CreateNotebookAsync("Work");
        await AddPageAsync("Plan", "work plan");

        // As on a new device: the name is only kept on the device where the notebook was unlocked.
        await Page.EvaluateAsync("() => Object.keys(localStorage).filter(k => k.includes('e2e_labels_')).forEach(k => localStorage.removeItem(k))");
        await ReloadAsync();
        await Page.Locator("#nav-notebooks").ClickAsync();
        var created = Created();
        var id = await created.GetAttributeAsync("data-notebook");
        Assert.IsTrue(Guid.TryParseExact(id, "D", out _), $"'{id}' is not a GUID");
        await Expect(created.Locator(".notebook-title")).ToHaveTextAsync(id!);

        await created.Locator(".notebook-open").ClickAsync();
        await Expect(Page.Locator("#e2e-unlock-message")).ToHaveTextAsync($"{id} is locked. Enter its passphrase to use it.");
        await UnlockAsync();
        await Expect(Page.Locator("h1.page-title")).ToContainTextAsync("Work");
        await Row("Plan.md").Locator(".file-name").ClickAsync();
        await Expect(Page.Locator("#note-editor")).ToHaveValueAsync("work plan");
        await Page.Locator("#nav-settings").ClickAsync();
        await Expect(Page.Locator("#e2e-created-profiles .e2e-profile-name")).ToHaveTextAsync("Work");
    }

    [TestMethod]
    public async Task GivenAnOpenNotebook_WhenLocked_ThenItIsLockedAndTheNotebooksAreShown()
    {
        await LoginAsync();
        await CreateNotebookAsync("Work");

        await Page.Locator("#lock-notebook-button").ClickAsync();

        await Expect(Page.Locator("h1.page-title")).ToContainTextAsync("Notebooks");
        await Expect(Created().Locator(".notebook-state")).ToHaveTextAsync("Locked");
        await Expect(Created().Locator(".notebook-title")).ToHaveTextAsync("Work");
        await Expect(Page.Locator("#e2e-lock-indicator")).ToHaveCountAsync(0);
    }

    [TestMethod]
    public async Task GivenARememberedUserWithAPasskey_WhenBiometricsAreTurnedOnForANotebook_ThenItOpensWithThemFromTheNotebooksPage_UntilForgotten()
    {
        // Arrange: Chromium's virtual authenticator stands in for the device's passkey, with the PRF extension the
        // keys are derived from.
        await AddVirtualAuthenticatorAsync();
        await LoginRememberedWithPasskeyAsync();
        await CreateNotebookAsync("Work");
        var work = Created();

        // Turned on when unlocking with the passphrase.
        await Page.Locator("#lock-notebook-button").ClickAsync();
        await work.Locator(".notebook-open").ClickAsync();
        await Page.Locator("#e2e-unlock-passphrase").FillAsync(Passphrase);
        await Page.Locator("#e2e-unlock-remember-biometrics").CheckAsync();
        await Page.Locator("#e2e-unlock-button").ClickAsync();
        await Expect(Page.Locator("#e2e-unlock-panel")).ToHaveCountAsync(0);

        // Locked, the notebook shows a grey fingerprint, and opens straight onto its pages.
        await Page.Locator("#lock-notebook-button").ClickAsync();
        await Expect(work.Locator(".notebook-icon.bi-fingerprint")).ToBeVisibleAsync();
        await Expect(work.Locator(".notebook-forget-biometrics")).ToBeVisibleAsync();
        await ScreenshotAsync("BiometricNotebook");
        await work.Locator(".notebook-open").ClickAsync();
        await Expect(Page.Locator("h1.page-title")).ToContainTextAsync("Work");
        await Expect(Page.Locator("#e2e-unlock-panel")).ToHaveCountAsync(0);

        // Forgetting asks first; cancelled, nothing changes.
        await Page.Locator("#lock-notebook-button").ClickAsync();
        await work.Locator(".notebook-forget-biometrics").ClickAsync();
        await Expect(Page.Locator("#forget-biometrics-dialog")).ToContainTextAsync("Work");
        await ScreenshotAsync("ForgetBiometrics");
        await Page.Locator("#forget-biometrics-dialog").GetByRole(AriaRole.Button, new() { Name = "Cancel" }).ClickAsync();
        await Expect(Page.Locator("#forget-biometrics-dialog")).ToHaveCountAsync(0);
        await Expect(work.Locator(".notebook-icon.bi-fingerprint")).ToBeVisibleAsync();

        // Act: forgotten.
        await work.Locator(".notebook-forget-biometrics").ClickAsync();
        await Page.Locator("#forget-biometrics-confirm").ClickAsync();

        // Assert: back to the passphrase.
        await Expect(work.Locator(".notebook-icon.bi-fingerprint")).ToHaveCountAsync(0);
        await Expect(work.Locator(".notebook-forget-biometrics")).ToHaveCountAsync(0);
        await work.Locator(".notebook-open").ClickAsync();
        await Expect(Page.Locator("#e2e-unlock-panel")).ToBeVisibleAsync();
        await Expect(Page.Locator("#e2e-unlock-passkey-button")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#e2e-unlock-remember-biometrics")).ToBeVisibleAsync();
    }

    [TestMethod]
    public async Task GivenAPasswordSignInWithoutRememberMe_WhenANotebookIsLocked_ThenBiometricsAreNotOffered()
    {
        // Arrange
        await LoginAsync();
        await CreateNotebookAsync("Work");

        // Act
        await Page.Locator("#lock-notebook-button").ClickAsync();

        // Assert
        await Expect(Created().Locator(".notebook-icon.bi-lock-fill")).ToBeVisibleAsync();
        await Expect(Created().Locator(".notebook-forget-biometrics")).ToHaveCountAsync(0);
    }

    private ILocator Created() => Page.Locator("#notebooks .notebook:not([data-notebook='notes'])");

    private async Task AddVirtualAuthenticatorAsync()
    {
        var cdp = await Context.NewCDPSessionAsync(Page);
        await cdp.SendAsync("WebAuthn.enable");
        await cdp.SendAsync("WebAuthn.addVirtualAuthenticator", new Dictionary<string, object>
        {
            ["options"] = new Dictionary<string, object>
            {
                ["protocol"] = "ctap2",
                ["transport"] = "internal",
                ["hasResidentKey"] = true,
                ["hasUserVerification"] = true,
                ["isUserVerified"] = true,
                ["hasPrf"] = true,
                ["automaticPresenceSimulation"] = true,
            },
        });
    }

    private async Task LoginRememberedWithPasskeyAsync()
    {
        await GoToLoginAsync();
        await Page.Locator("#username").FillAsync("bob.hoskins@example.com");
        await Page.Locator("#password").FillAsync("password123");
        await Page.Locator("#remember-me").CheckAsync();
        await Page.Locator("#login-button").ClickAsync();
        await Page.Locator("#webauthn-enable").ClickAsync();
        await Expect(Page.Locator("#dashboard-greeting")).ToBeVisibleAsync();
    }

    private ILocator Notebook(string name) => Page.Locator($"#notebooks .notebook[data-notebook='{name}']");

    private ILocator Row(string name) => Page.Locator($"#file-tree .file-row[data-name='{name}']");

    private async Task CreateNotebookAsync(string title)
    {
        await Page.Locator("#nav-notebooks").ClickAsync();
        await Page.Locator("#new-notebook-button").ClickAsync();
        await Page.Locator("#notebook-title").FillAsync(title);
        await Page.Locator("#notebook-passphrase").FillAsync(Passphrase);
        await Page.Locator("#notebook-confirm-passphrase").FillAsync(Passphrase);
        await Page.Locator("#notebook-acknowledge").CheckAsync();
        await Page.Locator("#notebook-create").ClickAsync();
        await Expect(Page.Locator("h1.page-title")).ToContainTextAsync(title);
    }

    private async Task AddPageAsync(string name, string content)
    {
        await Page.Locator("#new-note-button").ClickAsync();
        await Page.Locator("#dialog-name").FillAsync(name);
        await Page.Keyboard.PressAsync("Enter");
        await Page.Locator("#note-editor").FillAsync(content);
        await Page.Locator("#save-note-button").ClickAsync();
        await Expect(Page.Locator("#editor-status")).ToContainTextAsync("Saved at");
        await Page.Locator("#close-note-button").ClickAsync();
        await Expect(Row($"{name}.md")).ToBeVisibleAsync();
    }
}
