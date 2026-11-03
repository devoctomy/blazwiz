using System.Security.Cryptography;
using Devoctomy.Blazwiz.Cryptography.Interfaces;
using Devoctomy.Blazwiz.Cryptography.Models;

namespace Devoctomy.Blazwiz.Cryptography;

/// <summary>
/// Implementation of <see cref="IKeyWrapService"/> wrapping keys with AES-256-GCM (<see cref="IAeadCipher"/>) and a new
/// 12 byte random nonce each time.
/// </summary>
public class AeadKeyWrapService(IAeadCipher cipher)
    : IKeyWrapService
{
    private readonly IAeadCipher cipher = cipher ?? throw new ArgumentNullException(nameof(cipher));

    /// <inheritdoc/>
    public byte[] CreateKey(int? length = null)
    {
        var size = length ?? cipher.KeySize;
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 1, nameof(length));
        return RandomNumberGenerator.GetBytes(size);
    }

    /// <inheritdoc/>
    public WrappedKey Wrap(byte[] keyEncryptionKey, byte[] key, byte[]? associatedData = null)
    {
        ArgumentNullException.ThrowIfNull(keyEncryptionKey);
        ArgumentNullException.ThrowIfNull(key);

        var nonce = RandomNumberGenerator.GetBytes(cipher.NonceSize);
        var wrapped = cipher.Encrypt(keyEncryptionKey, nonce, key, associatedData);
        return new WrappedKey
        {
            Algorithm = BouncyCastleAesGcmAeadCipher.AlgorithmName,
            Nonce = Convert.ToBase64String(nonce),
            Wrapped = Convert.ToBase64String(wrapped),
        };
    }

    /// <inheritdoc/>
    public byte[] Unwrap(byte[] keyEncryptionKey, WrappedKey wrappedKey, byte[]? associatedData = null)
    {
        ArgumentNullException.ThrowIfNull(keyEncryptionKey);
        ArgumentNullException.ThrowIfNull(wrappedKey);
        if (!string.Equals(wrappedKey.Algorithm, BouncyCastleAesGcmAeadCipher.AlgorithmName, StringComparison.Ordinal))
        {
            throw new NotSupportedException($"Keys wrapped with '{wrappedKey.Algorithm}' are not supported.");
        }

        var nonce = Convert.FromBase64String(wrappedKey.Nonce);
        var wrapped = Convert.FromBase64String(wrappedKey.Wrapped);
        if (nonce.Length != cipher.NonceSize)
        {
            throw new FormatException($"The nonce must be {cipher.NonceSize} bytes.");
        }

        return cipher.Decrypt(keyEncryptionKey, nonce, wrapped, associatedData);
    }
}
