using System.Security.Cryptography;
using blazwiz.Core.Sync.Interfaces;

namespace blazwiz.Core.Sync;

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
