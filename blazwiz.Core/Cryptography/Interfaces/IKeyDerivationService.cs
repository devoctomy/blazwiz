namespace blazwiz.Core.Cryptography.Interfaces;

/// <summary>
/// Derives keys from input key material.
/// </summary>
public interface IKeyDerivationService
{
    /// <summary>
    /// Derives a key from the input key material.
    /// </summary>
    /// <param name="inputKeyMaterial">The secret to derive from.</param>
    /// <param name="salt">The salt, which need not be secret.</param>
    /// <param name="info">Context binding the key to its use, so each use gets a different key.</param>
    /// <param name="length">The length of the key in bytes.</param>
    /// <returns>The derived key.</returns>
    byte[] DeriveKey(byte[] inputKeyMaterial, byte[] salt, byte[] info, int length);
}
