using System.Diagnostics.CodeAnalysis;
using Devoctomy.Blazwiz.Encryption.Models;

namespace Devoctomy.Blazwiz.Encryption.Interfaces;

/// <summary>
/// The unlocked end to end encryption keys for this session, and what to do with files written. The raw keys are only
/// ever held in memory (in the <see cref="Cryptography.Interfaces.IDerivedKeyStore"/>) and never written anywhere.
/// </summary>
/// <remarks>
/// Set by <see cref="IE2eKeyManager"/>; read by the encrypting file store.
/// </remarks>
public interface IE2eKeyRing
{
    /// <summary>
    /// Gets what happens to files written.
    /// </summary>
    E2eWriteMode WriteMode { get; }

    /// <summary>
    /// Gets the id of the file key new files are encrypted with, or null when locked.
    /// </summary>
    string? ActiveKeyId { get; }

    /// <summary>
    /// Holds the unlocked keys, replacing any held, and encrypts files written from now on.
    /// </summary>
    /// <param name="masterKey">The master key.</param>
    /// <param name="fileKeys">Every file key by id.</param>
    /// <param name="activeKeyId">The id of the key new files are encrypted with.</param>
    void Load(byte[] masterKey, IReadOnlyDictionary<string, byte[]> fileKeys, string activeKeyId);

    /// <summary>
    /// Changes what happens to files written, keeping the keys, e.g. <see cref="E2eWriteMode.Plaintext"/> while files
    /// are being decrypted to turn encryption off.
    /// </summary>
    /// <param name="mode">The mode.</param>
    /// <exception cref="InvalidOperationException">Encrypting without the keys.</exception>
    void SetWriteMode(E2eWriteMode mode);

    /// <summary>
    /// Gets a copy of a file key.
    /// </summary>
    /// <param name="keyId">The key id.</param>
    /// <param name="key">A copy of the key, or null when it is not held.</param>
    /// <returns>True when the key is held.</returns>
    bool TryGetFileKey(string keyId, [NotNullWhen(true)] out byte[]? key);

    /// <summary>
    /// Gets a copy of the master key.
    /// </summary>
    /// <param name="key">A copy of the key, or null when it is not held.</param>
    /// <returns>True when the key is held.</returns>
    bool TryGetMasterKey([NotNullWhen(true)] out byte[]? key);

    /// <summary>
    /// Wipes and forgets the keys, and sets what happens to files written from now on.
    /// </summary>
    /// <param name="mode"><see cref="E2eWriteMode.Plaintext"/> or <see cref="E2eWriteMode.Refuse"/>.</param>
    void Clear(E2eWriteMode mode);
}
