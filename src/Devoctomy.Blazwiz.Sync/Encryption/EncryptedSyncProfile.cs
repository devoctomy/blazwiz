using Devoctomy.Blazwiz.Sync.Interfaces;

namespace Devoctomy.Blazwiz.Sync.Encryption;

/// <summary>
/// A profile whose files can be end to end encrypted (<c>UseEncryption</c>), with the store app code is given (which
/// encrypts and decrypts) and its index. One is registered per such profile.
/// </summary>
/// <param name="name">The profile name.</param>
/// <param name="fileStore">The store app code is given: encrypting, over the tracking store.</param>
/// <param name="indexStore">The profile's sync index.</param>
public class EncryptedSyncProfile(string name, ISyncFileStore fileStore, ISyncIndexStore indexStore)
{
    /// <summary>
    /// Gets the profile name.
    /// </summary>
    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

    /// <summary>
    /// Gets the store app code is given.
    /// </summary>
    public ISyncFileStore FileStore { get; } = fileStore ?? throw new ArgumentNullException(nameof(fileStore));

    /// <summary>
    /// Gets the profile's sync index.
    /// </summary>
    public ISyncIndexStore IndexStore { get; } = indexStore ?? throw new ArgumentNullException(nameof(indexStore));
}
