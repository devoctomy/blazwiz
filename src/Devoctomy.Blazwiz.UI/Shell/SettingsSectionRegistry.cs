using Devoctomy.Blazwiz.UI.Shell.Interfaces;

namespace Devoctomy.Blazwiz.UI.Shell;

/// <inheritdoc/>
public class SettingsSectionRegistry(IEnumerable<SettingsSectionRegistration> registrations, IEnumerable<SettingsSectionRemoval> removals)
    : ISettingsSectionRegistry
{
    private readonly IReadOnlyList<Type> sections = GetShown(
        registrations ?? throw new ArgumentNullException(nameof(registrations)),
        removals ?? throw new ArgumentNullException(nameof(removals)));

    /// <inheritdoc/>
    public IReadOnlyList<Type> GetSections() => sections;

    private static List<Type> GetShown(IEnumerable<SettingsSectionRegistration> registrations, IEnumerable<SettingsSectionRemoval> removals)
    {
        var removed = removals.Select(r => r.ComponentType).ToHashSet();
        return registrations
            .Where(r => !removed.Contains(r.ComponentType))
            .OrderBy(r => r.Order)
            .Select(r => r.ComponentType)
            .ToList();
    }
}
