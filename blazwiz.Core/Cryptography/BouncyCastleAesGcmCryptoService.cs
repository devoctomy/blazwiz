using System.Security.Cryptography;
using blazwiz.Core.Cryptography.Interfaces;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;

namespace blazwiz.Core.Cryptography;

/// <summary>
/// Implementation of ICryptoService using Bouncy Castle AES-GCM encryption
/// Stream format: [nonce][ciphertext][authentication tag]
/// This implementation is compatible with NativeAesGcmCryptoService.
/// </summary>
public class BouncyCastleAesGcmCryptoService : ICryptoService
{
    private const int NonceSize = 12;
    private const int TagSize = 16; // AES-GCM uses a 128-bit authentication tag

    /// <summary>
    /// Encrypt the input stream via AES GCM using the key provided.
    /// </summary>
    /// <param name="inputStream">Input stream to encrypt.</param>
    /// <param name="outputStream">Output stream to store cipher data.</param>
    /// <param name="base64Key">Base64 encoded AES key.</param>
    /// <returns>Nothing.</returns>
    /// <exception cref="ArgumentNullException">Thrown when inputStream, outputStream, or base64Key is null.</exception>
    /// <exception cref="ArgumentException">Thrown when base64Key is empty or not a valid base64 string.</exception>
    public async Task EncryptAsync(
        Stream inputStream,
        Stream outputStream,
        string? base64Key)
    {
        ArgumentNullException.ThrowIfNull(inputStream, nameof(inputStream));
        ArgumentNullException.ThrowIfNull(outputStream, nameof(outputStream));
        ArgumentException.ThrowIfNullOrEmpty(base64Key, nameof(base64Key));

        byte[] key = Convert.FromBase64String(base64Key);

        byte[] nonce = new byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);

        await outputStream.WriteAsync(nonce);

        using var memoryStream = new MemoryStream();
        await inputStream.CopyToAsync(memoryStream);
        byte[] plaintext = memoryStream.ToArray();

        // Initialize Bouncy Castle AES-GCM cipher
        var cipher = new GcmBlockCipher(new AesEngine());
        var parameters = new AeadParameters(new KeyParameter(key), TagSize * 8, nonce);

        cipher.Init(true, parameters);

        // Encrypt the data
        byte[] output = new byte[cipher.GetOutputSize(plaintext.Length)];
        int len = cipher.ProcessBytes(plaintext, 0, plaintext.Length, output, 0);
        len += cipher.DoFinal(output, len);

        // Split the output into ciphertext and tag
        int ciphertextLength = len - TagSize;
        byte[] ciphertext = new byte[ciphertextLength];
        byte[] tag = new byte[TagSize];

        Buffer.BlockCopy(output, 0, ciphertext, 0, ciphertextLength);
        Buffer.BlockCopy(output, ciphertextLength, tag, 0, TagSize);

        await outputStream.WriteAsync(ciphertext);
        await outputStream.WriteAsync(tag);
    }

    /// <summary>
    /// Decrypt the input stream via AES GCM using the key provided.
    /// </summary>
    /// <param name="inputStream">Input stream to decrypt.</param>
    /// <param name="outputStream">Output stream to store plaintext data.</param>
    /// <param name="base64Key">Base64 encoded AES key.</param>
    /// <returns>Nothing.</returns>
    /// <exception cref="ArgumentNullException">Thrown when inputStream, outputStream, or base64Key is null.</exception>
    /// <exception cref="ArgumentException">Thrown when base64Key is empty or not a valid base64 string.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the input data is invalid or cannot be decrypted.</exception>
    public async Task DecryptAsync(
        Stream inputStream,
        Stream outputStream,
        string? base64Key)
    {
        ArgumentNullException.ThrowIfNull(inputStream, nameof(inputStream));
        ArgumentNullException.ThrowIfNull(outputStream, nameof(outputStream));
        ArgumentNullException.ThrowIfNull(base64Key, nameof(base64Key));

        if (string.IsNullOrEmpty(base64Key))
        {
            throw new ArgumentException("Key cannot be empty", nameof(base64Key));
        }

        byte[] key = Convert.FromBase64String(base64Key);
        byte[] nonce = new byte[NonceSize];
        int bytesRead = await inputStream.ReadAsync(nonce.AsMemory(0, NonceSize));
        if (bytesRead != NonceSize)
        {
            throw new InvalidOperationException($"Failed to read nonce from input stream. Expected {NonceSize} bytes but got {bytesRead}.");
        }

        using var memoryStream = new MemoryStream();
        await inputStream.CopyToAsync(memoryStream);
        byte[] encryptedData = memoryStream.ToArray();
        if (encryptedData.Length < TagSize)
        {
            throw new InvalidOperationException("Input data is too short to contain authentication tag.");
        }

        int ciphertextLength = encryptedData.Length - TagSize;
        byte[] ciphertext = new byte[ciphertextLength];
        byte[] tag = new byte[TagSize];

        Buffer.BlockCopy(encryptedData, 0, ciphertext, 0, ciphertextLength);
        Buffer.BlockCopy(encryptedData, ciphertextLength, tag, 0, TagSize);

        // Combine ciphertext and tag for Bouncy Castle
        byte[] ciphertextWithTag = new byte[ciphertextLength + TagSize];
        Buffer.BlockCopy(ciphertext, 0, ciphertextWithTag, 0, ciphertextLength);
        Buffer.BlockCopy(tag, 0, ciphertextWithTag, ciphertextLength, TagSize);

        // Initialize Bouncy Castle AES-GCM cipher for decryption
        var cipher = new GcmBlockCipher(new AesEngine());
        var parameters = new AeadParameters(new KeyParameter(key), TagSize * 8, nonce);

        cipher.Init(false, parameters);

        try
        {
            // Decrypt the data
            byte[] plaintext = new byte[cipher.GetOutputSize(ciphertextWithTag.Length)];
            int len = cipher.ProcessBytes(ciphertextWithTag, 0, ciphertextWithTag.Length, plaintext, 0);
            len += cipher.DoFinal(plaintext, len);

            // Write only the actual decrypted data (trim to actual length)
            byte[] actualPlaintext = new byte[len];
            Buffer.BlockCopy(plaintext, 0, actualPlaintext, 0, len);

            await outputStream.WriteAsync(actualPlaintext);
        }
        catch (InvalidCipherTextException ex)
        {
            // Convert Bouncy Castle exception to the same type as .NET throws
            throw new AuthenticationTagMismatchException("Authentication tag mismatch", ex);
        }
    }
}
