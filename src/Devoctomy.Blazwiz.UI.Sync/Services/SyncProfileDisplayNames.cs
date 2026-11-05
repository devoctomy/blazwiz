using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.UI.Sync.Services.Interfaces;
using Microsoft.Extensions.Localization;

namespace Devoctomy.Blazwiz.UI.Sync.Services;

/// <summary>
/// Implementation of <see cref="ISyncProfileDisplayNames"/>, reading the profiles from the registry and the labels of
/// the profiles the user created from <see cref="ISyncProtectedProfileService"/>. Read on every call, so the current
/// culture, the app's overrides and new labels apply.
/// </summary>
/// <param name="registry">The sync profiles.</param>
/// <param name="profiles">The user's protected profiles, with their labels on this device.</param>
/// <param name="localizerFactory">Creates the localizer of a profile's resources.</param>
public class SyncProfileDisplayNames(ISyncProfileRegistry registry, ISyncProtectedProfileService profiles, IStringLocalizerFactory localizerFactory)
    : ISyncProfileDisplayNames
{
    private readonly ISyncProfileRegistry registry = registry ?? throw new ArgumentNullException(nameof(registry));
    private readonly ISyncProtectedProfileService profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
    private readonly IStringLocalizerFactory localizerFactory = localizerFactory ?? throw new ArgumentNullException(nameof(localizerFactory));

    /// <inheritdoc/>
    public string GetDisplayName(string profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (registry.Find(profile) is { DisplayText: { } key, DisplayTextResource: { } resource })
        {
            return localizerFactory.Create(resource)[key].Value;
        }

        return profiles.Profiles.FirstOrDefault(p => p.Name == profile)?.Label ?? profile;
    }
}
