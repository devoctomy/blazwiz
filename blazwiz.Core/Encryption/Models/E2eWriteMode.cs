namespace blazwiz.Core.Encryption.Models;

/// <summary>
/// What happens to files written while the app runs.
/// </summary>
public enum E2eWriteMode
{
    /// <summary>
    /// Written as they are: encryption is off, or being turned off.
    /// </summary>
    Plaintext,

    /// <summary>
    /// Encrypted with the active file key.
    /// </summary>
    Encrypt,

    /// <summary>
    /// Refused: encryption is on but this device cannot encrypt yet, so nothing may be written in plaintext.
    /// </summary>
    Refuse,
}
