using System.Diagnostics.CodeAnalysis;
using Devoctomy.Blazwiz.Encryption.Models;

namespace Devoctomy.Blazwiz.Encryption.Interfaces;

/// <summary>
/// The unlocked end to end encryption keys of one key scope (one profile) for this session. The raw keys are only ever
/// held in memory (in the <see cref="Cryptography.Interfaces.IDerivedKeyStore"/>) and never written anywhere. While
/// it holds no keys, the profile is locked: its files can be neither read nor written.
/// </summary>
/// <remarks>
/// Set by <see cref="IE2eProtectedProfileManager"/>; read by the encrypting file store.
/// </remarks>
public interface IE2eKeyRing
{
    /// <summary>
    /// Gets the key scope the keys belong to.
    /// </summary>
    E2eKeyScope Scope { get; }

    /// <summary>
    /// Gets the id of the file key new files are encrypted with, or null when locked.
    /// </summary>
    string? ActiveKeyId { get; }

    /// <summary>
    /// Holds the unlocked keys, replacing any held.
    /// </summary>
    /// <param name="masterKey">The master key.</param>
    /// <param name="fileKeys">Every file key by id.</param>
    /// <param name="activeKeyId">The id of the key new files are encrypted with.</param>
    void Load(byte[] masterKey, IReadOnlyDictionary<string, byte[]> fileKeys, string activeKeyId);

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
    /// Wipes and forgets the keys: the profile is locked.
    /// </summary>
    void Clear();
}
