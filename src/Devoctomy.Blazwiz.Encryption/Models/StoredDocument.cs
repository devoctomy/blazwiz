namespace Devoctomy.Blazwiz.Encryption.Models;

/// <summary>
/// A document as kept in a store, with the version it was read at.
/// </summary>
public sealed class StoredDocument
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StoredDocument"/> class.
    /// </summary>
    /// <param name="value">The document.</param>
    /// <param name="etag">The store's version of it.</param>
    public StoredDocument(string value, string etag)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentException.ThrowIfNullOrEmpty(etag);

        Value = value;
        ETag = etag;
    }

    /// <summary>
    /// Gets the document.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Gets the store's version of the document. Passed back when writing it, so the write fails if it changed in
    /// between.
    /// </summary>
    public string ETag { get; }
}
