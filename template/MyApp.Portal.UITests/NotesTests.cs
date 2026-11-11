using System.Text.RegularExpressions;

namespace MyApp.Portal.UITests;

[TestClass]
public class NotesTests : TestBase
{
    [TestMethod]
    public async Task GivenNewUser_WhenOpeningFiles_ThenWelcomeNoteCanBeOpened()
    {
        await OpenFilesAsync();

        await Row("Welcome.md").Locator(".file-name").ClickAsync();

        await Expect(Page.Locator("h1.page-title")).ToContainTextAsync("Welcome.md");
        await Expect(Page.Locator("#note-editor")).ToHaveValueAsync(new Regex("^# Welcome to your notes"));
        await Expect(Page.Locator("#note-preview h1")).ToHaveTextAsync("Welcome to your notes");
        await ScreenshotAsync("WelcomeNote");
    }

    [TestMethod]
    public async Task GivenFolder_WhenCreatingFormattingAndSavingNote_ThenNoteIsSavedInFolder()
    {
        await OpenFilesAsync();

        await Page.Locator("#new-folder-button").ClickAsync();
        await Page.Locator("#dialog-name").FillAsync("Work");
        await Page.Locator("#dialog-confirm").ClickAsync();
        await Expect(Page.Locator("#selected-folder-name")).ToHaveTextAsync("Work");

        await Page.Locator("#new-note-button").ClickAsync();
        await Page.Locator("#dialog-name").FillAsync("Plan");
        await Page.Keyboard.PressAsync("Enter");

        await Expect(Page.Locator("h1.page-title")).ToContainTextAsync("Plan.md");
        await Expect(Page.Locator(".page-subtitle")).ToHaveTextAsync("All notes / Work");
        var editor = Page.Locator("#note-editor");
        await editor.FillAsync("ship it");
        await editor.SelectTextAsync();
        await Page.Locator("#format-bold").ClickAsync();
        await Expect(editor).ToHaveValueAsync("**ship it**");
        await Expect(Page.Locator("#note-preview strong")).ToHaveTextAsync("ship it");
        await Expect(Page.Locator("#editor-status")).ToContainTextAsync("Unsaved changes");

        await Page.Locator("#save-note-button").ClickAsync();
        await Expect(Page.Locator("#editor-status")).ToContainTextAsync("Saved at");
        await ScreenshotAsync("Editor");

        // Reload to prove it was stored, not just kept in memory.
        await ReloadAsync();
        await UnlockAsync();
        await Expect(Page.Locator("#note-editor")).ToHaveValueAsync("**ship it**");

        await Page.Locator("#close-note-button").ClickAsync();
        await Row("Work").Locator(".file-name").ClickAsync();
        await Expect(Row("Plan.md")).ToHaveAttributeAsync("style", new Regex("--depth: 1"));
    }

    [TestMethod]
    public async Task GivenNoteAndFolder_WhenDraggingNoteOntoFolder_ThenNoteMovesIntoFolder()
    {
        await OpenFilesAsync();
        await Page.Locator("#new-folder-button").ClickAsync();
        await Page.Locator("#dialog-name").FillAsync("Archive");
        await Page.Locator("#dialog-confirm").ClickAsync();
        await Expect(Row("Archive")).ToBeVisibleAsync();

        await Row("Welcome.md").DragToAsync(Row("Archive"));

        // The destination folder is expanded to show the moved note.
        await Expect(Row("Archive").Locator(".file-count")).ToHaveTextAsync("1");
        await Expect(Row("Welcome.md")).ToHaveAttributeAsync("style", new Regex("--depth: 1"));

        // And back to the top level.
        await Row("Welcome.md").DragToAsync(Page.Locator("#files-root"));
        await Expect(Row("Archive").Locator(".file-count")).ToHaveTextAsync("0");
        await Expect(Row("Welcome.md")).ToHaveAttributeAsync("style", new Regex("--depth: 0"));
    }

