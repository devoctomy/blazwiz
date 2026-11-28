# Devoctomy.Blazwiz.UI.Sync

The blazwiz sync module: sync (`Devoctomy.Blazwiz.Sync`) wired to the app shell, Cognito and S3, with its UI: the Sync
page, the status pill, and storage and end to end encryption settings. Each profile's keys are kept in the user's own
prefix of the data bucket (in the browser when no data bucket is configured, for local development), and a profile
can allow unlocking with a passkey.

## Usage

After `AddBlazwizUi`:

```csharp
builder.Services.AddBlazwizSyncModule(builder.Configuration, sync => sync
    .AddProfile("files", profile => profile
        .UseKeyId("3f2b8c1e-6d4a-4f7b-9c2e-8a1d5b7e9f40")
        .UseRemote("files/**/*")));
```

See `Devoctomy.Blazwiz.Sync` for the builder. The module adds Cognito sign in (`AddBlazwizCognito`) itself, and reads
the `AwsS3` section of the configuration, which the deploy script of `Devoctomy.Blazwiz.Aws` writes:

```json
"AwsS3": {
  "Region": "eu-west-2",
  "DataBucketName": "..."
}
```

and the optional `Argon2id` section, the Argon2id settings for new keys in place of the build's defaults (lighter
ones suit local development and UI tests):

```json
"Argon2id": {
  "MemoryKib": 19456
}
```

Settings lists the profiles set up in code, by their display names (`UseDisplayText(key, resourceType)` on the
profile, or its name), and the protected profiles the user creates, by id, each with its own passphrase. A banner
shows while a profile set up in code has no passphrase yet, and a lock indicator beside the sync pill shows how many
are unlocked, with Lock all. Wrap a page's content of a profile in `E2eUnlockPanel`, and while the profile is locked
it asks for the passphrase in its place:

```razor
<E2eUnlockPanel Profile="photos">
    ...
</E2eUnlockPanel>
```

Its text comes from resources (`SyncUiText`), so apps can reword and translate it.

### Settings sections

The module adds three sections to the Settings page, each registered on its own: Sync (`SyncSettingsSection`), End to
end encryption (`E2eProfilesSection`) and the Argon2id benchmark (`Argon2idBenchmarkPanel`). An app that does the same
job on its own pages takes a section off the page:

```csharp
builder.Services.RemoveSettingsSection<E2eProfilesSection>();
```

### The passphrase form

Wherever a new passphrase is entered, it is entered in one form, `E2ePassphraseForm`, with two tabs:

- **Passphrase**: the passphrase twice, and the acknowledgement that it cannot be recovered. When changing a
  passphrase, the current one too.
- **Advanced**: the Argon2id settings its key is derived with (`Argon2idEditor`: memory, iterations, parallelism), and
  a Benchmark button. They start at the settings that apply: the app's for a new passphrase, the profile's current
  ones when changing a passphrase. The user can enter anything Argon2id can run; there is no upper limit. Settings left
  as they start are used as they are. Changed settings are only accepted once a benchmark has completed with exactly
  those values on this device, and changing a value afterwards needs another benchmark. The key length is fixed,
  because the key wraps the profile's master key.

Settings uses it for New protected profile, Set passphrase and Change passphrase. To use it in a dialog of your own,
have your view model take an `E2ePassphraseFormViewModel` in its constructor (each gets one of its own), and put the
component in the dialog's form with your own fields as its content, shown at the top of the first tab:

```razor
<form @onsubmit="() => ViewModel.CreateCommand.ExecuteAsync(null)" @onsubmit:preventDefault>
    <E2ePassphraseForm Form="ViewModel.PassphraseForm" IdPrefix="album" FirstTabTitle="Album">
        <input type="text" @bind="ViewModel.Name" />
    </E2ePassphraseForm>
</form>
```

```csharp
await PassphraseForm.ResetAsync(scope, asksForCurrentPassphrase: true); // when the dialog opens; no scope for a new profile
var error = PassphraseForm.Validate();                                 // null when it can be used
await profiles.CreateAsync(PassphraseForm.Passphrase, PassphraseForm.GetArgon2Parameters());
PassphraseForm.ClearPassphrases();                                     // once used
```

The form has no view model of its own to follow: it is shown again whenever your view is. So your view model says when
the form changes, which also keeps your confirm button in step with `PassphraseForm.CanConfirm`:

```csharp
PassphraseForm.PropertyChanged += (_, _) => OnPropertyChanged(nameof(PassphraseForm)); // and remove it in Dispose
```

`ISyncProtectedProfileService.CreateAsync` and `SetUpAsync`, and `ISyncKeyRotationService.RotateAsync`, take the chosen
settings. The template's new notebook dialog is built this way.

