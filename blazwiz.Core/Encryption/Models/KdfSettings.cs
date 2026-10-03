using System.Text.Json.Serialization;
using blazwiz.Core.Cryptography.Models;

namespace blazwiz.Core.Encryption.Models;

/// <summary>
/// How the passphrase key in a <see cref="UserKeysDocument"/> is derived. Always read from the document when deriving,
/// never from the build, so documents made by Debug and Release builds stay readable and the costs can be raised.
/// </summary>
public class KdfSettings
{
    /// <summary>
    /// The only supported algorithm.
    /// </summary>
    public const string Argon2idAlgorithm = "argon2id";

    /// <summary>
    /// Gets or sets the algorithm.
    /// </summary>
    [JsonPropertyName("alg")]
    public string Algorithm { get; set; } = Argon2idAlgorithm;

    /// <summary>
    /// Gets or sets the random salt, base64.
    /// </summary>
    [JsonPropertyName("salt")]
    public string Salt { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the memory used, in KiB.
    /// </summary>
    [JsonPropertyName("memory_kib")]
    public int MemoryKib { get; set; }

    /// <summary>
    /// Gets or sets the number of passes over the memory.
    /// </summary>
    [JsonPropertyName("iterations")]
    public int Iterations { get; set; }

    /// <summary>
    /// Gets or sets the number of lanes.
    /// </summary>
    [JsonPropertyName("parallelism")]
    public int Parallelism { get; set; }

    /// <summary>
    /// Gets or sets the key length in bytes.
    /// </summary>
    [JsonPropertyName("key_len")]
    public int KeyLength { get; set; }

    /// <summary>
    /// Creates the settings for a new salt and cost settings.
    /// </summary>
    /// <param name="salt">The salt.</param>
    /// <param name="parameters">The cost settings.</param>
    /// <returns>The settings.</returns>
    public static KdfSettings Create(byte[] salt, Argon2idParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(salt);
        ArgumentNullException.ThrowIfNull(parameters);

        return new KdfSettings
        {
            Salt = Convert.ToBase64String(salt),
            MemoryKib = parameters.MemoryKib,
            Iterations = parameters.Iterations,
            Parallelism = parameters.Parallelism,
            KeyLength = parameters.KeyLength,
        };
    }

    /// <summary>
    /// Gets the cost settings.
    /// </summary>
    /// <returns>The cost settings.</returns>
    public Argon2idParameters ToParameters() => new Argon2idParameters
    {
        MemoryKib = MemoryKib,
        Iterations = Iterations,
        Parallelism = Parallelism,
        KeyLength = KeyLength,
    };
}
