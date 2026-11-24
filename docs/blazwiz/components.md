# Reusable components

These are the components the framework gives you to use in your own pages. They live in two packages:

- `Devoctomy.Blazwiz.UI`, namespace `Devoctomy.Blazwiz.UI.Components` (already in the template's `_Imports.razor`).
- `Devoctomy.Blazwiz.UI.Sync`, namespace `Devoctomy.Blazwiz.UI.Sync.Components` (in `_Imports.razor` when the app
  has sync).

Some components are put in place by the framework itself (banners, the toast host, Settings sections). They are
listed at the end so you know what they are, and which you can remove.

All text these components show comes from resources, so you can reword or translate it
([Text and translation](text-and-translation.md)).

## Components for your pages

### ModalDialog

A themed Bootstrap modal dialog. Render it while the dialog should be open, and stop rendering it to close it:

```razor
@if (ViewModel.IsRenameDialogOpen)
{
    <ModalDialog Id="rename-dialog" Title="Rename" Icon="bi-pencil"
                 OnClose="() => ViewModel.CloseRenameDialogCommand.Execute(null)">
        <ChildContent>
            <input type="text" class="form-control" @bind="ViewModel.NewName" />
        </ChildContent>
        <Footer>
            <button type="button" class="btn btn-secondary" @onclick="() => ViewModel.CloseRenameDialogCommand.Execute(null)">Cancel</button>
            <button type="button" class="btn btn-primary" @onclick="() => ViewModel.RenameCommand.ExecuteAsync(null)">Rename</button>
        </Footer>
    </ModalDialog>
}
```

| Parameter | Default | What it is |
|---|---|---|
| `Title` | (required) | The dialog's title. |
| `Id` | `modal-dialog` | The element's id. The title's id is `<Id>-title`, which labels the dialog for screen readers. Give each dialog on a page its own. |
| `Icon` | none | A Bootstrap icon class shown before the title, e.g. `bi-folder`. |
| `SizeClass` | none | A Bootstrap modal size class, e.g. `modal-sm` or `modal-lg`. |
| `FocusMode` | `ModalDialogFocusMode.FirstInput` | What is focused when the dialog opens (below). |
| `ChildContent` | none | The body. |
| `Footer` | none | The footer, usually the action buttons. No footer is drawn without it. |
| `OnClose` | none | Called when the user presses Escape or the close (X) button. Set your view model's "open" flag to false here. |

#### What gets focus

`FocusMode` takes a `ModalDialogFocusMode` (in `Devoctomy.Blazwiz.UI.Components`):

| Mode | Focuses | If there is nothing to focus |
|---|---|---|
| `FirstInput` (default) | The first `input`, `select` or `textarea` in the dialog, with an input's text selected, so the user can type straight away. | The dialog itself. |
| `FirstButton` | The first button in the footer. | The dialog itself. |
| `Dialog` | The dialog itself. Nothing is triggered by Enter, and screen readers read its title. | (always works) |
| `Autofocus` | The first element in the dialog marked with the `autofocus` attribute. | The dialog itself. |
| `None` | Nothing: your code focuses what it wants. | (nothing is focused) |

Which to use:

- A form: the default, `FirstInput`.
- A confirmation with no inputs: `Dialog`, so a stray Enter doesn't confirm it. (`FirstInput` falls back to the dialog
  too.) Use `FirstButton` only if the first footer button is the safe choice, such as Cancel.
- A particular field or button: mark it `autofocus` and use `Autofocus`.

```razor
<ModalDialog Id="delete-dialog" Title="Delete this page?" Icon="bi-trash3"
             FocusMode="ModalDialogFocusMode.Autofocus"
             OnClose="() => ViewModel.CloseDialogCommand.Execute(null)">
    <ChildContent>It cannot be undone.</ChildContent>
    <Footer>
        <button type="button" class="btn btn-secondary" autofocus @onclick="() => ViewModel.CloseDialogCommand.Execute(null)">Cancel</button>
        <button type="button" class="btn btn-danger" @onclick="() => ViewModel.DeleteCommand.ExecuteAsync(null)">Delete</button>
    </Footer>
</ModalDialog>
```

> **With `None`, Escape only closes the dialog once focus is inside it**, because Escape is handled by the dialog
> element. Make sure your code moves focus into the dialog.

### PageHeader

The standard page heading, with an optional icon, subtitle and action buttons. It also sets the browser tab's title
to "`<Title> - <app title>`".

