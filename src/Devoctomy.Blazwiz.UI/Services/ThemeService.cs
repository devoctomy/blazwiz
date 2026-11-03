using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.UI.Models;
using Devoctomy.Blazwiz.UI.Services.Interfaces;

namespace Devoctomy.Blazwiz.UI.Services;

/// <inheritdoc/>
public class ThemeService(
    IUserSettingsService userSettingsService,
    IBrowserInteropService browserInteropService)
    : IThemeService
{
    private readonly IUserSettingsService userSettingsService = userSettingsService ?? throw new ArgumentNullException(nameof(userSettingsService));
    private readonly IBrowserInteropService browserInteropService = browserInteropService ?? throw new ArgumentNullException(nameof(browserInteropService));
    private bool initialized;

    /// <inheritdoc/>
    public event EventHandler? ThemeChanged;

    /// <inheritdoc/>
    public string CurrentTheme { get; private set; } = ThemeNames.Light;

    /// <inheritdoc/>
    public bool IsDark => CurrentTheme == ThemeNames.Dark;

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        if (initialized)
        {
            return;
        }

        var settings = await userSettingsService.GetSettingsAsync();
        var theme = settings.Theme ?? await browserInteropService.GetPreferredThemeAsync();
        CurrentTheme = theme == ThemeNames.Dark ? ThemeNames.Dark : ThemeNames.Light;
        await browserInteropService.SetThemeAsync(CurrentTheme);
        initialized = true;
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    public async Task SetThemeAsync(string theme)
    {
        if (theme != ThemeNames.Light && theme != ThemeNames.Dark)
        {
            throw new ArgumentException($"Unsupported theme '{theme}'.", nameof(theme));
        }

        CurrentTheme = theme;
        await browserInteropService.SetThemeAsync(theme);

        var settings = await userSettingsService.GetSettingsAsync();
        settings.Theme = theme;
        await userSettingsService.SaveSettingsAsync(settings);

        initialized = true;
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    public Task ToggleThemeAsync()
    {
        return SetThemeAsync(IsDark ? ThemeNames.Light : ThemeNames.Dark);
    }
}
