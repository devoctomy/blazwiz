using System.Globalization;
using Microsoft.Extensions.Localization;

namespace Devoctomy.Blazwiz.Core.Text.Interfaces;

/// <summary>
/// Gets text that depends on a count, in the right plural form for the language (e.g. "1 file", "3 files").
/// </summary>
public interface ITextPlurals
{
    /// <summary>
    /// Gets the plural form a language uses for a count.
    /// </summary>
    /// <param name="culture">The culture.</param>
    /// <param name="count">The count.</param>
    /// <returns>The plural form.</returns>
    PluralCategory GetCategory(CultureInfo culture, long count);

    /// <summary>
    /// Gets the text for a count from the resource <c>&lt;key&gt;.&lt;form&gt;</c> for the current culture, falling
    /// back to <c>&lt;key&gt;.Other</c>. The count is placeholder <c>{0}</c>; any further arguments follow it.
    /// </summary>
    /// <param name="text">The resources to look in.</param>
    /// <param name="key">The text's key without the form, e.g. <c>Sync.Storage.FileCount</c>.</param>
    /// <param name="count">The count.</param>
    /// <param name="arguments">Values for placeholders <c>{1}</c> onwards.</param>
    /// <returns>The text.</returns>
    string Format(IStringLocalizer text, string key, long count, params object[] arguments);
}