A user remembered on this device with a passkey ("Remember me" at sign in) can tick "Unlock with biometrics next time
on this device" when unlocking a profile with its passphrase in the panel. To use biometrics from elsewhere, such as a
list of the user's profiles, inject `IE2eDeviceUnlockService`:

- `CanUnlockAsync(scope)`: whether biometrics can unlock the profile here (turned on here, and the user has a passkey
  here). Show it on locked profiles, e.g. with a fingerprint icon.
- `UnlockAsync(scope)`: syncs the profile first when online, asks for the passkey, unlocks it, and wipes the passkey's
  key from memory again. It returns false when the passkey is cancelled or cannot unlock it; open the profile's page
  anyway, and the panel asks for the passphrase.

The passkey's key is never left in memory: it is wiped once the session has started, after each unlock with
biometrics, and whenever a profile locks, so every unlock with biometrics asks for the passkey.
- `ForgetAsync(scope)`: stops unlocking it with biometrics on this device. Its passphrase still unlocks it. Ask the
  user to confirm first.

The template's Notebooks page uses all three.

### Changing a passphrase

Change passphrase, in Settings, rotates the profile's keys and encrypts its files again, so anyone who had the old
passphrase can read nothing written afterwards. What the user sees:

1. They enter the current and new passphrases. The Advanced tab shows the Argon2id settings the profile has now: the
   new passphrase keeps them unless they are changed there (and benchmarked). The profile is synced and an upload is
   timed. Offline, or if the sync fails, the change is refused and nothing has changed.
2. An estimate of how long it will take, with a warning: it cannot be skipped or stopped, and every device is signed
   out at the end. They can still cancel.
3. A progress bar while the files are encrypted again and uploaded. The dialog cannot be closed.
4. Every device, this one included, is signed out (`IE2eRotationSignOut`). If the sign-in service cannot be reached,
   the dialog says so and keeps offering Sign out everywhere until it works.

To offer it from a page of your own, in place of or as well as the Settings section, render
`E2eChangePassphraseDialog` while it should be open. It is the same dialog, for one profile:

```razor
@if (ViewModel.IsChangingPassphrase)
{
    <E2eChangePassphraseDialog Profile="@ViewModel.ProfileName" OnClose="() => ViewModel.CloseChangePassphraseCommand.Execute(null)" />
}
```

If the change is cut short (the tab is closed, the connection drops), the profile is unusable until it is finished.
`E2eUnlockPanel` shows a warning and Finish passphrase change in place of the profile's pages, on whichever device
unlocks it next, and Settings shows the profile as "Passphrase change unfinished".

A device that unlocks with the passkey is asked for the new passphrase, and told why, once the keys have been changed
on another device.

### A file's history

`FileHistoryPicker` is a History dropdown for the header of a page showing a synced file: **Current**, then every
version the bucket has, each as its ETag and upload time, listed each time it is expanded. `FileHistoryBar` goes above
the file's content: while a past version is shown, it says which, with **Back to current** and **Restore this
version**. Both take a `FileHistoryViewModel`, owned by the page's view model:

```razor
<FileHistoryPicker Model="ViewModel.History" />
<FileHistoryBar Model="ViewModel.History" />
```

The page's view model injects `FileHistoryViewModel`, calls `SetFile(profile, key, host)` once the file has loaded,
raises `PropertyChanged` for it when it changes, and shows `SelectedContent` (decrypted) read only while
`IsViewingVersion`. `ShownChanged` says when that changes; back on the current version, the page reads the file again,
as it may have been restored. The host (`IFileHistoryHost`, usually the page's view model) says whether there are
unsaved changes, and saves or drops them: with unsaved changes, picking a version first asks Save, Don't save or
Cancel. Restoring asks to confirm, then saves the version's content as the file's new current version
(`ISyncFileHistoryService.RestoreAsync`). While the app is offline (`IConnectivityService` gives a reason) the
dropdown is disabled. A version that can't be decrypted on this device is not shown, and the bar says so. Without a
data bucket, both show nothing. Text is under `Sync.History.*`.

## Versions

Every `Devoctomy.Blazwiz.*` package and the `Devoctomy.Blazwiz.Templates` template are released together at one
version. Use the same version of all of them; an app made from the template sets it once, as `BlazwizVersion` in its
`Directory.Packages.props`. Before 1.0 any release may break. See [versioning](https://github.com/devoctomy/blazwiz/blob/main/docs/versioning.md) and
the [changelog](https://github.com/devoctomy/blazwiz/blob/main/CHANGELOG.md).

Part of [blazwiz](https://github.com/devoctomy/blazwiz): a framework for Blazor WebAssembly PWAs that sign in with AWS Cognito, sync files to S3 with end
to end encryption, and work offline.
