using System.Security.Cryptography;
using blazwiz.Core.Encryption.Exceptions;
using blazwiz.Core.Encryption.Interfaces;
using blazwiz.Core.Encryption.Models;
using blazwiz.Core.Sync.Interfaces;
using blazwiz.Core.Sync.Models;

namespace blazwiz.Core.Sync.Encryption;

/// <summary>
/// Wraps a profile's file store so app code reads and writes plaintext while the store holds end to end encrypted
/// files. Reads decrypt any file with an encrypted file header (and pass other files through); writes follow the
/// <see cref="IE2eKeyRing.WriteMode"/>: encrypt with the active key, write plaintext, or refuse.
/// </summary>
/// <remarks>
/// It wraps the <see cref="TrackingSyncFileStore"/>, so the index records the stored (encrypted) content. Folder
/// markers have no content, so they pass straight through.
/// </remarks>
public class EncryptingSyncFileStore(
    ISyncFileStore inner,
    IE2eKeyRing keyRing,
    IEncryptedFileFormat encryptedFileFormat)
    : ISyncFileStore
{
    private readonly ISyncFileStore inner = inner ?? throw new ArgumentNullException(nameof(inner));
    private readonly IE2eKeyRing keyRing = keyRing ?? throw new ArgumentNullException(nameof(keyRing));
    private readonly IEncryptedFileFormat encryptedFileFormat = encryptedFileFormat ?? throw new ArgumentNullException(nameof(encryptedFileFormat));

    /// <inheritdoc/>
    public Task<IReadOnlyList<SyncFileInfo>> ListAsync(CancellationToken ct = default) => inner.ListAsync(ct);

    /// <inheritdoc/>
    /// <exception cref="E2eLockedException">The file is encrypted with a key this device does not hold.</exception>
    public async Task<SyncFile?> ReadAsync(string key, CancellationToken ct = default)
    {
        var file = await inner.ReadAsync(key, ct);
        if (file == null || !encryptedFileFormat.TryGetKeyId(file.Content, out var keyId))
        {
            return file;
        }

        if (!keyRing.TryGetFileKey(keyId!, out var fileKey))
        {
            throw new E2eLockedException(E2eErrorCodes.CannotDecrypt, key);
        }

        try
        {
            return new SyncFile
            {
                Key = file.Key,
                Content = encryptedFileFormat.Decrypt(file.Content, fileKey),
                ModifiedAt = file.ModifiedAt,
            };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(fileKey);
        }
    }

    /// <inheritdoc/>
    /// <exception cref="E2eLockedException">Encryption is on, but this device cannot encrypt yet.</exception>
    public Task WriteAsync(string key, byte[] content, DateTimeOffset? modifiedAt = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        return keyRing.WriteMode switch
        {
            E2eWriteMode.Encrypt => WriteEncryptedAsync(key, content, modifiedAt, ct),
            E2eWriteMode.Refuse => throw new E2eLockedException(E2eErrorCodes.CannotEncrypt),
            _ => inner.WriteAsync(key, content, modifiedAt, ct),
        };
    }

    /// <inheritdoc/>
    public Task CreateFolderAsync(string key, CancellationToken ct = default) => inner.CreateFolderAsync(key, ct);

    /// <inheritdoc/>
    public Task DeleteAsync(string key, CancellationToken ct = default) => inner.DeleteAsync(key, ct);

    private async Task WriteEncryptedAsync(string key, byte[] content, DateTimeOffset? modifiedAt, CancellationToken ct)
    {
        var keyId = keyRing.ActiveKeyId;
        if (keyId == null || !keyRing.TryGetFileKey(keyId, out var fileKey))
        {
            throw new E2eLockedException(E2eErrorCodes.CannotEncrypt);
        }

        byte[] encrypted;
        try
        {
            encrypted = encryptedFileFormat.Encrypt(content, keyId, fileKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(fileKey);
        }

        await inner.WriteAsync(key, encrypted, modifiedAt, ct);
    }
}
