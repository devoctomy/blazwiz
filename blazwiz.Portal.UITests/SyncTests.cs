using System.Text.RegularExpressions;

namespace blazwiz.Portal.UITests;

/// <summary>
/// The UI tests use the placeholder sign-in with no AWS, so there is no cloud sync: these cover what the user sees
/// without it. Real syncing is covered by the Core integration tests against the real bucket.
/// </summary>
[TestClass]
public class SyncTests : TestBase
{
    [TestMethod]
    public async Task GivenNoCloud_WhenSignedIn_ThenPillSaysOfflineNoCredentialsAndSyncNowIsDisabled()
    {
        await LoginAsync();

        var pill = Page.Locator("#sync-status-pill");
        await Expect(pill).ToContainTextAsync("Offline");
        await Expect(pill).ToHaveAttributeAsync("title", new Regex("Offline: no credentials"));

        await pill.ClickAsync();
        await Expect(Page.Locator("#sync-popover-reason")).ToContainTextAsync("Offline: no credentials");
        await Expect(Page.Locator("#sync-now-button")).ToBeDisabledAsync();
        await ScreenshotAsync("StatusPopover");
    }

    [TestMethod]
    public async Task GivenNoCloud_WhenEditingNotes_ThenTheyAreSavedAndThePendingCountGoesUp()
    {
        await LoginAsync();
        await Page.Locator("#nav-files").ClickAsync();
        await Expect(Row("Welcome.md")).ToBeVisibleAsync();

        // The welcome note is a local change waiting to sync.
        var pill = Page.Locator("#sync-status-pill");
        await Expect(pill).ToContainTextAsync("1 pending");

        await Page.Locator("#new-note-button").ClickAsync();
        await Page.Locator("#dialog-name").FillAsync("Offline idea");
        await Page.Keyboard.PressAsync("Enter");
        await Page.Locator("#note-editor").FillAsync("written with no cloud");
        await Page.Locator("#save-note-button").ClickAsync();
        await Expect(Page.Locator("#editor-status")).ToContainTextAsync("Saved at");

        await Expect(pill).ToContainTextAsync("2 pending");

        // Stored locally, so still there after a reload.
        await Page.ReloadAsync();
        await Expect(Page.Locator("#note-editor")).ToHaveValueAsync("written with no cloud");
    }

    [TestMethod]
    public async Task GivenNoCloud_WhenOfflineModeSwitchedOn_ThenReasonIsOfflineModeAndItSurvivesAReload()
    {
        await LoginAsync();
        var pill = Page.Locator("#sync-status-pill");
        await pill.ClickAsync();

        await Page.Locator("#offline-mode-switch").CheckAsync();
        await Expect(pill).ToHaveAttributeAsync("title", new Regex("Offline: offline mode"));

        await Page.ReloadAsync();
        await Expect(Page.Locator("#sync-status-pill")).ToHaveAttributeAsync("title", new Regex("Offline: offline mode"));
        await Page.Locator("#sync-status-pill").ClickAsync();
        await Expect(Page.Locator("#offline-mode-switch")).ToBeCheckedAsync();
    }

    [TestMethod]
    public async Task GivenOfflineModeOn_WhenSigningOutAndBackIn_ThenAdvancedOptionsShowItAndUntickingTurnsItOff()
    {
        await LoginAsync();
        await Page.Locator("#sync-status-pill").ClickAsync();
        await Page.Locator("#offline-mode-switch").CheckAsync();
        await Expect(Page.Locator("#sync-status-pill")).ToHaveAttributeAsync("title", new Regex("offline mode"));
        await Page.Locator(".sync-popover-backdrop").ClickAsync();

        await Page.Locator("#logout-button").ClickAsync();
        await ExpectLoginPageAsync();
        await Expect(Page.Locator("#advanced-options")).ToBeVisibleAsync();
        await Expect(Page.Locator("#start-offline")).ToBeCheckedAsync();
        await ScreenshotAsync("AdvancedOptions");

        await Page.Locator("#start-offline").UncheckAsync();
        await Page.Locator("#username").FillAsync("bob.hoskins@example.com");
        await Page.Locator("#password").FillAsync("password123");
        await Page.Locator("#login-button").ClickAsync();
        await Expect(Page.Locator("#dashboard-greeting")).ToBeVisibleAsync();

        await Expect(Page.Locator("#sync-status-pill")).ToHaveAttributeAsync("title", new Regex("Offline: no credentials"));
    }

    [TestMethod]
    public async Task GivenNoOfflineMode_WhenOpeningLogin_ThenAdvancedOptionsCollapsed()
    {
        await GoToLoginAsync();

        await Expect(Page.Locator("#advanced-options-toggle")).ToBeVisibleAsync();
        await Expect(Page.Locator("#advanced-options")).Not.ToBeVisibleAsync();

        await Page.Locator("#advanced-options-toggle").ClickAsync();
        await Expect(Page.Locator("#start-offline")).Not.ToBeCheckedAsync();
    }

