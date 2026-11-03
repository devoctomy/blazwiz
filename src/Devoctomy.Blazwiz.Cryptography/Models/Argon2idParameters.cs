namespace Devoctomy.Blazwiz.Cryptography.Models;

/// <summary>
/// The cost settings for Argon2id. Deriving the same key again needs exactly the same settings and salt, so they are
/// stored with whatever the key protects rather than taken from the build.
/// </summary>
public class Argon2idParameters
{
    /// <summary>
    /// Gets or sets the memory used, in KiB. Memory is what makes Argon2id expensive to attack with GPUs.
    /// </summary>
    public int MemoryKib { get; set; }

    /// <summary>
    /// Gets or sets the number of passes over the memory.
    /// </summary>
    public int Iterations { get; set; }

    /// <summary>
    /// Gets or sets the number of lanes. WebAssembly is single threaded, so lanes run one after another there.
    /// </summary>
    public int Parallelism { get; set; }

    /// <summary>
    /// Gets or sets the derived key length in bytes.
    /// </summary>
    public int KeyLength { get; set; }

    /// <summary>
    /// Creates a copy of the parameters.
    /// </summary>
    /// <returns>The copy.</returns>
    public Argon2idParameters Clone() => (Argon2idParameters)MemberwiseClone();
}
