using Devoctomy.Blazwiz.Cryptography.Models;
using Devoctomy.Blazwiz.Encryption.Models;

namespace Devoctomy.Blazwiz.Encryption.Interfaces;

/// <summary>
/// The cryptography of a keys document (<see cref="UserKeysDocument"/>), for any key scope: creating one from a
/// passphrase, reading it, unwrapping its master key with the passphrase, rotating its keys for a new passphrase, and
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
    /// Rotates a document's keys for a new passphrase: a new random master key, wrapped by the key derived from the new
    /// passphrase (with a new salt and the given Argon2id costs), and a new active file key. Every existing file key is
    /// wrapped again under the new master key, so files encrypted with them still decrypt, and the old active key is
    /// retired. The document is marked <see cref="UserKeysDocument.RotationInProgress"/> until the files are encrypted
    /// again.
    /// </summary>
    /// <param name="document">The document to change.</param>
    /// <param name="masterKey">The document's current raw master key.</param>
    /// <param name="passphrase">The new passphrase.</param>
    /// <param name="parameters">The Argon2id costs, or null for the app's.</param>
    /// <param name="progress">Told when the key is derived and when the keys are created.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The changed document and its new raw keys; dispose it to wipe them.</returns>
    /// <exception cref="InvalidDataException">A file key does not belong to the master key.</exception>
    Task<E2eNewKeys> RotateAsync(UserKeysDocument document, byte[] masterKey, string passphrase, Argon2idParameters? parameters = null, IProgress<E2eProgress>? progress = null, CancellationToken ct = default);

    /// <summary>
    /// Unwraps a document's file keys with its master key and loads them, with the master key, into a key ring.
    /// </summary>
    /// <param name="keyRing">The key ring.</param>
    /// <param name="document">The document.</param>
    /// <param name="masterKey">The raw master key.</param>
    /// <exception cref="InvalidDataException">A file key does not belong to the master key.</exception>
    void LoadFileKeys(IE2eKeyRing keyRing, UserKeysDocument document, byte[] masterKey);
}
