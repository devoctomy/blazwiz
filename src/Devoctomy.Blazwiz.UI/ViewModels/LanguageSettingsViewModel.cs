using System.Globalization;
using Blazing.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Devoctomy.Blazwiz.UI.Models;
using Devoctomy.Blazwiz.UI.Services.Interfaces;

namespace Devoctomy.Blazwiz.UI.ViewModels;

/// <summary>
/// ViewModel for the Language section of the Settings page, shown when the app supports more than one language.
/// </summary>
public partial class LanguageSettingsViewModel : ViewModelBase
{
    private readonly ICultureService cultureService;

    /// <summary>
    /// Initializes a new instance of the <see cref="LanguageSettingsViewModel"/> class.
    /// </summary>
    /// <param name="cultureService">The app's languages.</param>
    public LanguageSettingsViewModel(ICultureService cultureService)
    {
        this.cultureService = cultureService ?? throw new ArgumentNullException(nameof(cultureService));
        Options = cultureService.SupportedCultures
            .Select(c => new CultureOption { Name = c.Name, DisplayName = Capitalize(c.NativeName, c) })
            .ToList();
    }

    /// <summary>
    /// Gets a value indicating whether there is a choice to make.
    /// </summary>
    public bool IsVisible => Options.Count > 1;

    /// <summary>
    /// Gets the languages to choose from, each named in its own language.
    /// </summary>
    public IReadOnlyList<CultureOption> Options { get; }

    /// <summary>
    /// Gets the name of the culture the app is shown in.
    /// </summary>
    public string Current => cultureService.CurrentCulture.Name;

    /// <summary>
    /// Switches to another language, reloading the app.
    /// </summary>
    /// <param name="name">The culture's name.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task ChangeAsync(string? name)
    {
        if (string.IsNullOrEmpty(name) || string.Equals(name, Current, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await cultureService.ChangeCultureAsync(name);
    }

    private static string Capitalize(string text, CultureInfo culture) =>
        text.Length == 0 ? text : char.ToUpper(text[0], culture) + text[1..];
}