```razor
<PageHeader Title="Notebooks" Icon="bi-journals" Subtitle="Each notebook has its own passphrase.">
    <button type="button" class="btn btn-primary">New notebook</button>
</PageHeader>
```

| Parameter | What it is |
|---|---|
| `Title` | (required) The heading. |
| `Subtitle` | A line under it. |
| `Icon` | A Bootstrap icon class. |
| `ChildContent` | Content on the right, usually buttons. |

### ErrorAlert

A red alert with a warning icon, shown only when there is a message:

```razor
<ErrorAlert Message="@ViewModel.ErrorMessage" />
```

| Parameter | Default | What it is |
|---|---|---|
| `Message` | none | The error to show. Nothing is drawn when it is empty. Use `IErrorText.Describe(exception)` to make it. |
| `Id` | `form-error` | The element's id. |

### Toasts

Short messages that appear over the page and go away by themselves, e.g. "Copied". You don't render anything: the
shell already has the `ToastHost`. Inject `IToastService` into your view model:

```csharp
toastService.Show(text["Things.Saved"], ToastTone.Success);
```

- `Show(message, tone, duration)` shows one. `tone` is a `ToastTone`: `Info` (the default), `Success`, `Warning` or
  `Danger`, which sets its colour and icon. `duration` defaults to 4 seconds.
- At most three are shown; a fourth pushes out the oldest.
- `Dismiss(id)` takes one away early.

The message is shown as given, so pass text that is already translated.

### OfflineModeSwitch

The switch that turns offline mode on and off, with its help text. The Settings page shows it in the Offline mode
section; you can put it on a page of your own too: `<OfflineModeSwitch />`.

### SessionTimer

The countdown until the session's current token expires, shown under the user's name in the sidebar:
`<SessionTimer />`.

### ChangePasswordForm

The Profile page's change password form, as a panel: `<ChangePasswordForm />`. Use it if you replace the Profile
page.

### MfaSettingsPanel

The Profile page's Two-step verification panel, which turns a code from an authenticator app on or off:
`<MfaSettingsPanel />`. Shown only when signed in online. Use it if you replace the Profile page.

### AuthenticatorSetupPanel

Shows the secret to add an account to an authenticator app with: an "Open in authenticator app" link, a QR code, and
the key with a Copy button. `MfaSettingsPanel` and the sign-in page use it. It shows an
`AuthenticatorSetupViewModel` that your own view model owns and fills (`Show(secret, account)`):
`<AuthenticatorSetupPanel Setup="ViewModel.Setup" IdPrefix="my-mfa" />`.

### ForgotPasswordLink

The sign-in page's "Forgot password?" link, shown only while the forgot password page is turned on:
`<ForgotPasswordLink />`. Use it if you replace the sign-in page.

### E2eUnlockPanel

*Sync module.* Shows a sync profile's content while it is unlocked, and in its place, while it is locked, asks for its
passphrase:

```razor
<E2eUnlockPanel Profile="files">
    ...the profile's content...
</E2eUnlockPanel>
```

| Parameter | What it is |
|---|---|
| `Profile` | (required) The sync profile's name. For a profile a user created, its name is its id. |
| `ChildContent` | What to show while the profile is unlocked. |

While locked it shows a passphrase field and an Unlock button, plus an "Unlock with biometrics next time on this
device" tick box (or a biometrics button, once turned on) when the profile allows it. A profile with no passphrase yet
gets a Set passphrase button, which opens the passphrase form. If a passphrase change was cut short, it shows a
warning and Finish passphrase change instead.

### E2ePassphraseForm

*Sync module.* The form a new passphrase is entered in, with two tabs:

- **Passphrase**: the passphrase twice, and the acknowledgement that it cannot be recovered. When changing a
  passphrase, the current one too. Your own fields go at the top.
- **Advanced**: the Argon2id settings its key is derived with, and a Benchmark button.

Put it inside your dialog's `<form>`, with your own fields as its content:

```razor
<form id="notebook-form" @onsubmit="() => ViewModel.CreateCommand.ExecuteAsync(null)" @onsubmit:preventDefault>
    <E2ePassphraseForm Form="ViewModel.PassphraseForm" IdPrefix="notebook" FirstTabTitle="Notebook"
                       Disabled="ViewModel.IsWorking">
        <input type="text" class="form-control" id="notebook-title" @bind="ViewModel.Title" @bind:event="oninput" />
    </E2ePassphraseForm>
</form>
```

