using Blazing.Mvvm.ComponentModel;
using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Models.Settings;
using CommunityToolkit.Mvvm.Input;

namespace blazwiz.Portal.Application.ViewModels;

/// <summary>
/// ViewModel for the Appearance section of the Settings page: the light or dark theme.
/// </summary>
public partial class AppearanceSettingsViewModel : ViewModelBase
{
    private readonly IThemeService themeService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppearanceSettingsViewModel"/> class.
    /// </summary>
    /// <param name="themeService">The theme service.</param>
    public AppearanceSettingsViewModel(IThemeService themeService)
    {
        this.themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        this.themeService.ThemeChanged += OnThemeChanged;
    }

    /// <summary>
    /// Gets the current theme name.
    /// </summary>
    public string CurrentTheme => themeService.CurrentTheme;

    /// <summary>
    /// Selects the given theme.
    /// </summary>
    /// <param name="theme">The theme name, see <see cref="ThemeNames"/>.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public Task SelectThemeAsync(string theme)
    {
        return themeService.SetThemeAsync(theme);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            themeService.ThemeChanged -= OnThemeChanged;
        }

        base.Dispose(disposing);
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(CurrentTheme));
    }
}
