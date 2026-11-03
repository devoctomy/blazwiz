using System.Globalization;
using Devoctomy.Blazwiz.Core.Text.Interfaces;
using Microsoft.Extensions.Localization;

namespace Devoctomy.Blazwiz.Core.Text;

/// <inheritdoc/>
public class TextPlurals(TextOptions options)
    : ITextPlurals
{
    private readonly TextOptions options = options ?? throw new ArgumentNullException(nameof(options));

    /// <inheritdoc/>
    public PluralCategory GetCategory(CultureInfo culture, long count)
    {
        ArgumentNullException.ThrowIfNull(culture);
        return options.PluralRules.TryGetValue(culture.TwoLetterISOLanguageName, out var rules)
            ? rules(count)
            : count == 1 ? PluralCategory.One : PluralCategory.Other;
    }

    /// <inheritdoc/>
    public string Format(IStringLocalizer text, string key, long count, params object[] arguments)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        object[] values = [count, .. arguments ?? []];
        var category = GetCategory(CultureInfo.CurrentUICulture, count);
        var form = text[$"{key}.{category}", values];
        return form.ResourceNotFound ? text[$"{key}.{PluralCategory.Other}", values] : form;
    }
}
