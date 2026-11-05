using Devoctomy.Blazwiz.Cryptography.Models;
using Devoctomy.Blazwiz.Encryption.Models;

namespace Devoctomy.Blazwiz.Encryption.Interfaces;

/// <summary>
/// The cryptography of a keys document (<see cref="UserKeysDocument"/>), for any key scope: creating one from a
/// passphrase, reading it, unwrapping its master key with the passphrase, wrapping it again for a new passphrase, and
/// loading its file keys into a key ring.
/// </summary>
public interface IE2eKeysDocumentService
{
    /// <summary>
    /// Creates a keys document for a new passphrase: a random master key wrapped by the key derived from the
    /// passphrase, and one active file key wrapped by the master key.
    /// </summary>
    /// <param name="passphrase">The passphrase.</param>
    /// <param name="parameters">The Argon2id costs, or null for the app's.</param>
    /// <param name="progress">Told when the key is derived and when the keys are created.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The document and its raw keys; dispose it to wipe them.</returns>
    Task<E2eNewKeys> CreateAsync(string passphrase, Argon2idParameters? parameters = null, IProgress<E2eProgress>? progress = null, CancellationToken ct = default);

    /// <summary>
    /// Reads and checks a keys document.
    /// </summary>
    /// <param name="json">The document, as JSON.</param>
    /// <returns>The checked document.</returns>
    /// <exception cref="InvalidDataException">The document is not valid or not supported.</exception>
    UserKeysDocument Parse(string json);

    /// <summary>
    /// Writes a keys document.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <returns>The JSON.</returns>
    string Serialize(UserKeysDocument document);

    /// <summary>
    /// Unwraps a document's master key with the passphrase.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="passphrase">The passphrase.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The raw master key; the caller wipes it.</returns>
    /// <exception cref="Exceptions.E2eWrongPassphraseException">The passphrase is wrong.</exception>
    /// <exception cref="InvalidDataException">The document is not valid.</exception>
    Task<byte[]> UnwrapMasterKeyAsync(UserKeysDocument document, string passphrase, CancellationToken ct = default);

    /// <summary>
    /// Wraps a document's master key for a new passphrase, with a new salt and the current Argon2id costs. The file
    /// keys are not changed.
    /// </summary>
    /// <param name="document">The document to change.</param>
    /// <param name="masterKey">The raw master key.</param>
    /// <param name="passphrase">The new passphrase.</param>
    /// <param name="parameters">The Argon2id costs, or null for the app's.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task RewrapMasterKeyAsync(UserKeysDocument document, byte[] masterKey, string passphrase, Argon2idParameters? parameters = null, CancellationToken ct = default);

    /// <summary>
    /// Unwraps a document's file keys with its master key and loads them, with the master key, into a key ring.
    /// </summary>
    /// <param name="keyRing">The key ring.</param>
    /// <param name="document">The document.</param>
    /// <param name="masterKey">The raw master key.</param>
    /// <exception cref="InvalidDataException">A file key does not belong to the master key.</exception>
    void LoadFileKeys(IE2eKeyRing keyRing, UserKeysDocument document, byte[] masterKey);
}