    [TestMethod]
    public async Task GivenNote_WhenRenamingMovingAndDeleting_ThenFilesUpdate()
    {
        await OpenFilesAsync();
        await Page.Locator("#new-folder-button").ClickAsync();
        await Page.Locator("#dialog-name").FillAsync("Docs");
        await Page.Locator("#dialog-confirm").ClickAsync();
        await Expect(Row("Docs")).ToBeVisibleAsync();

        var welcome = Row("Welcome.md");
        await welcome.HoverAsync();
        await welcome.Locator(".file-action-rename").ClickAsync();
        await Expect(Page.Locator("#dialog-name")).ToHaveValueAsync("Welcome");
        await Page.Locator("#dialog-name").FillAsync("Hello");
        await Page.Locator("#dialog-confirm").ClickAsync();
        await Expect(Row("Hello.md")).ToBeVisibleAsync();

        await Row("Hello.md").HoverAsync();
        await Row("Hello.md").Locator(".file-action-move").ClickAsync();
        await Page.Locator("#move-dialog .list-group-item[data-name='Docs']").ClickAsync();
        await ScreenshotAsync("MoveDialog");
        await Page.Locator("#dialog-confirm").ClickAsync();
        await Expect(Row("Hello.md")).ToHaveAttributeAsync("style", new Regex("--depth: 1"));

        await Row("Docs").HoverAsync();
        await Row("Docs").Locator(".file-action-delete").ClickAsync();
        await Expect(Page.Locator("#delete-dialog")).ToContainTextAsync("1 item");
        await Page.Locator("#dialog-confirm").ClickAsync();
        await Expect(Page.Locator("#files-empty")).ToBeVisibleAsync();
    }

    [TestMethod]
    public async Task GivenDuplicateName_WhenCreatingNote_ThenErrorIsShownInDialog()
    {
        await OpenFilesAsync();

        await Page.Locator("#new-note-button").ClickAsync();
        await Page.Locator("#dialog-name").FillAsync("welcome");
        await Page.Locator("#dialog-confirm").ClickAsync();

        await Expect(Page.Locator("#dialog-error")).ToContainTextAsync("already something called 'welcome.md'");
        await Page.Keyboard.PressAsync("Escape");
        await Expect(Page.Locator("#name-dialog")).Not.ToBeVisibleAsync();
    }

    [TestMethod]
    public async Task GivenUnsavedChanges_WhenNavigatingAway_ThenAskedWhatToDo()
    {
        await OpenFilesAsync();
        await Row("Welcome.md").Locator(".file-name").ClickAsync();
        await Page.Locator("#note-editor").FillAsync("changed");

        await Page.Locator("#nav-settings").ClickAsync();
        await Expect(Page.Locator("#leave-dialog")).ToBeVisibleAsync();
        await ScreenshotAsync("UnsavedChanges");

        await Page.Locator("#leave-stay").ClickAsync();
        await Expect(Page.Locator("#leave-dialog")).Not.ToBeVisibleAsync();
        await Expect(Page.Locator("#note-editor")).ToHaveValueAsync("changed");

        await Page.Locator("#nav-settings").ClickAsync();
        await Page.Locator("#leave-discard").ClickAsync();
        await Expect(Page.Locator("h1.page-title")).ToContainTextAsync("Settings");
    }

    [TestMethod]
    public async Task GivenEditor_WhenTogglingPreview_ThenPreviewIsHiddenAndShown()
    {
        await OpenFilesAsync();
        await Row("Welcome.md").Locator(".file-name").ClickAsync();
        await Expect(Page.Locator("#note-preview")).ToBeVisibleAsync();

        await Page.Locator("#toggle-preview-button").ClickAsync();
        await Expect(Page.Locator("#note-preview")).Not.ToBeVisibleAsync();

        await Page.Locator("#toggle-preview-button").ClickAsync();
        await Expect(Page.Locator("#note-preview")).ToBeVisibleAsync();
    }

    [TestMethod]
    public async Task GivenNoPassphrase_WhenOpeningFiles_ThenThePanelPointsToSettings()
    {
        await LoginAsync();
        await OpenNotesAsync();

        await Expect(Page.Locator("#e2e-unlock-message")).ToHaveTextAsync("Notes needs a passphrase before it can be used. Set one in Settings.");
        await Expect(Page.Locator("#new-note-button")).ToHaveCountAsync(0);
        await ScreenshotAsync("NeedsPassphrase");
        await Page.Locator("#e2e-unlock-settings").ClickAsync();
        await Expect(Page.Locator("#e2e-section")).ToBeVisibleAsync();
    }

    [TestMethod]
    public async Task GivenNotesLocked_WhenOpeningFiles_ThenThePanelUnlocksThem()
    {
        await LoginAsync();
        await SetPassphraseAsync();
        await Page.Locator("#e2e-lock-indicator").ClickAsync();
        await Page.Locator("#e2e-lock-menu-lock-all").ClickAsync();

        await OpenNotesAsync();
        await Expect(Page.Locator("#e2e-unlock-message")).ToHaveTextAsync("Notes is locked. Enter its passphrase to use it.");
        await ScreenshotAsync("Locked");
        await UnlockAsync();

        await Expect(Row("Welcome.md")).ToBeVisibleAsync();
        await Expect(Page.Locator("#new-note-button")).ToBeVisibleAsync();
    }

