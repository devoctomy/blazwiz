using System.Globalization;
using System.Security.Cryptography;
using Blazing.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Devoctomy.Blazwiz.Encryption.Exceptions;
using Devoctomy.Blazwiz.Sync.Connectivity;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;
using Devoctomy.Blazwiz.UI.Sync.Text;
using Devoctomy.Blazwiz.UI.Text.Interfaces;
using Microsoft.Extensions.Localization;

namespace Devoctomy.Blazwiz.UI.Sync.ViewModels;

/// <summary>
/// ViewModel for a synced file's history (<c>FileHistoryPicker</c>), owned by the page showing the file. Its versions
/// are listed (metadata only) each time the dropdown is expanded, and never otherwise. Picking one downloads and
/// decrypts it, and the page shows <see cref="SelectedContent"/> in place of the file, read only, until it goes back
/// to the current version. Restoring saves the version's content as the file's new current version. With unsaved
/// changes on the page, picking a version first asks whether to save them. While the app is offline the dropdown is
/// disabled; a version already shown can still be restored, as restoring is a local save. A version that cannot be
/// decrypted on this device (e.g. under a key it does not hold) is not shown, and the user is told why.
/// </summary>
public partial class FileHistoryViewModel : ViewModelBase
{
    private readonly ISyncFileHistoryService history;
    private readonly IConnectivityService connectivity;
    private readonly IStringLocalizer<SyncUiText> text;
    private readonly IErrorText errorText;
    private readonly TimeProvider timeProvider;

    private string? profile;
    private string? key;
    private IFileHistoryHost? host;
    private int listing;
    private bool isOpen;
    private bool isLoading;
    private IReadOnlyList<SyncFileVersion> versions = [];
    private string? listError;
    private SyncFileVersionContent? selectedContent;
    private bool isReading;
    private string? error;
    private SyncFileVersion? pending;
    private bool isUnsavedDialogOpen;
    private bool isRestoreDialogOpen;
    private bool isRestoring;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileHistoryViewModel"/> class.
    /// </summary>
    /// <param name="history">Lists, reads and restores the file's versions.</param>
    /// <param name="connectivity">Says when the app is offline, so the dropdown is disabled.</param>
    /// <param name="text">The sync UI's text.</param>
    /// <param name="errorText">Describes errors.</param>
    /// <param name="timeProvider">Gives the local time zone the versions' times are shown in.</param>
    public FileHistoryViewModel(ISyncFileHistoryService history, IConnectivityService connectivity, IStringLocalizer<SyncUiText> text, IErrorText errorText, TimeProvider timeProvider)
    {
        this.history = history ?? throw new ArgumentNullException(nameof(history));
        this.connectivity = connectivity ?? throw new ArgumentNullException(nameof(connectivity));
        this.text = text ?? throw new ArgumentNullException(nameof(text));
        this.errorText = errorText ?? throw new ArgumentNullException(nameof(errorText));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.connectivity.StatusChanged += OnConnectivityChanged;
    }

    /// <summary>
    /// Raised when the version the page should show changes: to a past one (<see cref="SelectedContent"/>), or back to
    /// the current one (null), e.g. after a restore. Going back to the current one, the page reads the file again.
    /// </summary>
    public event EventHandler? ShownChanged;

    /// <summary>
    /// Gets a value indicating whether there is any history to show: false without a bucket (local development).
    /// </summary>
    public bool IsAvailable => history.IsAvailable && key != null;

    /// <summary>
    /// Gets a value indicating whether the app is offline (offline mode, sync turned off, no network or no credentials),
    /// so the dropdown is disabled. Before connectivity is first checked it is not.
    /// </summary>
    public bool IsOffline => connectivity.Status is { IsOnline: false, Reason: not null };

    /// <summary>
    /// Gets what the dropdown says while it is disabled offline, or null.
    /// </summary>
    public string? OfflineText => IsOffline ? text["Sync.History.Offline"].Value : null;

    /// <summary>
    /// Gets a value indicating whether the dropdown is expanded.
    /// </summary>
    public bool IsOpen
    {
        get => isOpen;
        private set => SetProperty(ref isOpen, value);
    }

    /// <summary>
    /// Gets a value indicating whether the versions are being listed.
    /// </summary>
    public bool IsLoading
    {
        get => isLoading;
        private set => SetProperty(ref isLoading, value);
    }

    /// <summary>
    /// Gets the file's versions in the bucket, newest first, as last listed.
    /// </summary>
    public IReadOnlyList<SyncFileVersion> Versions
    {
        get => versions;
        private set => SetProperty(ref versions, value);
    }

    /// <summary>
    /// Gets why the versions could not be listed (e.g. offline), or null.
    /// </summary>
    public string? ListError
    {
        get => listError;
        private set => SetProperty(ref listError, value);
    }

