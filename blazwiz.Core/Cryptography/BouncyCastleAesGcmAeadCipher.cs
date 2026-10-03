using System.Security.Cryptography;
using blazwiz.Core.Cryptography.Interfaces;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using IAeadCipher = blazwiz.Core.Cryptography.Interfaces.IAeadCipher;

namespace blazwiz.Core.Cryptography;

/// <summary>
/// Implementation of <see cref="IAeadCipher"/> using Bouncy Castle AES-256-GCM with a 12 byte nonce and a 16 byte tag.
/// Bouncy Castle is used because the browser (WebAssembly) has no native AES-GCM in .NET.
/// </summary>
public class BouncyCastleAesGcmAeadCipher : IAeadCipher
{
    /// <summary>
    /// The algorithm name, as recorded alongside wrapped keys.
    /// </summary>
    public const string AlgorithmName = "aes-256-gcm";

    /// <inheritdoc/>
    public int KeySize => 32;

    /// <inheritdoc/>
    public int NonceSize => 12;

    /// <inheritdoc/>
    public int TagSize => 16;

    /// <inheritdoc/>
    public byte[] Encrypt(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> associatedData = default)
    {
        var cipher = CreateCipher(true, key, nonce, associatedData);
        var output = new byte[cipher.GetOutputSize(plaintext.Length)];
        var length = cipher.ProcessBytes(plaintext, output);
        cipher.DoFinal(output.AsSpan(length));
        return output;
    }

    /// <inheritdoc/>
    public byte[] Decrypt(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> associatedData = default)
    {
        if (ciphertext.Length < TagSize)
        {
            throw new AuthenticationTagMismatchException("The ciphertext is too short to hold the authentication tag.");
        }

        var cipher = CreateCipher(false, key, nonce, associatedData);
        var output = new byte[cipher.GetOutputSize(ciphertext.Length)];
        try
        {
            var length = cipher.ProcessBytes(ciphertext, output);
            cipher.DoFinal(output.AsSpan(length));
        }
        catch (InvalidCipherTextException ex)
        {
            CryptographicOperations.ZeroMemory(output);
            throw new AuthenticationTagMismatchException("The data could not be authenticated: wrong key, or it has been changed.", ex);
        }

        return output;
    }

    private GcmBlockCipher CreateCipher(bool forEncryption, ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> associatedData)
    {
        if (key.Length != KeySize)
        {
            throw new ArgumentException($"The key must be {KeySize} bytes.", nameof(key));
        }

        if (nonce.Length != NonceSize)
        {
            throw new ArgumentException($"The nonce must be {NonceSize} bytes.", nameof(nonce));
        }

        var cipher = new GcmBlockCipher(new AesEngine());
        cipher.Init(forEncryption, new AeadParameters(new KeyParameter(key), TagSize * 8, nonce.ToArray(), associatedData.ToArray()));
        return cipher;
    }
}
