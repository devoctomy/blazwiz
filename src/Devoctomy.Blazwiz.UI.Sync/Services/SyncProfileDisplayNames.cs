using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.UI.Sync.Services.Interfaces;
using Microsoft.Extensions.Localization;

namespace Devoctomy.Blazwiz.UI.Sync.Services;

/// <summary>
/// Implementation of <see cref="ISyncProfileDisplayNames"/>, reading the profiles from the registry. Read on every call,
/// so the current culture and the app's overrides apply.
/// </summary>
/// <param name="registry">The sync profiles.</param>
/// <param name="localizerFactory">Creates the localizer of a profile's resources.</param>
public class SyncProfileDisplayNames(ISyncProfileRegistry registry, IStringLocalizerFactory localizerFactory)
    : ISyncProfileDisplayNames
{
    private readonly ISyncProfileRegistry registry = registry ?? throw new ArgumentNullException(nameof(registry));
    private readonly IStringLocalizerFactory localizerFactory = localizerFactory ?? throw new ArgumentNullException(nameof(localizerFactory));

    /// <inheritdoc/>
    public string GetDisplayName(string profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return registry.Find(profile) is { DisplayText: { } key, DisplayTextResource: { } resource }
            ? localizerFactory.Create(resource)[key].Value
            : profile;
    }
}
