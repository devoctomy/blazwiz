using System.Security.Cryptography;
using System.Text;
using Devoctomy.Blazwiz.Cryptography.Interfaces;
using Devoctomy.Blazwiz.Cryptography.Models;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;

namespace Devoctomy.Blazwiz.Cryptography;

/// <summary>
/// Implementation of <see cref="IArgon2idKeyDerivationService"/> using Bouncy Castle's Argon2 (version 1.3).
/// </summary>
public class BouncyCastleArgon2idKeyDerivationService : IArgon2idKeyDerivationService
{
    /// <summary>
    /// The shortest salt accepted, in bytes.
    /// </summary>
    public const int MinSaltLength = 8;

    /// <inheritdoc/>
    public async Task<byte[]> DeriveKeyAsync(string passphrase, byte[] salt, Argon2idParameters parameters, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(passphrase);
        ArgumentNullException.ThrowIfNull(salt);
        ArgumentNullException.ThrowIfNull(parameters);
        if (salt.Length < MinSaltLength)
        {
            throw new ArgumentException($"The salt must be at least {MinSaltLength} bytes.", nameof(salt));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(parameters.Iterations, 1, nameof(parameters));
        ArgumentOutOfRangeException.ThrowIfLessThan(parameters.Parallelism, 1, nameof(parameters));
        ArgumentOutOfRangeException.ThrowIfLessThan(parameters.MemoryKib, 8 * parameters.Parallelism, nameof(parameters));
        ArgumentOutOfRangeException.ThrowIfLessThan(parameters.KeyLength, 16, nameof(parameters));

        // Let the caller's UI update before the long, blocking computation.
        await Task.Yield();
        ct.ThrowIfCancellationRequested();

        var argon2Parameters = new Argon2Parameters.Builder(Argon2Parameters.Argon2id)
            .WithVersion(Argon2Parameters.Version13)
            .WithIterations(parameters.Iterations)
            .WithMemoryAsKB(parameters.MemoryKib)
            .WithParallelism(parameters.Parallelism)
            .WithSalt(salt)
            .Build();

        var generator = new Argon2BytesGenerator();
        generator.Init(argon2Parameters);

        var passphraseBytes = Encoding.UTF8.GetBytes(passphrase);
        var key = new byte[parameters.KeyLength];
        try
        {
            generator.GenerateBytes(passphraseBytes, key);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passphraseBytes);
        }

        return key;
    }
}
