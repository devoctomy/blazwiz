using Devoctomy.Blazwiz.Core.Exceptions;
using Devoctomy.Blazwiz.Core.Text.Interfaces;
using Devoctomy.Blazwiz.UI.Text.Interfaces;
using Microsoft.Extensions.Localization;

namespace Devoctomy.Blazwiz.UI.Text;

/// <inheritdoc/>
public class ErrorText(IStringLocalizerFactory localizerFactory, IEnumerable<ErrorTextSource> sources, ITextPlurals plurals)
    : IErrorText
{
    /// <summary>
    /// The key of the text shown for errors without a code, or whose code has no text.
    /// </summary>
    public const string UnknownKey = "Error.Unknown";

    private readonly IStringLocalizer uiText = (localizerFactory ?? throw new ArgumentNullException(nameof(localizerFactory))).Create(typeof(UiText));
    private readonly IReadOnlyList<IStringLocalizer> localizers = (sources ?? throw new ArgumentNullException(nameof(sources)))
        .Select(s => localizerFactory.Create(s.ResourceType))
        .ToList();

    private readonly ITextPlurals plurals = plurals ?? throw new ArgumentNullException(nameof(plurals));

    /// <inheritdoc/>
    public string Describe(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception is BlazwizException coded ? Describe(coded.Code, coded.Arguments) : uiText[UnknownKey];
    }

    /// <inheritdoc/>
    public string Describe(string code, IReadOnlyList<object> arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentNullException.ThrowIfNull(arguments);

        var key = "Error." + code;
        foreach (var text in localizers.Append(uiText))
        {
            if (arguments.Count > 0 && IsCount(arguments[0]) && !text[key + ".Other"].ResourceNotFound)
            {
                return plurals.Format(text, key, Convert.ToInt64(arguments[0], System.Globalization.CultureInfo.InvariantCulture), [.. arguments.Skip(1)]);
            }

            var found = text[key, [.. arguments]];
            if (!found.ResourceNotFound)
            {
                return found;
            }
        }

        return uiText[UnknownKey];
    }

    private static bool IsCount(object value) => value is int or long or short or byte;
}
