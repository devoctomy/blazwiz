using System.Security.Cryptography;
using System.Text;
using Devoctomy.Blazwiz.Cryptography.Interfaces;
using Devoctomy.Blazwiz.Encryption.Interfaces;

namespace Devoctomy.Blazwiz.Encryption;

/// <summary>
/// Implementation of <see cref="IEncryptedFileFormat"/> (format version 1) using an <see cref="IAeadCipher"/>.
/// </summary>
public class EncryptedFileFormat(IAeadCipher cipher)
    : IEncryptedFileFormat
{
    /// <summary>
    /// The format version written.
    /// </summary>
    public const byte Version = 1;

    /// <summary>
    /// The longest key id, in UTF-8 bytes.
    /// </summary>
    public const int MaxKeyIdLength = byte.MaxValue;

    private static readonly byte[] Magic = "E2EE"u8.ToArray();

    private readonly IAeadCipher cipher = cipher ?? throw new ArgumentNullException(nameof(cipher));

    /// <summary>
    /// Gets the magic bytes every encrypted file starts with.
    /// </summary>
    public static ReadOnlySpan<byte> MagicBytes => Magic;

    /// <inheritdoc/>
    public bool TryGetKeyId(ReadOnlySpan<byte> content, out string? keyId)
    {
        keyId = TryReadHeader(content, out var header) ? header.KeyId : null;
        return keyId != null;
    }

    /// <inheritdoc/>
    public byte[] Encrypt(ReadOnlySpan<byte> plaintext, string keyId, byte[] key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyId);
        ArgumentNullException.ThrowIfNull(key);

        var keyIdBytes = Encoding.UTF8.GetBytes(keyId);
        if (keyIdBytes.Length > MaxKeyIdLength)
        {
            throw new ArgumentException($"The key id must be at most {MaxKeyIdLength} bytes.", nameof(keyId));
        }

        var nonce = RandomNumberGenerator.GetBytes(cipher.NonceSize);
        var headerLength = Magic.Length + 2 + keyIdBytes.Length + nonce.Length;
        var header = new byte[headerLength];
        Magic.CopyTo(header, 0);
        header[Magic.Length] = Version;
        header[Magic.Length + 1] = (byte)keyIdBytes.Length;
        keyIdBytes.CopyTo(header, Magic.Length + 2);
        nonce.CopyTo(header, Magic.Length + 2 + keyIdBytes.Length);

        var ciphertext = cipher.Encrypt(key, nonce, plaintext, header);
        var output = new byte[header.Length + ciphertext.Length];
        header.CopyTo(output, 0);
        ciphertext.CopyTo(output, header.Length);
        return output;
    }

    /// <inheritdoc/>
    public byte[] Decrypt(ReadOnlySpan<byte> content, byte[] key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (!TryReadHeader(content, out var header))
        {
            throw new InvalidDataException("The content is not an encrypted file.");
        }

        var headerBytes = content[..header.Length];
        var nonce = headerBytes[^cipher.NonceSize..];
        return cipher.Decrypt(key, nonce, content[header.Length..], headerBytes);
    }

    private bool TryReadHeader(ReadOnlySpan<byte> content, out Header header)
    {
        header = default;
        var fixedLength = Magic.Length + 2;
        if (content.Length < fixedLength || !content.StartsWith(Magic) || content[Magic.Length] != Version)
        {
            return false;
        }

        var keyIdLength = content[Magic.Length + 1];
        var length = fixedLength + keyIdLength + cipher.NonceSize;
        if (keyIdLength == 0 || content.Length < length + cipher.TagSize)
        {
            return false;
        }

        string keyId;
        try
        {
            keyId = new UTF8Encoding(false, true).GetString(content.Slice(fixedLength, keyIdLength));
        }
        catch (DecoderFallbackException)
        {
            return false;
        }

        header = new Header(keyId, length);
        return true;
    }

    private readonly struct Header(string keyId, int length)
    {
        public string KeyId { get; } = keyId;

        public int Length { get; } = length;
    }
}
