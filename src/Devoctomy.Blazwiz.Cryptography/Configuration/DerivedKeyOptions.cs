namespace Devoctomy.Blazwiz.Cryptography.Configuration;

/// <summary>
/// A key to derive from the WebAuthn PRF output.
/// </summary>
public class DerivedKeyOptions
{
    /// <summary>
    /// Gets or sets the name the key is derived for and stored under.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the length of the key in bytes.
    /// </summary>
    public int Length { get; set; }
}
