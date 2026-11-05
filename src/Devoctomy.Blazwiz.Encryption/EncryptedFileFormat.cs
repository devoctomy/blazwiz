using System.Security.Cryptography;
using System.Text;
using Devoctomy.Blazwiz.Cryptography.Interfaces;
using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;

namespace Devoctomy.Blazwiz.Encryption;

/// <summary>
/// Implementation of <see cref="IEncryptedFileFormat"/> using an <see cref="IAeadCipher"/>.
/// </summary>
/// <param name="cipher">The AEAD cipher.</param>
public class EncryptedFileFormat(IAeadCipher cipher)
    : IEncryptedFileFormat
{
    /// <summary>
    /// The format version written.
    /// </summary>
    public const byte Version = 1;

    /// <summary>
    /// The longest key id, and the longest key scope id, in UTF-8 bytes.
    /// </summary>
    public const int MaxKeyIdLength = byte.MaxValue;

    private static readonly byte[] Magic = "E2EE"u8.ToArray();
    private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

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
    public byte[] Encrypt(ReadOnlySpan<byte> plaintext, string keyId, byte[] key, E2eFileBinding binding)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyId);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(binding);

        var keyIdField = ToLengthPrefixed(keyId, nameof(keyId));
        var scopeField = ToLengthPrefixed(binding.ScopeId, nameof(binding));
        var nonce = RandomNumberGenerator.GetBytes(cipher.NonceSize);

        var header = new byte[Magic.Length + 1 + keyIdField.Length + scopeField.Length + nonce.Length];
        var position = 0;
        Magic.CopyTo(header, position);
        position += Magic.Length;
        header[position++] = Version;
        keyIdField.CopyTo(header, position);
        position += keyIdField.Length;
        scopeField.CopyTo(header, position);
        position += scopeField.Length;
        nonce.CopyTo(header, position);

        var ciphertext = cipher.Encrypt(key, nonce, plaintext, AssociatedData(header, binding.Path));
        var output = new byte[header.Length + ciphertext.Length];
        header.CopyTo(output, 0);
        ciphertext.CopyTo(output, header.Length);
        return output;
    }

    /// <inheritdoc/>
    public byte[] Decrypt(ReadOnlySpan<byte> content, byte[] key, E2eFileBinding binding)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(binding);
        if (!TryReadHeader(content, out var header))
        {
            throw new InvalidDataException("The content is not an encrypted file.");
        }

        if (!string.Equals(header.ScopeId, binding.ScopeId, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The encrypted file belongs to another profile.");
        }

        var headerBytes = content[..header.Length];
        return cipher.Decrypt(key, headerBytes[^cipher.NonceSize..], content[header.Length..], AssociatedData(headerBytes, binding.Path));
    }

    private static byte[] AssociatedData(ReadOnlySpan<byte> header, string path)
    {
        var pathBytes = Encoding.UTF8.GetBytes(path);
        var data = new byte[header.Length + pathBytes.Length];
        header.CopyTo(data);
        pathBytes.CopyTo(data, header.Length);
        return data;
    }

    private static byte[] ToLengthPrefixed(string value, string parameterName)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        if (bytes.Length > MaxKeyIdLength)
        {
            throw new ArgumentException($"The value must be at most {MaxKeyIdLength} bytes.", parameterName);
        }

        var output = new byte[bytes.Length + 1];
        output[0] = (byte)bytes.Length;
        bytes.CopyTo(output, 1);
        return output;
    }

    private static bool TryReadString(ReadOnlySpan<byte> content, ref int position, out string? value)
    {
        value = null;
        if (content.Length <= position)
        {
            return false;
        }

        var length = content[position];
        if (length == 0 || content.Length < position + 1 + length)
        {
            return false;
        }

        try
        {
            value = StrictUtf8.GetString(content.Slice(position + 1, length));
        }
        catch (DecoderFallbackException)
        {
            return false;
        }

        position += 1 + length;
        return true;
    }

    private bool TryReadHeader(ReadOnlySpan<byte> content, out Header header)
    {
        header = default;
        if (content.Length < Magic.Length + 1 || !content.StartsWith(Magic) || content[Magic.Length] != Version)
        {
            return false;
        }

        var position = Magic.Length + 1;
        if (!TryReadString(content, ref position, out var keyId) || !TryReadString(content, ref position, out var scopeId))
        {
            return false;
        }

        var length = position + cipher.NonceSize;
        if (content.Length < length + cipher.TagSize)
        {
            return false;
        }

        header = new Header(keyId!, scopeId!, length);
        return true;
    }

    private readonly struct Header(string keyId, string scopeId, int length)
    {
        public string KeyId { get; } = keyId;

        public string ScopeId { get; } = scopeId;

        public int Length { get; } = length;
    }
}
