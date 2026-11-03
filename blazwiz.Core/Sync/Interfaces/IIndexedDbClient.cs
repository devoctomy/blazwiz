using blazwiz.Core.Sync.IndexedDb;

namespace blazwiz.Core.Sync.Interfaces;

/// <summary>
/// Generic access to the browser's IndexedDB, through <c>wwwroot/js/indexeddb.js</c>. Records are serialised with
/// camel case property names, and <c>byte[]</c> properties are stored as binary.
/// </summary>
public interface IIndexedDbClient
{
    /// <summary>
    /// Gets a record by key.
    /// </summary>
    /// <typeparam name="T">The record type.</typeparam>
    /// <param name="store">The store.</param>
    /// <param name="key">The key: a value, or an array for a compound key.</param>
    /// <returns>The record, or default if there is none.</returns>
    Task<T?> GetAsync<T>(IndexedDbStore store, object key);

    /// <summary>
    /// Gets the records whose index value equals a key.
    /// </summary>
    /// <typeparam name="T">The record type.</typeparam>
    /// <param name="store">The store.</param>
    /// <param name="indexName">The index.</param>
    /// <param name="key">The index value: a value, or an array for a compound index.</param>
    /// <param name="omitFields">Properties (camel case) to leave out of the results, e.g. large content.</param>
    /// <returns>The records, in index order.</returns>
    Task<IReadOnlyList<T>> GetAllByIndexAsync<T>(IndexedDbStore store, string indexName, object key, IReadOnlyList<string>? omitFields = null);

    /// <summary>
    /// Adds or replaces a record.
    /// </summary>
    /// <typeparam name="T">The record type.</typeparam>
    /// <param name="store">The store.</param>
    /// <param name="value">The record.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task PutAsync<T>(IndexedDbStore store, T value);

    /// <summary>
    /// Adds or replaces records, in one transaction.
    /// </summary>
    /// <typeparam name="T">The record type.</typeparam>
    /// <param name="store">The store.</param>
    /// <param name="values">The records.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task PutManyAsync<T>(IndexedDbStore store, IEnumerable<T> values);

    /// <summary>
    /// Gets the last record, in index order, whose index value is between two bounds (inclusive).
    /// </summary>
    /// <typeparam name="T">The record type.</typeparam>
    /// <param name="store">The store.</param>
    /// <param name="indexName">The index.</param>
    /// <param name="lower">The lower bound.</param>
    /// <param name="upper">The upper bound.</param>
    /// <returns>The record, or default if none is in range.</returns>
    Task<T?> GetLastByIndexAsync<T>(IndexedDbStore store, string indexName, object lower, object upper);

    /// <summary>
    /// Deletes records by key, in one transaction.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="keys">The keys.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task DeleteManyAsync(IndexedDbStore store, IEnumerable<object> keys);
}
