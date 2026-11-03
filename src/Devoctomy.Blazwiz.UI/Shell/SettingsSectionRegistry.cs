using Devoctomy.Blazwiz.UI.Shell.Interfaces;

namespace Devoctomy.Blazwiz.UI.Shell;

/// <inheritdoc/>
public class SettingsSectionRegistry(IEnumerable<SettingsSectionRegistration> registrations)
    : ISettingsSectionRegistry
{
    private readonly IReadOnlyList<Type> sections = (registrations ?? throw new ArgumentNullException(nameof(registrations)))
        .OrderBy(r => r.Order)
        .Select(r => r.ComponentType)
        .ToList();

    /// <inheritdoc/>
    public IReadOnlyList<Type> GetSections() => sections;
}