    /// <summary>
    /// Gets a value indicating whether the file has no versions in the bucket yet (it was never uploaded).
    /// </summary>
    public bool IsEmpty => !IsLoading && ListError == null && Versions.Count == 0;

    /// <summary>
    /// Gets the version shown, or null while the current one is.
    /// </summary>
    public SyncFileVersion? Selected => SelectedContent?.Version;

    /// <summary>
    /// Gets the past version shown, downloaded and decrypted, or null while the current one is.
    /// </summary>
    public SyncFileVersionContent? SelectedContent
    {
        get => selectedContent;
        private set
        {
            if (SetProperty(ref selectedContent, value))
            {
                OnPropertyChanged(nameof(Selected));
                OnPropertyChanged(nameof(IsViewingVersion));
                OnPropertyChanged(nameof(ViewingText));
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether a past version is shown, so the page shows it read only.
    /// </summary>
    public bool IsViewingVersion => SelectedContent != null;

    /// <summary>
    /// Gets what the bar above a past version says, or null while the current one is shown.
    /// </summary>
    public string? ViewingText => Selected is { } shown ? text["Sync.History.Viewing", Format(shown)].Value : null;

    /// <summary>
    /// Gets a value indicating whether a version is being downloaded.
    /// </summary>
    public bool IsReading
    {
        get => isReading;
        private set => SetProperty(ref isReading, value);
    }

    /// <summary>
    /// Gets why a version could not be shown or restored, or null.
    /// </summary>
    public string? Error
    {
        get => error;
        private set => SetProperty(ref error, value);
    }

    /// <summary>
    /// Gets a value indicating whether the dialog asking whether to save the page's changes first is open.
    /// </summary>
    public bool IsUnsavedDialogOpen
    {
        get => isUnsavedDialogOpen;
        private set => SetProperty(ref isUnsavedDialogOpen, value);
    }

    /// <summary>
    /// Gets a value indicating whether the dialog confirming a restore is open.
    /// </summary>
    public bool IsRestoreDialogOpen
    {
        get => isRestoreDialogOpen;
        private set => SetProperty(ref isRestoreDialogOpen, value);
    }

    /// <summary>
    /// Gets what the restore dialog says, naming the version shown.
    /// </summary>
    public string RestoreText => Selected is { } shown ? text["Sync.History.Restore.Text", Format(shown)] : string.Empty;

    /// <summary>
    /// Gets a value indicating whether a version is being restored.
    /// </summary>
    public bool IsRestoring
    {
        get => isRestoring;
        private set => SetProperty(ref isRestoring, value);
    }

    /// <summary>
    /// Sets the file whose history this is. Another file starts again from its current version.
    /// </summary>
    /// <param name="profileName">The sync profile the file is in.</param>
    /// <param name="fileKey">The file's key.</param>
    /// <param name="fileHost">The page showing it, asked about its unsaved changes.</param>
    public void SetFile(string profileName, string fileKey, IFileHistoryHost fileHost)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileName);
        ArgumentException.ThrowIfNullOrEmpty(fileKey);
        ArgumentNullException.ThrowIfNull(fileHost);

        host = fileHost;
        if (profileName == profile && fileKey == key)
        {
            return;
        }

        profile = profileName;
        key = fileKey;
        listing++;
        IsOpen = false;
        IsLoading = false;
        Versions = [];
        ListError = null;
        Error = null;
        pending = null;
        IsUnsavedDialogOpen = false;
        IsRestoreDialogOpen = false;
        SelectedContent = null;
        OnPropertyChanged(nameof(IsAvailable));
        OnPropertyChanged(nameof(IsEmpty));
    }

    /// <summary>
    /// Gets how a version is shown in the list: its ETag and when it was uploaded, in local time.
    /// </summary>
    /// <param name="version">The version.</param>
    /// <returns>The text.</returns>
    public string Format(SyncFileVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);

        var uploaded = TimeZoneInfo.ConvertTime(version.UploadedAt, timeProvider.LocalTimeZone);
        return text["Sync.History.Version", version.ETag.Trim('"'), uploaded.ToString("g", CultureInfo.CurrentCulture)];
    }

    /// <summary>
    /// Expands the dropdown, listing the versions again, or collapses it.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task ToggleAsync()
    {
        if (IsOpen)
        {
            Close();
            return;
        }

        if (profile == null || key == null || IsOffline)
        {
            return;
        }

        IsOpen = true;
        var current = ++listing;
        IsLoading = true;
        ListError = null;
        OnPropertyChanged(nameof(IsEmpty));
        try
        {
            var listed = await history.GetVersionsAsync(profile, key);
            if (current == listing)
            {
                Versions = listed;
            }
        }
        catch (Exception ex)
        {
            if (current == listing)
            {
                Versions = [];
                ListError = errorText.Describe(ex);
            }
        }
        finally
        {
            if (current == listing)
            {
                IsLoading = false;
                OnPropertyChanged(nameof(IsEmpty));
            }
        }
    }

