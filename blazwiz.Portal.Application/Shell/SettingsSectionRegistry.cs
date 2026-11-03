using blazwiz.Portal.Application.Shell.Interfaces;

namespace blazwiz.Portal.Application.Shell;

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
