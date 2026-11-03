using System.Collections.Concurrent;
using System.Reflection;
using System.Resources;
using Microsoft.Extensions.Localization;

namespace Devoctomy.Blazwiz.Core.Text;

/// <summary>
/// Creates the localizers behind <c>IStringLocalizer&lt;T&gt;</c>: each package's resources (a <c>.resx</c> named
/// after <c>T</c>, next to it), with the app's overrides on top (see <see cref="OverridingStringLocalizer"/>).
/// </summary>
public class OverridingStringLocalizerFactory(TextOptions options)
    : IStringLocalizerFactory
{
    private readonly TextOptions options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly ConcurrentDictionary<string, IStringLocalizer> localizers = new ConcurrentDictionary<string, IStringLocalizer>(StringComparer.Ordinal);

    /// <inheritdoc/>
    public IStringLocalizer Create(Type resourceSource)
    {
        ArgumentNullException.ThrowIfNull(resourceSource);
        return localizers.GetOrAdd(
            resourceSource.FullName!,
            name => new OverridingStringLocalizer(new ResourceManager(name, resourceSource.Assembly), options));
    }

    /// <inheritdoc/>
    public IStringLocalizer Create(string baseName, string location)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseName);
        ArgumentException.ThrowIfNullOrWhiteSpace(location);
        return localizers.GetOrAdd(
            baseName,
            name => new OverridingStringLocalizer(new ResourceManager(name, Assembly.Load(new AssemblyName(location))), options));
    }
}