    /// <summary>
    /// Collapses the dropdown.
    /// </summary>
    [RelayCommand]
    public void Close()
    {
        IsOpen = false;
    }

    /// <summary>
    /// Shows a version: a past one is downloaded first; null is the current one. With unsaved changes on the page, a
    /// past version waits for the user to say whether to save them.
    /// </summary>
    /// <param name="version">The version, or null for the current one.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task SelectAsync(SyncFileVersion? version)
    {
        IsOpen = false;
        if (version == null)
        {
            ShowCurrent();
            return;
        }

        if (host?.HasUnsavedChanges == true)
        {
            pending = version;
            IsUnsavedDialogOpen = true;
            return;
        }

        await ShowAsync(version);
    }

    /// <summary>
    /// Saves the page's changes, then shows the version picked. If they could not be saved, the page stays as it is.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task SaveAndViewAsync()
    {
        var version = TakePending();
        if (version != null && host != null && await host.SaveChangesAsync())
        {
            await ShowAsync(version);
        }
    }

    /// <summary>
    /// Drops the page's changes, then shows the version picked.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task DiscardAndViewAsync()
    {
        var version = TakePending();
        if (version != null && host != null)
        {
            host.DiscardChanges();
            await ShowAsync(version);
        }
    }

    /// <summary>
    /// Keeps the page's changes and stays on the current version.
    /// </summary>
    [RelayCommand]
    public void CancelView()
    {
        TakePending();
    }

    /// <summary>
    /// Goes back to the current version. Nothing is changed.
    /// </summary>
    [RelayCommand]
    public void BackToCurrent()
    {
        ShowCurrent();
    }

    /// <summary>
    /// Asks to confirm restoring the version shown.
    /// </summary>
    [RelayCommand]
    public void OpenRestore()
    {
        if (SelectedContent != null)
        {
            Error = null;
            IsRestoreDialogOpen = true;
        }
    }

    /// <summary>
    /// Closes the restore dialog without restoring. Ignored while restoring.
    /// </summary>
    [RelayCommand]
    public void CancelRestore()
    {
        if (!IsRestoring)
        {
            IsRestoreDialogOpen = false;
        }
    }

    /// <summary>
    /// Saves the version shown as the file's new current version, then shows the current version.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task RestoreAsync()
    {
        if (SelectedContent is not { } shown || IsRestoring)
        {
            return;
        }

        IsRestoring = true;
        try
        {
            await history.RestoreAsync(shown);
            IsRestoreDialogOpen = false;
            ShowCurrent();
        }
        catch (Exception ex)
        {
            IsRestoreDialogOpen = false;
            Error = errorText.Describe(ex);
        }
        finally
        {
            IsRestoring = false;
        }
    }

    /// <summary>
    /// Hides the error.
    /// </summary>
    [RelayCommand]
    public void DismissError()
    {
        Error = null;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            connectivity.StatusChanged -= OnConnectivityChanged;
        }

        base.Dispose(disposing);
    }

    // A version's content is checked when it is decrypted: under a key this device does not hold (E2eLockedException),
    // altered or not of this file (CryptographicException), or not an encrypted file of the profile (InvalidDataException).
    private static bool IsDecryptionFailure(Exception ex) =>
        ex is E2eLockedException or CryptographicException or InvalidDataException;

    private void OnConnectivityChanged(object? sender, ConnectivityStatus status)
    {
        if (IsOffline)
        {
            Close();
        }

        OnPropertyChanged(nameof(IsOffline));
        OnPropertyChanged(nameof(OfflineText));
    }

    private SyncFileVersion? TakePending()
    {
        var version = pending;
        pending = null;
        IsUnsavedDialogOpen = false;
        return version;
    }

    private async Task ShowAsync(SyncFileVersion version)
    {
        IsReading = true;
        Error = null;
        try
        {
            var content = await history.ReadVersionAsync(version);
            if (content == null)
            {
                Error = text["Sync.History.Gone"];
                return;
            }

            SelectedContent = content;
            ShownChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) when (IsDecryptionFailure(ex))
        {
            // The current version stays shown; nothing that could not be decrypted is ever shown or restored.
            Error = text["Sync.History.CannotDecrypt"];
        }
        catch (Exception ex)
        {
            Error = errorText.Describe(ex);
        }
        finally
        {
            IsReading = false;
        }
    }

    private void ShowCurrent()
    {
        if (SelectedContent == null)
        {
            return;
        }

        SelectedContent = null;
        ShownChanged?.Invoke(this, EventArgs.Empty);
    }
}
