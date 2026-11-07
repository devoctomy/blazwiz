using System.Text.RegularExpressions;

namespace MyApp.Portal.UITests;

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

//#if (IncludeNotes)
    [TestMethod]
    public async Task GivenNoCloud_WhenEditingNotes_ThenTheyAreSavedAndThePendingCountGoesUp()
    {
        await LoginAsync();
        await SetPassphraseAsync();
        await OpenNotesAsync();
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
        await ReloadAsync();
        await UnlockAsync();
        await Expect(Page.Locator("#note-editor")).ToHaveValueAsync("written with no cloud");
    }
//#endif

    [TestMethod]
    public async Task GivenNoCloud_WhenSyncSwitchedOff_ThenPillSaysSyncOffAndItSurvivesAReload()
    {
        await LoginAsync();
        var pill = Page.Locator("#sync-status-pill");
        await pill.ClickAsync();

        await Page.Locator("#sync-enabled-switch").UncheckAsync();
        await Expect(pill).ToHaveAttributeAsync("title", new Regex("Sync is turned off"));
        await Expect(pill).ToContainTextAsync("Sync off");

        await ReloadAsync();
        await Expect(Page.Locator("#sync-status-pill")).ToHaveAttributeAsync("title", new Regex("Sync is turned off"));
        await Page.Locator("#sync-status-pill").ClickAsync();
        await Expect(Page.Locator("#sync-enabled-switch")).Not.ToBeCheckedAsync();
    }

    [TestMethod]
    public async Task GivenSyncOff_WhenSigningOutAndBackIn_ThenAdvancedOptionsShowItAndUntickingTurnsSyncOn()
    {
        await LoginAsync();
        await Page.Locator("#sync-status-pill").ClickAsync();
        await Page.Locator("#sync-enabled-switch").UncheckAsync();
        await Expect(Page.Locator("#sync-status-pill")).ToHaveAttributeAsync("title", new Regex("Sync is turned off"));
        await Page.Locator(".sync-popover-backdrop").ClickAsync();

        await Page.Locator("#logout-button").ClickAsync();
        await ExpectLoginPageAsync();
        await Expect(Page.Locator("#advanced-options")).ToBeVisibleAsync();
        await Expect(Page.Locator("#start-sync-off")).ToBeCheckedAsync();
        await ScreenshotAsync("AdvancedOptions");

        await Page.Locator("#start-sync-off").UncheckAsync();
        await Page.Locator("#username").FillAsync("bob.hoskins@example.com");
        await Page.Locator("#password").FillAsync("password123");
        await Page.Locator("#login-button").ClickAsync();
        await Expect(Page.Locator("#dashboard-greeting")).ToBeVisibleAsync();

        await Expect(Page.Locator("#sync-status-pill")).ToHaveAttributeAsync("title", new Regex("Offline: no credentials"));
    }

    [TestMethod]
    public async Task GivenSyncOn_WhenOpeningLogin_ThenAdvancedOptionsCollapsed()
    {
        await GoToLoginAsync();

        await Expect(Page.Locator("#advanced-options-toggle")).ToBeVisibleAsync();
        await Expect(Page.Locator("#advanced-options")).Not.ToBeVisibleAsync();

        await Page.Locator("#advanced-options-toggle").ClickAsync();
        await Expect(Page.Locator("#start-sync-off")).Not.ToBeCheckedAsync();
    }

    [TestMethod]
    public async Task GivenSignedIn_WhenOfflineModeTurnedOnInSettings_ThenBannerOfflineCountdownAndNoSync()
    {
        await LoginAsync();
        await Page.Locator("#nav-settings").ClickAsync();

        await Page.Locator("#offline-mode-switch").CheckAsync();

        await Expect(Page.Locator("#offline-banner")).ToBeVisibleAsync();
        await Expect(Page.Locator("#offline-banner-text")).ToContainTextAsync("You're in offline mode");
        await Expect(Page.Locator("#session-timer")).ToHaveTextAsync(new Regex(@"^Offline 1[45]:\d{2}$"));
        await Expect(Page.Locator("#sync-status-pill")).ToHaveAttributeAsync("title", new Regex("Offline: offline mode"));
        await Expect(Page.Locator("#e2e-offline")).ToContainTextAsync("You're offline");
        await ScreenshotAsync("OfflineMode");
    }

    [TestMethod]
    public async Task GivenOfflineMode_WhenTurnedOff_ThenLoginPageIsShown()
    {
        await LoginAsync();
        await Page.Locator("#nav-settings").ClickAsync();
        await Page.Locator("#offline-mode-switch").CheckAsync();
        await Expect(Page.Locator("#offline-banner")).ToBeVisibleAsync();

        // A click, not UncheckAsync: the page leaves for the login page before the switch could be seen unticked.
        await Page.Locator("#offline-mode-switch").ClickAsync();

        await ExpectLoginPageAsync();
    }

    [TestMethod]
    public async Task GivenOfflineMode_WhenSignInOnlineClicked_ThenLoginPageIsShown()
    {
        await LoginAsync();
        await Page.Locator("#nav-settings").ClickAsync();
        await Page.Locator("#offline-mode-switch").CheckAsync();

        await Page.Locator("#offline-banner-sign-in").ClickAsync();

        await ExpectLoginPageAsync();
    }

    [TestMethod]
    public async Task GivenOfflineMode_WhenPageReloaded_ThenOfflineSessionIsGoneAndLoginPageIsShown()
    {
        await LoginAsync();
        await Page.Locator("#nav-settings").ClickAsync();
        await Page.Locator("#offline-mode-switch").CheckAsync();
        await Expect(Page.Locator("#offline-banner")).ToBeVisibleAsync();

        await ReloadAsync();

        await ExpectLoginPageAsync();
    }

    [TestMethod]
    public async Task GivenSyncPage_WhenOpened_ThenConnectivityEventsCanBeSearchedFilteredAndSurviveAReload()
    {
        await LoginAsync();

        // The first connectivity check runs once sync starts after sign in. Turning sync off before it finishes means it
        // never reports "no credentials", so wait for it (the event is logged before the status changes).
        await Expect(Page.Locator("#sync-status-pill")).ToHaveAttributeAsync("title", new Regex("Offline: no credentials"));
        await Page.Locator("#sync-status-pill").ClickAsync();
        await Page.Locator("#sync-enabled-switch").UncheckAsync();
        await Page.Locator(".sync-popover-backdrop").ClickAsync();

        await Page.Locator("#nav-sync").ClickAsync();
        await Expect(Page.Locator("h1.page-title")).ToContainTextAsync("Sync");

        var log = Page.Locator("#sync-log");
        await Expect(log).ToContainTextAsync("Offline: no credentials");
        await Expect(log).ToContainTextAsync("Sync turned off");
        await Expect(log.Locator(".bi-wifi-off").First).ToBeVisibleAsync();
        await Expect(log.Locator(".bi-airplane").First).ToBeVisibleAsync();
        await ScreenshotAsync("SyncLog");

        await Page.Locator("#sync-search").FillAsync("credentials");
        await Expect(log).ToContainTextAsync("Offline: no credentials");
        await Expect(log).Not.ToContainTextAsync("Sync turned off");
        await Page.Locator("#sync-search").FillAsync(string.Empty);

        await Page.Locator(".sync-filter[data-group='Connectivity']").ClickAsync();
        await Expect(log).ToContainTextAsync("Sync turned off");
        await Page.Locator(".sync-filter[data-group='Uploads']").ClickAsync();
        await Expect(Page.Locator("#sync-log-empty")).ToBeVisibleAsync();

        await ReloadAsync();
        await Expect(Page.Locator("#sync-log")).ToContainTextAsync("Sync turned off");
    }

