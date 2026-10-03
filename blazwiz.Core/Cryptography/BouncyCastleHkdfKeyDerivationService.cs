using blazwiz.Core.Cryptography.Interfaces;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;

namespace blazwiz.Core.Cryptography;

/// <summary>
/// Implementation of <see cref="IKeyDerivationService"/> using Bouncy Castle HKDF (RFC 5869) with SHA-256.
/// </summary>
public class BouncyCastleHkdfKeyDerivationService : IKeyDerivationService
{
    /// <summary>
    /// The longest key HKDF-SHA-256 can derive: 255 times the 32 byte hash length.
    /// </summary>
    public const int MaxKeyLength = 255 * 32;

    /// <inheritdoc/>
    public byte[] DeriveKey(byte[] inputKeyMaterial, byte[] salt, byte[] info, int length)
    {
        ArgumentNullException.ThrowIfNull(inputKeyMaterial);
        ArgumentNullException.ThrowIfNull(salt);
        ArgumentNullException.ThrowIfNull(info);
        if (inputKeyMaterial.Length == 0)
        {
            throw new ArgumentException("The input key material must not be empty.", nameof(inputKeyMaterial));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(length, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(length, MaxKeyLength);

        var generator = new HkdfBytesGenerator(new Sha256Digest());
        generator.Init(new HkdfParameters(inputKeyMaterial, salt, info));

        var key = new byte[length];
        generator.GenerateBytes(key, 0, length);
        return key;
    }
}
