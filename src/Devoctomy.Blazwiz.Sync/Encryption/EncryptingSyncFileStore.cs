using System.Security.Cryptography;
using Devoctomy.Blazwiz.Encryption.Exceptions;
using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;

namespace Devoctomy.Blazwiz.Sync.Encryption;

/// <summary>
/// Wraps a profile's file store so app code reads and writes plaintext while the store holds end to end encrypted
/// files, each bound to the profile and its path. While the profile is locked (its key ring holds no keys), reads and
/// writes are refused; content that is not an encrypted file of the profile is never returned.
/// </summary>
/// <remarks>
/// It wraps the <see cref="TrackingSyncFileStore"/>, so the index records the stored (encrypted) content. Folder
/// markers have no content, so they pass straight through.
/// </remarks>
public class EncryptingSyncFileStore(
    ISyncFileStore inner,
    IE2eKeyRing keyRing,
    IEncryptedFileFormat encryptedFileFormat,
    IE2eIdleLock? idleLock = null)
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
        if (file == null || file.Content.Length == 0)
        {
            return file;
        }

        if (!encryptedFileFormat.TryGetKeyId(file.Content, out var keyId))
        {
            // Every profile is encrypted: plaintext there was not written by this app.
            throw new InvalidDataException($"'{key}' is not an encrypted file of this profile.");
        }

        if (!keyRing.TryGetFileKey(keyId!, out var fileKey))
        {
            throw new E2eLockedException(E2eErrorCodes.CannotDecrypt, key);
        }

        try
        {
            idleLock?.Touch(keyRing.Scope);
            return new SyncFile
            {
                Key = file.Key,
                Content = encryptedFileFormat.Decrypt(file.Content, fileKey, new E2eFileBinding(keyRing.Scope.Id, key)),
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

        return WriteEncryptedAsync(key, content, modifiedAt, ct);
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
            // Bound to the profile and to the file's path.
            encrypted = encryptedFileFormat.Encrypt(content, keyId, fileKey, new E2eFileBinding(keyRing.Scope.Id, key));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(fileKey);
        }

        await inner.WriteAsync(key, encrypted, modifiedAt, ct);
        idleLock?.Touch(keyRing.Scope);
    }
}
