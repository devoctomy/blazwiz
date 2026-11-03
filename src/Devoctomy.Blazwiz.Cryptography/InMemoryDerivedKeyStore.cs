using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using Devoctomy.Blazwiz.Cryptography.Interfaces;

namespace Devoctomy.Blazwiz.Cryptography;

/// <summary>
/// Implementation of <see cref="IDerivedKeyStore"/> holding the keys in memory. Callers get copies, so they cannot
/// change the stored keys, and keys are zeroed when replaced or cleared.
/// </summary>
public class InMemoryDerivedKeyStore : IDerivedKeyStore
{
    private readonly Dictionary<string, byte[]> keys = new Dictionary<string, byte[]>(StringComparer.Ordinal);
    private readonly Lock gate = new Lock();

    /// <inheritdoc/>
    public void SetKey(string name, byte[] key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(key);

        var copy = (byte[])key.Clone();
        lock (gate)
        {
            if (keys.TryGetValue(name, out var existing))
            {
                CryptographicOperations.ZeroMemory(existing);
            }

            keys[name] = copy;
        }
    }

    /// <inheritdoc/>
    public bool TryGetKey(string name, [NotNullWhen(true)] out byte[]? key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        lock (gate)
        {
            if (keys.TryGetValue(name, out var stored))
            {
                key = (byte[])stored.Clone();
                return true;
            }
        }

        key = null;
        return false;
    }

    /// <inheritdoc/>
    public bool RemoveKey(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        lock (gate)
        {
            if (!keys.Remove(name, out var existing))
            {
                return false;
            }

            CryptographicOperations.ZeroMemory(existing);
            return true;
        }
    }

    /// <inheritdoc/>
    public void Clear()
    {
        lock (gate)
        {
            foreach (var key in keys.Values)
            {
                CryptographicOperations.ZeroMemory(key);
            }

            keys.Clear();
        }
    }
}
