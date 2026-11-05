using System.Security.Cryptography;

namespace Devoctomy.Blazwiz.Encryption.Models;

/// <summary>
/// A new keys document and its raw keys, made by <see cref="Interfaces.IE2eKeysDocumentService.CreateAsync"/>.
/// Disposing it wipes the raw keys.
/// </summary>
/// <param name="document">The keys document.</param>
/// <param name="masterKey">The raw master key.</param>
/// <param name="fileKeyId">The id of the active file key.</param>
/// <param name="fileKey">The raw active file key.</param>
public sealed class E2eNewKeys(UserKeysDocument document, byte[] masterKey, string fileKeyId, byte[] fileKey)
    : IDisposable
{
    /// <summary>
    /// Gets the keys document.
    /// </summary>
    public UserKeysDocument Document { get; } = document ?? throw new ArgumentNullException(nameof(document));

    /// <summary>
    /// Gets the raw master key.
    /// </summary>
    public byte[] MasterKey { get; } = masterKey ?? throw new ArgumentNullException(nameof(masterKey));

    /// <summary>
    /// Gets the id of the active file key.
    /// </summary>
    public string FileKeyId { get; } = fileKeyId ?? throw new ArgumentNullException(nameof(fileKeyId));

    /// <summary>
    /// Gets the raw active file key.
    /// </summary>
    public byte[] FileKey { get; } = fileKey ?? throw new ArgumentNullException(nameof(fileKey));

    /// <inheritdoc/>
    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(MasterKey);
        CryptographicOperations.ZeroMemory(FileKey);
    }
}