    [TestMethod]
    public async Task GivenSyncPage_WhenOpened_ThenConnectivityEventsCanBeSearchedFilteredAndSurviveAReload()
    {
        await LoginAsync();
        await Page.Locator("#sync-status-pill").ClickAsync();
        await Page.Locator("#offline-mode-switch").CheckAsync();
        await Page.Locator(".sync-popover-backdrop").ClickAsync();

        await Page.Locator("#nav-sync").ClickAsync();
        await Expect(Page.Locator("h1.page-title")).ToContainTextAsync("Sync");

        var log = Page.Locator("#sync-log");
        await Expect(log).ToContainTextAsync("Offline: no credentials");
        await Expect(log).ToContainTextAsync("Offline mode turned on");
        await Expect(log.Locator(".bi-wifi-off").First).ToBeVisibleAsync();
        await Expect(log.Locator(".bi-airplane").First).ToBeVisibleAsync();
        await ScreenshotAsync("SyncLog");

        await Page.Locator("#sync-search").FillAsync("credentials");
        await Expect(log).ToContainTextAsync("Offline: no credentials");
        await Expect(log).Not.ToContainTextAsync("Offline mode turned on");
        await Page.Locator("#sync-search").FillAsync(string.Empty);

        await Page.Locator(".sync-filter[data-group='Connectivity']").ClickAsync();
        await Expect(log).ToContainTextAsync("Offline mode turned on");
        await Page.Locator(".sync-filter[data-group='Uploads']").ClickAsync();
        await Expect(Page.Locator("#sync-log-empty")).ToBeVisibleAsync();

        await Page.ReloadAsync();
        await Expect(Page.Locator("#sync-log")).ToContainTextAsync("Offline mode turned on");
    }

    [TestMethod]
    public async Task GivenNotes_WhenStorageSwitchedToOpfsAndBack_ThenNotesMoveWithNothingNewToSync()
    {
        await LoginAsync();
        await Page.Locator("#nav-files").ClickAsync();
        await Expect(Row("Welcome.md")).ToBeVisibleAsync();
        await Page.Locator("#new-note-button").ClickAsync();
        await Page.Locator("#dialog-name").FillAsync("Moving");
        await Page.Keyboard.PressAsync("Enter");
        await Page.Locator("#note-editor").FillAsync("kept in OPFS");
        await Page.Locator("#save-note-button").ClickAsync();
        await Expect(Page.Locator("#editor-status")).ToContainTextAsync("Saved at");
        await Expect(Page.Locator("#sync-status-pill")).ToContainTextAsync("2 pending");

        // Move to OPFS.
        await Page.Locator("#nav-settings").ClickAsync();
        await Expect(Page.Locator("#storage-option-indexeddb")).ToHaveClassAsync(new Regex("active"));
        await Page.Locator("#storage-option-opfs").ClickAsync();
        await Expect(Page.Locator("#storage-dialog-summary")).ToHaveTextAsync(new Regex(@"^\d+ files will be moved to Browser file system \(OPFS\) on this device\.$"));
        await ScreenshotAsync("ConfirmMove");
        await Page.Locator("#storage-confirm").ClickAsync();
        await Expect(Page.Locator("#storage-dialog")).ToBeHiddenAsync();
        await Expect(Page.Locator("#storage-option-opfs")).ToHaveClassAsync(new Regex("active"));
        await Expect(Page.Locator("#storage-error")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#settings-pending")).ToHaveTextAsync("2");
        await ScreenshotAsync("MovedToOpfs");
        CollectionAssert.Contains(await ListOpfsFilesAsync(), "notes/Moving.md");

        // Still in OPFS, and still readable, after a reload.
        await Page.ReloadAsync();
        await Expect(Page.Locator("#storage-option-opfs")).ToHaveClassAsync(new Regex("active"));
        await Page.Locator("#nav-files").ClickAsync();
        await Row("Moving.md").Locator(".file-name").ClickAsync();
        await Expect(Page.Locator("#note-editor")).ToHaveValueAsync("kept in OPFS");
        await Expect(Page.Locator("#sync-status-pill")).ToContainTextAsync("2 pending");

        // And back to IndexedDB, which leaves OPFS empty.
        await Page.Locator("#close-note-button").ClickAsync();
        await Page.Locator("#nav-settings").ClickAsync();
        await Page.Locator("#storage-option-indexeddb").ClickAsync();
        await Page.Locator("#storage-confirm").ClickAsync();
        await Expect(Page.Locator("#storage-option-indexeddb")).ToHaveClassAsync(new Regex("active"));
        Assert.AreEqual(0, (await ListOpfsFilesAsync()).Count);
        await Page.ReloadAsync();
        await Page.Locator("#nav-files").ClickAsync();
        await Row("Moving.md").Locator(".file-name").ClickAsync();
        await Expect(Page.Locator("#note-editor")).ToHaveValueAsync("kept in OPFS");
    }

    [TestMethod]
    public async Task GivenStorageChoiceOpen_WhenCancelled_ThenNothingMoves()
    {
        await LoginAsync();
        await Page.Locator("#nav-settings").ClickAsync();
        await Page.Locator("#storage-option-opfs").ClickAsync();
        await Expect(Page.Locator("#storage-dialog")).ToBeVisibleAsync();

        await Page.Locator("#storage-dialog .btn-secondary").ClickAsync();

        await Expect(Page.Locator("#storage-dialog")).ToBeHiddenAsync();
        await Expect(Page.Locator("#storage-option-indexeddb")).ToHaveClassAsync(new Regex("active"));
        Assert.AreEqual(0, (await ListOpfsFilesAsync()).Count);
    }

    private ILocator Row(string name) => Page.Locator($"#file-tree .file-row[data-name='{name}']");

    // Every file under the notes OPFS root, as "<key>" relative to the user's directory.
    private async Task<List<string>> ListOpfsFilesAsync()
    {
        var files = await Page.EvaluateAsync<string[]>(@"async () => {
            const found = [];
            async function walk(directory, prefix) {
                for await (const [name, handle] of directory.entries()) {
                    if (handle.kind === 'directory') {
                        await walk(handle, prefix + name + '/');
                    } else {
                        found.push(prefix + name);
                    }
                }
            }

            const top = await navigator.storage.getDirectory();
            let root;
            try {
                root = await top.getDirectoryHandle('blazwiz_notes');
            } catch {
                return found;
            }

            for await (const [, owner] of root.entries()) {
                await walk(owner, '');
            }

            return found;
        }");
        return [.. files];
    }
}
