using blazwiz.Core.Sync.Interfaces;
using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Application.Shell.Interfaces;
using blazwiz.Portal.Application.Text.Interfaces;
using blazwiz.Portal.Application.Text;
using Microsoft.Extensions.Localization;

namespace blazwiz.Portal.Application.Services;

/// <inheritdoc cref="ISyncFolderAccessMonitor"/>
public sealed class SyncFolderAccessMonitor(
    ISyncFolderService folderService,
    ISyncScheduler syncScheduler,
    IBrowserNetworkStatus browserNetworkStatus,
    IShellContentService shellContentService,
    IStringLocalizer<SyncUiText> text,
    IErrorText errorText)
    : ISyncFolderAccessMonitor, IDisposable
{
    private readonly ISyncFolderService folderService = folderService ?? throw new ArgumentNullException(nameof(folderService));
    private readonly ISyncScheduler syncScheduler = syncScheduler ?? throw new ArgumentNullException(nameof(syncScheduler));
    private readonly IBrowserNetworkStatus browserNetworkStatus = browserNetworkStatus ?? throw new ArgumentNullException(nameof(browserNetworkStatus));
    private readonly IShellContentService shellContentService = shellContentService ?? throw new ArgumentNullException(nameof(shellContentService));
    private readonly IStringLocalizer<SyncUiText> text = text ?? throw new ArgumentNullException(nameof(text));
    private readonly IErrorText errorText = errorText ?? throw new ArgumentNullException(nameof(errorText));
    private bool isWatching;

    /// <inheritdoc/>
    public event EventHandler? Changed;

    /// <inheritdoc/>
    public bool NeedsAccess { get; private set; }

    /// <inheritdoc/>
    public string? FolderName { get; private set; }

    /// <inheritdoc/>
    public string? Error { get; private set; }

    /// <inheritdoc/>
    public void Start()
    {
        if (!isWatching)
        {
            browserNetworkStatus.BecameVisible += OnBecameVisible;
            isWatching = true;
        }
    }

    /// <inheritdoc/>
    public void Stop()
    {
        if (isWatching)
        {
            browserNetworkStatus.BecameVisible -= OnBecameVisible;
            isWatching = false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> CheckAsync()
    {
        try
        {
            if (!await folderService.NeedsReconnectAsync())
            {
                return false;
            }

            FolderName = (await folderService.GetStatusAsync())?.Name;
            NeedsAccess = true;
            Changed?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch (Exception)
        {
            // Sync reports the problem itself if the folder cannot be used.
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task ReconnectAsync()
    {
        try
        {
            if (!await folderService.ReconnectAsync())
            {
                SetError(text["Sync.Folder.NotAllowed"]);
                return;
            }

            if ((await folderService.GetStatusAsync())?.IsGranted != true)
            {
                SetError(text["Sync.Folder.NotKept"]);
                return;
            }
        }
        catch (Exception ex)
        {
            SetError(text["Sync.Folder.ReconnectFailed", errorText.Describe(ex)]);
            return;
        }

        Error = null;
        NeedsAccess = false;
        Changed?.Invoke(this, EventArgs.Empty);
        shellContentService.RequestReload();
        try
        {
            await syncScheduler.StartAsync();
        }
        catch (Exception)
        {
            // Sync failures show in the status pill and the sync log; the app keeps working locally.
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Stop();
    }

    private void SetError(string error)
    {
        Error = error;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // Phones (e.g. Chrome on Android) can take the permission away while the app is in the background.
    private async void OnBecameVisible(object? sender, EventArgs e)
    {
        try
        {
            if (!NeedsAccess && await CheckAsync())
            {
                await syncScheduler.StopAsync();
            }
        }
        catch (Exception)
        {
            // Event handlers must not throw.
        }
    }
}
