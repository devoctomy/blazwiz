using Devoctomy.Blazwiz.Encryption.Models;

namespace Devoctomy.Blazwiz.Encryption.Interfaces;

/// <summary>
/// The key rings of this session, one per key scope. The account's ring is also registered as
/// <see cref="IE2eKeyRing"/>.
/// </summary>
public interface IE2eKeyRings
{
    /// <summary>
    /// Gets a scope's key ring, creating it (locked, writing plaintext) the first time.
    /// </summary>
    /// <param name="scope">The key scope.</param>
    /// <returns>The scope's key ring.</returns>
    IE2eKeyRing Get(E2eKeyScope scope);
}
