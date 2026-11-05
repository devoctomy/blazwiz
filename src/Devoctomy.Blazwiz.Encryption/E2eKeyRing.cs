using System.Diagnostics.CodeAnalysis;
using Devoctomy.Blazwiz.Cryptography.Interfaces;
using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;

namespace Devoctomy.Blazwiz.Encryption;

/// <summary>
/// Implementation of <see cref="IE2eKeyRing"/> for one key scope, holding the keys in the <see cref="IDerivedKeyStore"/>
/// under names starting <see cref="E2eKeyNames.ScopePrefix"/> and the scope id, so they are wiped with the other session
/// keys on sign out. Created by <see cref="E2eKeyRings"/>.
/// </summary>
/// <param name="keyStore">Holds the raw keys.</param>
/// <param name="scope">The key scope.</param>
public class E2eKeyRing(IDerivedKeyStore keyStore, E2eKeyScope scope)
    : IE2eKeyRing
{
    private readonly IDerivedKeyStore keyStore = keyStore ?? throw new ArgumentNullException(nameof(keyStore));
    private readonly string masterKeyName = E2eKeyNames.ScopePrefix + (scope ?? throw new ArgumentNullException(nameof(scope))).Id + E2eKeyNames.MasterKeySuffix;
    private readonly string fileKeyPrefix = E2eKeyNames.ScopePrefix + scope.Id + E2eKeyNames.FileKeySuffix;
    private readonly Lock gate = new Lock();
    private readonly HashSet<string> fileKeyIds = new HashSet<string>(StringComparer.Ordinal);

    private E2eWriteMode writeMode = E2eWriteMode.Plaintext;
    private string? activeKeyId;

    /// <inheritdoc/>
    public E2eKeyScope Scope { get; } = scope;

    /// <inheritdoc/>
    public E2eWriteMode WriteMode
    {
        get
        {
            lock (gate)
            {
                return writeMode;
            }
        }
    }

    /// <inheritdoc/>
    public string? ActiveKeyId
    {
        get
        {
            lock (gate)
            {
                return activeKeyId;
            }
        }
    }

    /// <inheritdoc/>
    public void Load(byte[] masterKey, IReadOnlyDictionary<string, byte[]> fileKeys, string activeKeyId)
    {
        ArgumentNullException.ThrowIfNull(masterKey);
        ArgumentNullException.ThrowIfNull(fileKeys);
        ArgumentException.ThrowIfNullOrWhiteSpace(activeKeyId);
        if (!fileKeys.ContainsKey(activeKeyId))
        {
            throw new ArgumentException("The active key must be one of the file keys.", nameof(activeKeyId));
        }

        lock (gate)
        {
            RemoveKeys();
            keyStore.SetKey(masterKeyName, masterKey);
            foreach (var (id, key) in fileKeys)
            {
                keyStore.SetKey(fileKeyPrefix + id, key);
                fileKeyIds.Add(id);
            }

            this.activeKeyId = activeKeyId;
            writeMode = E2eWriteMode.Encrypt;
        }
    }

    /// <inheritdoc/>
    public void SetWriteMode(E2eWriteMode mode)
    {
        lock (gate)
        {
            if (mode == E2eWriteMode.Encrypt && activeKeyId == null)
            {
                throw new InvalidOperationException("Files cannot be encrypted until the keys are unlocked.");
            }

            writeMode = mode;
        }
    }

    /// <inheritdoc/>
    public bool TryGetFileKey(string keyId, [NotNullWhen(true)] out byte[]? key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyId);

        lock (gate)
        {
            if (fileKeyIds.Contains(keyId))
            {
                return keyStore.TryGetKey(fileKeyPrefix + keyId, out key);
            }
        }

        key = null;
        return false;
    }

    /// <inheritdoc/>
    public bool TryGetMasterKey([NotNullWhen(true)] out byte[]? key)
    {
        lock (gate)
        {
            if (activeKeyId != null)
            {
                return keyStore.TryGetKey(masterKeyName, out key);
            }
        }

        key = null;
        return false;
    }

    /// <inheritdoc/>
    public void Clear(E2eWriteMode mode)
    {
        if (mode == E2eWriteMode.Encrypt)
        {
            throw new ArgumentException("Files cannot be encrypted without the keys.", nameof(mode));
        }

        lock (gate)
        {
            RemoveKeys();
            writeMode = mode;
        }
    }

    private void RemoveKeys()
    {
        keyStore.RemoveKey(masterKeyName);
        foreach (var id in fileKeyIds)
        {
            keyStore.RemoveKey(fileKeyPrefix + id);
        }

        fileKeyIds.Clear();
        activeKeyId = null;
    }
}
