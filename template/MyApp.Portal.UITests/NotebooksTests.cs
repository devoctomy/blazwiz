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

    private ILocator Created() => Page.Locator("#notebooks .notebook:not([data-notebook='notes'])");

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
