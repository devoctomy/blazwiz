using System.Globalization;
using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Core.Text;
using Devoctomy.Blazwiz.UI.Services.Interfaces;

namespace Devoctomy.Blazwiz.UI.Services;

/// <inheritdoc/>
public class CultureService(
    TextOptions textOptions,
    ILocalStorageService localStorageService,
    IBrowserInteropService browserInteropService,
    INavigationService navigationService)
    : ICultureService
{
    /// <summary>
    /// The local storage key the chosen culture is kept under.
    /// </summary>
    public const string StorageKey = "culture";

    private readonly TextOptions textOptions = textOptions ?? throw new ArgumentNullException(nameof(textOptions));
    private readonly ILocalStorageService localStorageService = localStorageService ?? throw new ArgumentNullException(nameof(localStorageService));
    private readonly IBrowserInteropService browserInteropService = browserInteropService ?? throw new ArgumentNullException(nameof(browserInteropService));
    private readonly INavigationService navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));

    /// <inheritdoc/>
    public IReadOnlyList<CultureInfo> SupportedCultures => textOptions.SupportedCultures;

    /// <inheritdoc/>
    public CultureInfo CurrentCulture => CultureInfo.CurrentUICulture;

    /// <inheritdoc/>
    public async Task ApplySavedCultureAsync()
    {
        if (SupportedCultures.Count == 0)
        {
            return;
        }

        var saved = await localStorageService.GetItemAsync(StorageKey);
        var culture = Find(saved) ?? Match(CultureInfo.CurrentUICulture) ?? SupportedCultures[0];
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        await browserInteropService.SetDocumentLanguageAsync(culture.Name);
    }

    /// <inheritdoc/>
    public async Task ChangeCultureAsync(string name)
    {
        var culture = Find(name) ?? throw new ArgumentException($"The app does not support the culture '{name}'.", nameof(name));
        await localStorageService.SetItemAsync(StorageKey, culture.Name);
        navigationService.Reload();
    }

    private CultureInfo? Find(string? name) =>
        string.IsNullOrWhiteSpace(name)
            ? null
            : SupportedCultures.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));

    // The browser's language: the same culture, or failing that the same language (fr-CA matches fr-FR).
    private CultureInfo? Match(CultureInfo browser) =>
        Find(browser.Name)
        ?? SupportedCultures.FirstOrDefault(c => string.Equals(c.TwoLetterISOLanguageName, browser.TwoLetterISOLanguageName, StringComparison.OrdinalIgnoreCase));
}
