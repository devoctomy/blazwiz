using System.Collections;
using System.Globalization;
using System.Resources;
using Devoctomy.Blazwiz.Core.Text;
using Devoctomy.Blazwiz.Core.Text.Interfaces;
using Devoctomy.Blazwiz.UI.Text;
using Devoctomy.Blazwiz.UI.Text.Interfaces;
using Microsoft.Extensions.Localization;

namespace Devoctomy.Blazwiz.Testing;

/// <summary>
/// The framework's real text, as the app gets it before its own overrides, so an app's tests can check the actual
/// wording, and check that the app's overrides still name text the framework has.
/// </summary>
public sealed class FrameworkText
{
    private readonly OverridingStringLocalizerFactory factory;
    private readonly Type[] resources;

    /// <summary>
    /// Initializes a new instance of the <see cref="FrameworkText"/> class.
    /// </summary>
    /// <param name="moduleTextResources">The text of the modules the app uses, e.g. <c>typeof(SyncUiText)</c>: their
    /// error codes are described, and their keys can be overridden.</param>
    public FrameworkText(params Type[] moduleTextResources)
    {
        ArgumentNullException.ThrowIfNull(moduleTextResources);

        Options = new TextOptions();
        factory = new OverridingStringLocalizerFactory(Options);
        resources = [typeof(UiText), .. moduleTextResources];
        Ui = new StringLocalizer<UiText>(factory);
        Plurals = new TextPlurals(Options);
        Errors = new ErrorText(factory, moduleTextResources.Select(t => new ErrorTextSource(t)), Plurals);
    }

    /// <summary>
    /// Gets the options, e.g. to add overrides in a test (see <see cref="TextBuilder"/>).
    /// </summary>
    public TextOptions Options { get; }

    /// <summary>
    /// Gets the app shell's text.
    /// </summary>
    public IStringLocalizer<UiText> Ui { get; }

    /// <summary>
    /// Gets the plural forms.
    /// </summary>
    public ITextPlurals Plurals { get; }

    /// <summary>
    /// Gets the error descriptions.
    /// </summary>
    public IErrorText Errors { get; }

    /// <summary>
    /// Gets the text named after a resource class: a module's, or the app's own.
    /// </summary>
    /// <typeparam name="TResource">The class the resources are named after.</typeparam>
    /// <returns>The text.</returns>
    public IStringLocalizer<TResource> For<TResource>() => new StringLocalizer<TResource>(factory);

    /// <summary>
    /// Finds the keys in the app's overrides (its neutral resources) that are not framework text, e.g. because the
    /// framework renamed or removed them in an upgrade. An override with such a key is never used.
    /// </summary>
    /// <param name="overrides">The class the app's override resources are named after.</param>
    /// <returns>The keys that are not framework text; empty when every override is used.</returns>
    public IReadOnlyList<string> UnknownOverrideKeys(Type overrides)
    {
        ArgumentNullException.ThrowIfNull(overrides);

        var known = resources.SelectMany(Keys).ToHashSet(StringComparer.Ordinal);
        return Keys(overrides).Where(k => !known.Contains(k)).Order(StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Finds the app's overrides (in its neutral resources) whose placeholders, such as <c>{0}</c>, are not the same
    /// as the framework text's they replace, e.g. after the framework added or removed one in an upgrade. Such an
    /// override shows the wrong values, or fails to format.
    /// </summary>
    /// <param name="overrides">The class the app's override resources are named after.</param>
    /// <returns>The keys whose placeholders differ; empty when they all match.</returns>
    public IReadOnlyList<string> MismatchedOverridePlaceholders(Type overrides)
    {
        ArgumentNullException.ThrowIfNull(overrides);

        var framework = resources
            .SelectMany(Entries)
            .GroupBy(e => e.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Value, StringComparer.Ordinal);
        return Entries(overrides)
            .Where(e => framework.TryGetValue(e.Key, out var text) && !Placeholders(text).SetEquals(Placeholders(e.Value)))
            .Select(e => e.Key)
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    // The argument indexes a format string uses: {0}, {1:N0} and {2,5} give 0, 1 and 2. {{ and }} are not placeholders.
    private static HashSet<int> Placeholders(string text) =>
        System.Text.RegularExpressions.Regex.Matches(text.Replace("{{", string.Empty, StringComparison.Ordinal), @"\{(\d+)(?:[,:][^}]*)?\}")
            .Select(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture))
            .ToHashSet();

    private static IEnumerable<KeyValuePair<string, string>> Entries(Type resource)
    {
        var set = new ResourceManager(resource.FullName!, resource.Assembly)
            .GetResourceSet(CultureInfo.InvariantCulture, createIfNotExists: true, tryParents: false);
        return set?.Cast<DictionaryEntry>()
            .Where(e => e.Value is string)
            .Select(e => new KeyValuePair<string, string>((string)e.Key, (string)e.Value!)) ?? [];
    }

    private static IEnumerable<string> Keys(Type resource)
    {
        var set = new ResourceManager(resource.FullName!, resource.Assembly)
            .GetResourceSet(CultureInfo.InvariantCulture, createIfNotExists: true, tryParents: false);
        return set?.Cast<DictionaryEntry>().Select(e => (string)e.Key) ?? [];
    }
}
