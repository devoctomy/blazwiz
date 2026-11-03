using blazwiz.Portal.Application.Shell.Interfaces;

namespace blazwiz.Portal.Application.Shell;

/// <inheritdoc/>
public class PageRegistry(IEnumerable<PageRegistration> registrations)
    : IPageRegistry
{
    // Later registrations replace earlier ones, so the app's choices win over the framework's defaults.
    private readonly IReadOnlyDictionary<string, Type?> pages = (registrations ?? throw new ArgumentNullException(nameof(registrations)))
        .GroupBy(r => r.Key, StringComparer.Ordinal)
        .ToDictionary(g => g.Key, g => g.Last().ComponentType, StringComparer.Ordinal);

    /// <inheritdoc/>
    public Type? GetComponent(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return pages.TryGetValue(key, out var type) ? type : null;
    }

    /// <inheritdoc/>
    public bool IsEnabled(string key) => GetComponent(key) != null;
}