//#if (IncludeNotes)
    [TestMethod]
    public async Task GivenNotes_WhenStorageSwitchedToOpfsAndBack_ThenNotesMoveWithNothingNewToSync()
    {
        await LoginAsync();
        await SetPassphraseAsync();
        await OpenNotesAsync();
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
        await Expect(Page.Locator("#storage-dialog-summary")).ToHaveTextAsync(new Regex(@"^\d+ files will be moved to OPFS on this device\.$"));
        await ScreenshotAsync("ConfirmMove");
        await Page.Locator("#storage-confirm").ClickAsync();
        await Expect(Page.Locator("#storage-dialog")).ToBeHiddenAsync();
        await Expect(Page.Locator("#storage-option-opfs")).ToHaveClassAsync(new Regex("active"));
        await Expect(Page.Locator("#storage-error")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#settings-pending")).ToHaveTextAsync("2");
        await ScreenshotAsync("MovedToOpfs");
        CollectionAssert.Contains(await ListOpfsFilesAsync(), "notes/Moving.md");

        // Still in OPFS, and still readable, after a reload.
        await ReloadAsync();
        await Expect(Page.Locator("#storage-option-opfs")).ToHaveClassAsync(new Regex("active"));
        await OpenNotesAsync();
        await UnlockAsync();
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
        await ReloadAsync();
        await OpenNotesAsync();
        await UnlockAsync();
        await Row("Moving.md").Locator(".file-name").ClickAsync();
        await Expect(Page.Locator("#note-editor")).ToHaveValueAsync("kept in OPFS");
    }
//#endif

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

//#if (IncludeNotes)
    [TestMethod]
    public async Task GivenPickedFolder_WhenStorageSwitchedToItAndAccessLapses_ThenNotesLiveThereAndReconnectRestoresThem()
    {
        await StubFolderPickerAsync();
        await LoginAsync();
        await SetPassphraseAsync();
        await OpenNotesAsync();
        await Expect(Row("Welcome.md")).ToBeVisibleAsync();
        await Page.Locator("#new-note-button").ClickAsync();
        await Page.Locator("#dialog-name").FillAsync("In a folder");
        await Page.Keyboard.PressAsync("Enter");
        await Page.Locator("#note-editor").FillAsync("visible on disk");
        await Page.Locator("#save-note-button").ClickAsync();
        await Expect(Page.Locator("#editor-status")).ToContainTextAsync("Saved at");

        // Picking the folder comes first, then the usual confirmation naming it.
        await Page.Locator("#nav-settings").ClickAsync();
        await Page.Locator("#storage-option-folder").ClickAsync();
        await Expect(Page.Locator("#storage-dialog-summary")).ToHaveTextAsync(new Regex(@"^\d+ files will be moved to File System \(picked-folder\) on this device\.$"));
        await Page.Locator("#storage-confirm").ClickAsync();
        await Expect(Page.Locator("#storage-option-folder")).ToHaveClassAsync(new Regex("active"));
        await Expect(Page.Locator("#storage-folder-name")).ToHaveTextAsync("Folder: picked-folder");
        await Expect(Page.Locator("#settings-pending")).ToHaveTextAsync("2");
        await ScreenshotAsync("MovedToFolder");
        var inFolder = await ListPickedFolderAsync();
        CollectionAssert.Contains(inFolder, "notes/In a folder.md");
        CollectionAssert.Contains(inFolder, "notes/Welcome.md");

        // The browser forgets the permission: the app asks for it back before syncing.
        await Page.EvaluateAsync("() => localStorage.setItem('test-folder-revoked', '1')");
        await ReloadAsync();
        await Expect(Page.Locator("#folder-access-banner")).ToContainTextAsync("picked-folder");
        await ScreenshotAsync("ReconnectBanner");

        // Reconnecting must not reload: some browsers (Chrome on Android) only keep the grant for the current page.
        await Page.EvaluateAsync("() => window.notReloaded = true");
        await Page.Locator("#folder-reconnect-button").ClickAsync();
        await Expect(Page.Locator("#folder-access-banner")).ToHaveCountAsync(0);
        Assert.IsTrue(await Page.EvaluateAsync<bool>("() => window.notReloaded === true"));
        await OpenNotesAsync();
        await UnlockAsync();
        await Row("In a folder.md").Locator(".file-name").ClickAsync();
        await Expect(Page.Locator("#note-editor")).ToHaveValueAsync("visible on disk");

        // Losing access while in the background (as phones do) shows the banner as soon as the app is back.
        await Page.EvaluateAsync("() => { localStorage.setItem('test-folder-revoked', '1'); document.dispatchEvent(new Event('visibilitychange')); }");
        await Expect(Page.Locator("#folder-access-banner")).ToBeVisibleAsync();

        // Using the notes meanwhile shows why they cannot be read, not Blazor's "unhandled error" bar.
        await Page.Locator("#close-note-button").ClickAsync();
        await Expect(Page.Locator("body")).ToContainTextAsync("Allow access to the folder 'picked-folder' to use your files.");
        await Expect(Page.Locator("#blazor-error-ui")).ToBeHiddenAsync();
        await Page.Locator("#folder-reconnect-button").ClickAsync();
        await Expect(Page.Locator("#folder-access-banner")).ToHaveCountAsync(0);
        await Row("In a folder.md").Locator(".file-name").ClickAsync();
        await Expect(Page.Locator("#note-editor")).ToHaveValueAsync("visible on disk");
        await Expect(Page.Locator("#blazor-error-ui")).ToBeHiddenAsync();
        Assert.IsTrue(await Page.EvaluateAsync<bool>("() => window.notReloaded === true"));

        // Moving back takes the notes out of the folder.
        await Page.Locator("#close-note-button").ClickAsync();
        await Page.Locator("#nav-settings").ClickAsync();
        await Page.Locator("#storage-option-indexeddb").ClickAsync();
        await Page.Locator("#storage-confirm").ClickAsync();
        await Expect(Page.Locator("#storage-option-indexeddb")).ToHaveClassAsync(new Regex("active"));
        Assert.AreEqual(0, (await ListPickedFolderAsync()).Count);
    }
//#endif

//#if (IncludeNotes)
    [TestMethod]
    public async Task GivenPickedFolderAlreadyHasNotes_WhenMovingThere_ThenRefusedAndTheirFilesKept()
    {
        await StubFolderPickerAsync();
        await LoginAsync();
        await Page.EvaluateAsync(@"async () => {
            const folder = await (await navigator.storage.getDirectory()).getDirectoryHandle('picked-folder', { create: true });
            const notes = await folder.getDirectoryHandle('notes', { create: true });
            const writable = await (await notes.getFileHandle('Mine.md', { create: true })).createWritable();
            await writable.write('my own note');
            await writable.close();
        }");

        await Page.Locator("#nav-settings").ClickAsync();
        await Page.Locator("#storage-option-folder").ClickAsync();
        await Page.Locator("#storage-confirm").ClickAsync();

        await Expect(Page.Locator("#storage-error")).ToContainTextAsync("The folder already has notes files in it");
        await Expect(Page.Locator("#storage-option-indexeddb")).ToHaveClassAsync(new Regex("active"));
        CollectionAssert.AreEqual(new[] { "notes/Mine.md" }, await ListPickedFolderAsync());
    }
//#endif

    [TestMethod]
    public async Task GivenTwoTabs_WhenOneMovesTheFiles_ThenOnlyTheOtherReloads()
    {
        await LoginAsync();
        var other = await Context.NewPageAsync();
        await GoToAsync(other);
        await Expect(other.Locator("#dashboard-greeting")).ToBeVisibleAsync();
        await other.EvaluateAsync("() => window.notReloaded = true");
        await Page.EvaluateAsync("() => window.notReloaded = true");

        await Page.Locator("#nav-settings").ClickAsync();
        await Page.Locator("#storage-option-opfs").ClickAsync();
        await Page.Locator("#storage-confirm").ClickAsync();
        await Expect(Page.Locator("#storage-option-opfs")).ToHaveClassAsync(new Regex("active"));

        await other.WaitForFunctionAsync("() => window.notReloaded === undefined");
        await Expect(other.Locator("#dashboard-greeting")).ToBeVisibleAsync();
        Assert.IsTrue(await Page.EvaluateAsync<bool>("() => window.notReloaded === true"));
    }

    private ILocator Row(string name) => Page.Locator($"#file-tree .file-row[data-name='{name}']");

    // Playwright cannot drive the real folder picker, so it hands back a real directory handle (an OPFS folder called
    // 'picked-folder'). Permission is granted unless the test sets 'test-folder-revoked' in localStorage, which asking
    // for permission again clears.
    private Task StubFolderPickerAsync()
    {
        return Page.AddInitScriptAsync(@"
            window.showDirectoryPicker = async () =>
                (await navigator.storage.getDirectory()).getDirectoryHandle('picked-folder', { create: true });
            FileSystemHandle.prototype.queryPermission = async () =>
                localStorage.getItem('test-folder-revoked') ? 'prompt' : 'granted';
            FileSystemHandle.prototype.requestPermission = async () => {
                localStorage.removeItem('test-folder-revoked');
                return 'granted';
            };");
    }

    private async Task<List<string>> ListPickedFolderAsync()
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

            await walk(await (await navigator.storage.getDirectory()).getDirectoryHandle('picked-folder', { create: true }), '');
            return found;
        }");
        return [.. files];
    }

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
                root = await top.getDirectoryHandle('myapp_notes');
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
