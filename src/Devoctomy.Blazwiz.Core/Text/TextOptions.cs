using System.Globalization;
using System.Resources;

namespace Devoctomy.Blazwiz.Core.Text;

/// <summary>
/// The app's text settings: its overrides of the framework's text, plural rules for its languages, and the cultures it
/// supports. Set with <see cref="Extensions.TextServiceCollectionExtensions.AddBlazwizText"/>.
/// </summary>
public class TextOptions
{
    /// <summary>
    /// Gets the overrides set in code: key, then culture name (empty for every culture), then the text.
    /// </summary>
    public Dictionary<string, Dictionary<string, string>> Overrides { get; } = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

    /// <summary>
    /// Gets the app's own resources overriding the framework's text, in the order added. Later ones win.
    /// </summary>
    public List<ResourceManager> OverrideResources { get; } = [];

    /// <summary>
    /// Gets the plural rules for each language (two letter ISO name, e.g. <c>pl</c>). Languages not listed use the
    /// English rules.
    /// </summary>
    public Dictionary<string, Func<long, PluralCategory>> PluralRules { get; } = new Dictionary<string, Func<long, PluralCategory>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the cultures the app offers, e.g. <c>en-GB</c> and <c>fr-FR</c>. The first is the default. Empty means the
    /// app only uses its neutral (English) text.
    /// </summary>
    public List<CultureInfo> SupportedCultures { get; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether every text is pseudo-localised: accented and wrapped in
    /// <c>[!! </c> and <c> !!]</c>, e.g. <c>[!! Śéţţîñĝš !!]</c>. For testing only: any visible text without the
    /// wrapping does not come from resources, so it cannot be reworded or translated.
    /// </summary>
    public bool PseudoLocalization { get; set; }
}
