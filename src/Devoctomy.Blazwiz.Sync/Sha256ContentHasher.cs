using System.Security.Cryptography;
using Devoctomy.Blazwiz.Sync.Interfaces;

namespace Devoctomy.Blazwiz.Sync;

/// <summary>
/// Hashes content with SHA-256 (<see cref="SHA256"/>).
/// </summary>
public class Sha256ContentHasher : IContentHasher
{
    /// <inheritdoc/>
    public string ComputeHash(byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);

        return Convert.ToHexStringLower(SHA256.HashData(content));
    }
}
