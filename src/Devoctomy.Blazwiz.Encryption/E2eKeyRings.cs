using System.Collections.Concurrent;
using Devoctomy.Blazwiz.Cryptography.Interfaces;
using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;

namespace Devoctomy.Blazwiz.Encryption;

/// <summary>
/// Implementation of <see cref="IE2eKeyRings"/>: an <see cref="E2eKeyRing"/> per key scope, all holding their keys in
/// the same <see cref="IDerivedKeyStore"/>.
/// </summary>
/// <param name="keyStore">Holds the raw keys.</param>
public class E2eKeyRings(IDerivedKeyStore keyStore)
    : IE2eKeyRings
{
    private readonly IDerivedKeyStore keyStore = keyStore ?? throw new ArgumentNullException(nameof(keyStore));
    private readonly ConcurrentDictionary<string, IE2eKeyRing> rings = new ConcurrentDictionary<string, IE2eKeyRing>(StringComparer.Ordinal);

    /// <inheritdoc/>
    public IE2eKeyRing Get(E2eKeyScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        return rings.GetOrAdd(scope.Id, _ => new E2eKeyRing(keyStore, scope));
    }
}
