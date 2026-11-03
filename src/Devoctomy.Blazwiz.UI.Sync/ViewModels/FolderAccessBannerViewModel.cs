using Blazing.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Devoctomy.Blazwiz.UI.Sync.Services.Interfaces;

namespace Devoctomy.Blazwiz.UI.Sync.ViewModels;

/// <summary>
/// ViewModel for the banner asking the user to allow access again to the folder their files are kept in.
/// </summary>
public partial class FolderAccessBannerViewModel : ViewModelBase
{
    private readonly ISyncFolderAccessMonitor monitor;

    /// <summary>
    /// Initializes a new instance of the <see cref="FolderAccessBannerViewModel"/> class.
    /// </summary>
    /// <param name="monitor">Knows whether access is needed, and restores it.</param>
    public FolderAccessBannerViewModel(ISyncFolderAccessMonitor monitor)
    {
        this.monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
        this.monitor.Changed += OnChanged;
    }

    /// <summary>
    /// Gets a value indicating whether the banner is shown.
    /// </summary>
    public bool IsVisible => monitor.NeedsAccess;

    /// <summary>
    /// Gets the folder's name, when known.
    /// </summary>
    public string? FolderName => monitor.FolderName;

    /// <summary>
    /// Gets why the last attempt to reconnect failed, if it did.
    /// </summary>
    public string? Error => monitor.Error;

    /// <summary>
    /// Asks the browser for access to the folder again.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public Task ReconnectAsync()
    {
        return monitor.ReconnectAsync();
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            monitor.Changed -= OnChanged;
        }

        base.Dispose(disposing);
    }

    private void OnChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(IsVisible));
        OnPropertyChanged(nameof(FolderName));
        OnPropertyChanged(nameof(Error));
    }
}
