using System.Collections;
using System.Globalization;
using System.Resources;
using Microsoft.Extensions.Localization;

namespace blazwiz.Core.Text;

/// <summary>
/// The framework's text, with the app's overrides. For each culture from the most specific to the neutral one (e.g.
/// <c>fr-CA</c>, <c>fr</c>, neutral), it looks at the app's overrides first and then the package's text, so a package's
/// French text still wins over the app's neutral (English) rewording.
/// </summary>
public class OverridingStringLocalizer(ResourceManager resources, TextOptions options)
    : IStringLocalizer
{
    private readonly ResourceManager resources = resources ?? throw new ArgumentNullException(nameof(resources));
    private readonly TextOptions options = options ?? throw new ArgumentNullException(nameof(options));

    /// <inheritdoc/>
    public LocalizedString this[string name]
    {
        get
        {
            ArgumentNullException.ThrowIfNull(name);
            var value = Find(name, CultureInfo.CurrentUICulture);
            return new LocalizedString(name, value ?? name, resourceNotFound: value == null, searchedLocation: resources.BaseName);
        }
    }

    /// <inheritdoc/>
    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            ArgumentNullException.ThrowIfNull(name);
            var value = Find(name, CultureInfo.CurrentUICulture);
            var text = value == null ? name : string.Format(CultureInfo.CurrentCulture, value, arguments);
            return new LocalizedString(name, text, resourceNotFound: value == null, searchedLocation: resources.BaseName);
        }
    }

    /// <inheritdoc/>
    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
    {
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var culture in Cultures(CultureInfo.CurrentUICulture, includeParentCultures))
        {
            AddKeys(keys, resources.GetResourceSet(culture, createIfNotExists: true, tryParents: false));
        }

        foreach (var key in keys)
        {
            yield return this[key];
        }
    }

    private static IEnumerable<CultureInfo> Cultures(CultureInfo culture, bool includeParents = true)
    {
        var current = culture;
        while (true)
        {
            yield return current;
            if (!includeParents || current.Equals(CultureInfo.InvariantCulture))
            {
                yield break;
            }

            current = current.Parent;
        }
    }

    private static void AddKeys(ISet<string> keys, ResourceSet? set)
    {
        if (set == null)
        {
            return;
        }

        foreach (DictionaryEntry entry in set)
        {
            if (entry.Key is string key && entry.Value is string)
            {
                keys.Add(key);
            }
        }
    }

    private static string? GetExact(ResourceManager manager, string name, CultureInfo culture)
    {
        try
        {
            return manager.GetResourceSet(culture, createIfNotExists: true, tryParents: false)?.GetString(name);
        }
        catch (MissingManifestResourceException)
        {
            return null;
        }
    }

    private string? Find(string name, CultureInfo culture)
    {
        foreach (var current in Cultures(culture))
        {
            if (options.Overrides.TryGetValue(name, out var byCulture) && byCulture.TryGetValue(current.Name, out var overridden))
            {
                return overridden;
            }

            for (var i = options.OverrideResources.Count - 1; i >= 0; i--)
            {
                if (GetExact(options.OverrideResources[i], name, current) is { } appText)
                {
                    return appText;
                }
            }

            if (GetExact(resources, name, current) is { } packageText)
            {
                return packageText;
            }
        }

        return null;
    }
}