    [TestMethod]
    public async Task GivenNotes_WhenThePassphraseIsChanged_ThenWarnedSignedOutAndOnlyTheNewPassphraseReadsThem()
    {
        const string newPassphrase = "a brand new passphrase";
        await OpenFilesAsync();
        await Page.Locator("#nav-settings").ClickAsync();
        await Page.Locator("#e2e-app-profiles .e2e-change").First.ClickAsync();
        await Expect(Page.Locator("#e2e-form")).ToContainTextAsync("Every device, this one included, is signed out");
        await Page.Locator("#e2e-current-passphrase").FillAsync(Passphrase);
        await Page.Locator("#e2e-passphrase").FillAsync(newPassphrase);
        await Page.Locator("#e2e-confirm-passphrase").FillAsync(newPassphrase);
        await Page.Locator("#e2e-acknowledge").CheckAsync();
        await Page.Locator("#e2e-confirm").ClickAsync();

        await Expect(Page.Locator("#e2e-rotation-estimate")).ToContainTextAsync("Files to encrypt again and upload: ");
        await Expect(Page.Locator("#e2e-rotation-warning")).ToContainTextAsync("This cannot be skipped or stopped");
        await ScreenshotAsync("Estimate");
        await Page.Locator("#e2e-rotation-start").ClickAsync();
        await ExpectLoginPageAsync();

        await LoginAsync();
        await OpenNotesAsync();
        await Page.Locator("#e2e-unlock-passphrase").FillAsync(Passphrase);
        await Page.Locator("#e2e-unlock-button").ClickAsync();
        await Expect(Page.Locator("#e2e-unlock-error")).ToHaveTextAsync("That passphrase is not correct. Try again.");
        await Page.Locator("#e2e-unlock-passphrase").FillAsync(newPassphrase);
        await Page.Locator("#e2e-unlock-button").ClickAsync();
        await Expect(Page.Locator("#e2e-unlock-panel")).ToHaveCountAsync(0);
        await Row("Welcome.md").Locator(".file-name").ClickAsync();
        await Expect(Page.Locator("#note-editor")).ToHaveValueAsync(new Regex("^# Welcome to your notes"));
    }

    [TestMethod]
    public async Task GivenAPassphraseChangeCutShort_WhenNotesAreUnlocked_ThenItMustBeFinishedFirst()
    {
        await OpenFilesAsync();

        // A change cut short leaves its mark in the keys document, which local development keeps in the browser.
        await Page.EvaluateAsync(
            """
            () => {
                for (let i = 0; i < localStorage.length; i++) {
                    const key = localStorage.key(i);
                    if (key.includes('e2e_browser_keys_')) {
                        const entry = JSON.parse(localStorage.getItem(key));
                        entry.Value = entry.Value.replace('"rotation_in_progress":false', '"rotation_in_progress":true');
                        entry.Version++;
                        localStorage.setItem(key, JSON.stringify(entry));
                    }
                }
            }
            """);
        await ReloadAsync();
        await OpenNotesAsync();
        await Page.Locator("#e2e-unlock-passphrase").FillAsync(Passphrase);
        await Page.Locator("#e2e-unlock-button").ClickAsync();

        await Expect(Page.Locator("#e2e-unlock-message")).ToHaveTextAsync("A passphrase change of Notes was interrupted. It must be finished before Notes can be used, and it must not be interrupted again.");
        await Expect(Page.Locator("#e2e-rotation-warning")).ToBeVisibleAsync();
        await Expect(Row("Welcome.md")).ToHaveCountAsync(0);
        await ScreenshotAsync("Interrupted");
        await Page.Locator("#e2e-rotation-finish").ClickAsync();
        await ExpectLoginPageAsync();

        await LoginAsync();
        await OpenNotesAsync();
        await UnlockAsync();
        await Expect(Row("Welcome.md")).ToBeVisibleAsync();
    }

    private ILocator Row(string name) => Page.Locator($"#file-tree .file-row[data-name='{name}']");

    private async Task OpenFilesAsync()
    {
        await LoginAsync();
        await SetPassphraseAsync();
        await OpenNotesAsync();
        await Expect(Row("Welcome.md")).ToBeVisibleAsync();
    }
}
