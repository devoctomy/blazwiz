using Blazing.Mvvm.ComponentModel;
using Devoctomy.Blazwiz.UI.Services.Interfaces;
using Devoctomy.Blazwiz.UI.Text;
using Microsoft.Extensions.Localization;

namespace Devoctomy.Blazwiz.UI.ViewModels;

/// <summary>
/// ViewModel for the small indicator in the corner while a new version of the app downloads in the background, so the
/// wait before the update is offered is visible.
/// </summary>
public partial class AppUpdateIndicatorViewModel : ViewModelBase
{
    private readonly IAppUpdateMonitor monitor;
    private readonly IStringLocalizer<UiText> text;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppUpdateIndicatorViewModel"/> class.
    /// </summary>
    /// <param name="monitor">Reports the download.</param>
    /// <param name="text">The UI's text.</param>
    public AppUpdateIndicatorViewModel(IAppUpdateMonitor monitor, IStringLocalizer<UiText> text)
    {
        this.monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
        this.text = text ?? throw new ArgumentNullException(nameof(text));
        this.monitor.Changed += OnChanged;
    }

    /// <summary>
    /// Gets a value indicating whether the indicator is shown.
    /// </summary>
    public bool IsVisible => monitor.IsDownloading;

    /// <summary>
    /// Gets the indicator's text, with the percentage done when the service worker reports progress.
    /// </summary>
    public string Message => monitor.Progress is { } progress
        ? text["AppUpdate.DownloadingPercent", (int)Math.Round(progress * 100)]
        : text["AppUpdate.Downloading"];

    /// <inheritdoc/>
    public override Task OnInitializedAsync()
    {
        return monitor.StartAsync();
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
        OnPropertyChanged(nameof(Message));
    }
}