| Parameter | Default | What it is |
|---|---|---|
| `Form` | (required) | The form's view model, an `E2ePassphraseFormViewModel` owned by your view model. |
| `IdPrefix` | `e2e` | The start of every element id inside, so two forms on one page don't clash. |
| `FirstTabTitle` | "Passphrase" | The first tab's title, when it holds more than the passphrase. |
| `AcknowledgeText` | the framework's | The no-recovery acknowledgement, worded for what the passphrase protects. |
| `Disabled` | `false` | Disables the fields, e.g. while saving. |
| `ChildContent` | none | Your own fields, at the top of the first tab. |

The form shows a view model it is given, so your view model must raise `PropertyChanged` when it changes. See
[A view model that owns another](views-and-view-models.md#a-view-model-that-owns-another) and
[Your own passphrase dialog](sync-and-encryption.md#your-own-passphrase-dialog).

### E2eChangePassphraseDialog

*Sync module.* The whole change passphrase dialog for one profile (the current and new passphrases, the estimate, the
progress, and signing every device out), as Settings shows it. Render it while it should be open:

```razor
@if (ViewModel.IsChangingPassphrase)
{
    <E2eChangePassphraseDialog Profile="@ViewModel.ProfileName" OnClose="() => ViewModel.CloseChangePassphraseCommand.Execute(null)" />
}
```

| Parameter | What it is |
|---|---|
| `Profile` | (required) The sync profile's name. |
| `OnClose` | Called when the dialog closes. |

### Argon2idEditor

*Sync module.* The Argon2id settings as fields (memory, iterations, parallelism), with a Benchmark button. It is
inside `E2ePassphraseForm` and the Argon2id benchmark panel; you rarely need it on its own.

| Parameter | Default | What it is |
|---|---|---|
| `Model` | (required) | An `Argon2idBenchmarkViewModel`, owned by your view model. |
| `IdPrefix` | `argon2id` | The start of every element id inside. |
| `Disabled` | `false` | Disables the fields and button. |
| `Compact` | `false` | Lays the fields out two to a row, for a dialog. |

## Components the framework places itself

You don't add these to pages. They are listed so you know where they come from.

| Component | Package | Where | What it does |
|---|---|---|---|
| `OfflineBanner` | UI | Banners | Shown in offline mode, with a button to sign in online again. |
| `ToastHost` | UI | Overlays | Shows the toasts. |
| `AppUpdateIndicator` | UI | Both layouts | A small indicator while a new version of the app downloads. |
| `AppearanceSettingsSection`, `LanguageSettingsSection`, `OfflineSettingsSection`, `DeviceSettingsSection`, `RememberedDevicesSettingsSection`, `AboutSettingsSection` | UI | Settings | The framework's Settings sections. |
| `E2eBanner` | UI.Sync | Banners | Shown while a profile set up in code needs a passphrase. |
| `FolderAccessBanner` | UI.Sync | Banners | Asks the user to allow access again to the folder their files are kept in. |
| `SyncPill` | UI.Sync | Floating | The sync status pill. |
| `E2eLockIndicator` | UI.Sync | Inside `SyncPill` | How many profiles are unlocked, with Lock all. |
| `StorageMigrationOverlay` | UI.Sync | Overlays | Shown while another tab moves the files to another storage. |
| `SyncSettingsSection`, `E2eProfilesSection`, `Argon2idBenchmarkPanel` | UI.Sync | Settings | Sync, End to end encryption, and the Argon2id benchmark. Remove any with `RemoveSettingsSection<T>()`. |
| `E2eProfileDialog` | UI.Sync | Inside `E2eProfilesSection` and `E2eChangePassphraseDialog` | The dialogs for setting, changing and finishing passphrases, creating and deleting profiles. |
| `StartSyncOffOption` | UI.Sync | Sign-in page | "Start with sync turned off". |

The shell itself (`MainLayout`, `LoginLayout`), the router (`BlazwizRouter`) and `PageHost` (which shows the
framework's replaceable pages) are covered in [The shell and its extension points](shell-and-extension-points.md).

## Styles

The framework's stylesheet (`_content/Devoctomy.Blazwiz.UI/css/blazwiz.css`) styles Bootstrap 5.3 and adds a few
building blocks you can use in your markup, such as `panel`, `panel-title`, `panel-text`, `section-title` and
`detail-list`. The template's Showcase page (`--showcase`) shows themed buttons, badges, alerts, forms and tables to
copy from. Icons are [Bootstrap Icons](https://icons.getbootstrap.com/) (`bi-*`). See [Theming](theming.md).
