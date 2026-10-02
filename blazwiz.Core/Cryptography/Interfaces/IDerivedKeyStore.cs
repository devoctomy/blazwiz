using System.Diagnostics.CodeAnalysis;

namespace blazwiz.Core.Cryptography.Interfaces;

/// <summary>
/// Holds derived keys in memory by name. Nothing is persisted, so the keys are gone when the app closes.
/// </summary>
public interface IDerivedKeyStore
{
    /// <summary>
    /// Stores a key, replacing (and wiping) any key already held under the name. The store keeps its own copy.
    /// </summary>
    /// <param name="name">The key name.</param>
    /// <param name="key">The key.</param>
    void SetKey(string name, byte[] key);

    /// <summary>
    /// Gets a copy of a key.
    /// </summary>
    /// <param name="name">The key name.</param>
    /// <param name="key">A copy of the key, or <see langword="null"/> when none is held under the name.</param>
    /// <returns><see langword="true"/> when the key is held.</returns>
    bool TryGetKey(string name, [NotNullWhen(true)] out byte[]? key);

    /// <summary>
    /// Wipes and forgets every key.
    /// </summary>
    void Clear();
}
