namespace blazwiz.Core.Sync.IndexedDb;

/// <summary>
/// An IndexedDB object store: which database it is in, its key path and its indexes. The store (and the database)
/// is created the first time it is used, so stores can be added to an existing database without a migration.
/// </summary>
/// <param name="database">The database name.</param>
/// <param name="name">The object store name.</param>
/// <param name="keyPath">The property (or properties, for a compound key) the records are keyed by, in camel case.</param>
/// <param name="indexes">The indexes.</param>
public class IndexedDbStore(string database, string name, IReadOnlyList<string> keyPath, IReadOnlyList<IndexedDbIndex>? indexes = null)
{
    /// <summary>
    /// Gets the database name.
    /// </summary>
    public string Database { get; } = string.IsNullOrWhiteSpace(database) ? throw new ArgumentException("A database name is required.", nameof(database)) : database;

    /// <summary>
    /// Gets the object store name.
    /// </summary>
    public string Name { get; } = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("A store name is required.", nameof(name)) : name;

    /// <summary>
    /// Gets the key path. One property for a simple key, several for a compound key.
    /// </summary>
    public IReadOnlyList<string> KeyPath { get; } = keyPath is { Count: > 0 } ? keyPath : throw new ArgumentException("A key path is required.", nameof(keyPath));

    /// <summary>
    /// Gets the indexes.
    /// </summary>
    public IReadOnlyList<IndexedDbIndex> Indexes { get; } = indexes ?? [];
}
