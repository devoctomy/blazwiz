using Blazing.Mvvm.ComponentModel;
using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;

namespace Devoctomy.Blazwiz.UI.Sync.ViewModels;

/// <summary>
/// ViewModel for the overlay shown while another tab moves the files to another storage. When the move finishes,
/// this tab reloads to use the new storage.
/// </summary>
public partial class StorageMigrationOverlayViewModel : ViewModelBase
{
    private readonly ISyncStorageChannel storageChannel;
    private readonly INavigationService navigationService;

    private bool isVisible;

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageMigrationOverlayViewModel"/> class.
    /// </summary>
    /// <param name="storageChannel">Hears about storage moves in other tabs.</param>
    /// <param name="navigationService">Reloads the app once a move finishes.</param>
    public StorageMigrationOverlayViewModel(ISyncStorageChannel storageChannel, INavigationService navigationService)
    {
        this.storageChannel = storageChannel ?? throw new ArgumentNullException(nameof(storageChannel));
        this.navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        this.storageChannel.MessageReceived += OnMessageReceived;
    }

    /// <summary>
    /// Gets a value indicating whether another tab is moving the files.
    /// </summary>
    public bool IsVisible
    {
        get => isVisible;
        private set => SetProperty(ref isVisible, value);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            storageChannel.MessageReceived -= OnMessageReceived;
        }

        base.Dispose(disposing);
    }

    private void OnMessageReceived(object? sender, SyncStorageChannelMessage e)
    {
        if (e == SyncStorageChannelMessage.Migrating)
        {
            IsVisible = true;
            return;
        }

        // Another tab moved the files: start again against the storage now in use.
        navigationService.Reload();
    }
}
