using blazwiz.Core.Cryptography.Models;

namespace blazwiz.Core.Cryptography.Configuration;

/// <summary>
/// The Argon2id settings used for keys derived from now on. Anything already derived keeps the settings stored with
/// it, so these can be raised later without breaking existing data, and Debug and Release data stay readable by both.
/// </summary>
public class Argon2idOptions
{
    /// <summary>
    /// The Release build memory: 512 MiB.
    /// </summary>
    public const int ReleaseMemoryKib = 512 * 1024;

    /// <summary>
    /// The Debug build memory: 19 MiB, quick enough to show derivation working while developing.
    /// </summary>
    public const int DebugMemoryKib = 19 * 1024;

    /// <summary>
    /// Gets or sets the settings for new keys. Defaults to the Release or Debug settings for this build.
    /// </summary>
    public Argon2idParameters Parameters { get; set; } = new Argon2idParameters
    {
#if DEBUG
        MemoryKib = DebugMemoryKib,
#else
        MemoryKib = ReleaseMemoryKib,
#endif
        Iterations = 2,
        Parallelism = 1,
        KeyLength = 32,
    };
}
