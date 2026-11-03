using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;

namespace Devoctomy.Blazwiz.Sync.Encryption.Interfaces;

/// <summary>
/// Encrypts or decrypts every file of the encrypted profiles in place, one at a time. Each file is marked in the index
/// (encrypted, and with which key) as it is written, so a sweep cut short carries on with the files not yet marked.
/// </summary>
/// <remarks>
/// Files are read and written through the store app code is given, so the <see cref="IE2eKeyRing"/>
/// write mode decides what is written, and each rewritten file is marked as changed and synced in its new form.
/// </remarks>
public interface ISyncEncryptionSweeper
{
    /// <summary>
    /// Counts the files the index marks as encrypted.
    /// </summary>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The number of encrypted files.</returns>
    Task<int> CountEncryptedAsync(CancellationToken ct = default);

    /// <summary>
    /// Counts the files the index marks as encrypted that have local changes not yet synced.
    /// </summary>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The number of encrypted files with changes not yet synced.</returns>
    Task<int> CountPendingEncryptedAsync(CancellationToken ct = default);

    /// <summary>
    /// Encrypts every file not yet marked as encrypted. The key ring must be encrypting writes.
    /// </summary>
    /// <param name="progress">Reports <see cref="E2eStep.EncryptFiles"/> as files are done.</param>
    /// <param name="profile">One profile, or null for every encrypted profile.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The number of files encrypted.</returns>
    Task<int> EncryptAsync(IProgress<E2eProgress>? progress = null, string? profile = null, CancellationToken ct = default);

    /// <summary>
    /// Decrypts every file marked as encrypted. The key ring must hold the keys and be writing plaintext.
    /// </summary>
    /// <param name="progress">Reports <see cref="E2eStep.DecryptFiles"/> as files are done.</param>
    /// <param name="pendingOnly">True to decrypt only files with local changes not yet synced.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The number of files decrypted.</returns>
    Task<int> DecryptAsync(IProgress<E2eProgress>? progress = null, bool pendingOnly = false, CancellationToken ct = default);
}
