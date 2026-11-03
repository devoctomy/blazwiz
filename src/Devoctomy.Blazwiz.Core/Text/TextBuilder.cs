using System.Globalization;
using System.Resources;

namespace Devoctomy.Blazwiz.Core.Text;

/// <summary>
/// Configures the app's text. Used by <see cref="Extensions.TextServiceCollectionExtensions.AddBlazwizText"/>.
/// </summary>
public class TextBuilder(TextOptions options)
{
    /// <summary>
    /// Gets the options being configured.
    /// </summary>
    public TextOptions Options { get; } = options ?? throw new ArgumentNullException(nameof(options));

    /// <summary>
    /// Adds the app's own resources: a <c>.resx</c> next to <typeparamref name="TResource"/>, with the same name. Its
    /// keys override the framework's text with the same keys, and its culture-specific files (e.g.
    /// <c>AppText.fr.resx</c>) add translations.
    /// </summary>
    /// <typeparam name="TResource">The class the resources are named after.</typeparam>
    /// <returns>The builder for method chaining.</returns>
    public TextBuilder AddOverrides<TResource>()
    {
        var type = typeof(TResource);
        Options.OverrideResources.Add(new ResourceManager(type.FullName!, type.Assembly));
        return this;
    }

    /// <summary>
    /// Overrides one text in code.
    /// </summary>
    /// <param name="key">The text's key, e.g. <c>Sync.E2eBanner.Locked</c>.</param>
    /// <param name="text">The new text. It must have the same placeholders as the original.</param>
    /// <param name="culture">The culture it is for, e.g. <c>fr</c>; null for every culture.</param>
    /// <returns>The builder for method chaining.</returns>
    public TextBuilder Override(string key, string text, string? culture = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(text);

        var name = culture == null ? string.Empty : CultureInfo.GetCultureInfo(culture).Name;
        if (!Options.Overrides.TryGetValue(key, out var byCulture))
        {
            byCulture = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Options.Overrides[key] = byCulture;
        }

        byCulture[name] = text;
        return this;
    }

    /// <summary>
    /// Sets the plural rules for a language. Languages without rules use the English ones (one, other).
    /// </summary>
    /// <param name="language">The language's two letter ISO name, e.g. <c>pl</c>.</param>
    /// <param name="rules">Chooses the plural form for a count.</param>
    /// <returns>The builder for method chaining.</returns>
    public TextBuilder UsePluralRules(string language, Func<long, PluralCategory> rules)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        ArgumentNullException.ThrowIfNull(rules);
        Options.PluralRules[language] = rules;
        return this;
    }

    /// <summary>
    /// Pseudo-localises every text, to find visible text that does not come from resources (see
    /// <see cref="TextOptions.PseudoLocalization"/>). For testing only.
    /// </summary>
    /// <returns>The builder for method chaining.</returns>
    public TextBuilder UsePseudoLocalization()
    {
        Options.PseudoLocalization = true;
        return this;
    }

    /// <summary>
    /// Sets the cultures the app offers. The first is the default.
    /// </summary>
    /// <param name="cultures">The culture names, e.g. <c>en-GB</c>, <c>fr-FR</c>.</param>
    /// <returns>The builder for method chaining.</returns>
    public TextBuilder SupportedCultures(params string[] cultures)
    {
        ArgumentNullException.ThrowIfNull(cultures);
        Options.SupportedCultures.Clear();
        Options.SupportedCultures.AddRange(cultures.Select(CultureInfo.GetCultureInfo));
        return this;
    }
}
