using blazwiz.Portal.Application.Shell.Interfaces;

namespace blazwiz.Portal.Application.Shell;

/// <inheritdoc/>
public class ShellRegistry(IEnumerable<ShellComponentRegistration> registrations)
    : IShellRegistry
{
    private readonly IReadOnlyList<ShellComponentRegistration> registrations =
        (registrations ?? throw new ArgumentNullException(nameof(registrations))).ToList();

    /// <inheritdoc/>
    public IReadOnlyList<Type> GetComponents(ShellRegion region)
    {
        return registrations
            .Where(r => r.Region == region)
            .OrderBy(r => r.Order)
            .Select(r => r.ComponentType)
            .ToList();
    }
}
