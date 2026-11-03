namespace Devoctomy.Blazwiz.Sync.IndexedDb;

/// <summary>
/// An index on an IndexedDB object store.
/// </summary>
/// <param name="name">The index name.</param>
/// <param name="keyPath">The property (or properties, for a compound index) indexed, in camel case.</param>
public class IndexedDbIndex(string name, IReadOnlyList<string> keyPath)
{
    /// <summary>
    /// Gets the index name.
    /// </summary>
    public string Name { get; } = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("An index name is required.", nameof(name)) : name;

    /// <summary>
    /// Gets the key path.
    /// </summary>
    public IReadOnlyList<string> KeyPath { get; } = keyPath is { Count: > 0 } ? keyPath : throw new ArgumentException("A key path is required.", nameof(keyPath));
}
