using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Devoctomy.Blazwiz.Cryptography.Interfaces;
using Devoctomy.Blazwiz.Cryptography.Models;
using Devoctomy.Blazwiz.Encryption.Exceptions;
using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;

namespace Devoctomy.Blazwiz.Encryption;

/// <summary>
/// Implementation of <see cref="IE2eProtectedProfileManager"/>. Each protected profile's keys document is kept in the
/// <see cref="IUserKeysStore"/> under its scope, with a copy on this device for offline unlocking; the profiles are
/// listed in the <see cref="IE2eProfileIndexStore"/>, with a copy on this device for offline listing. A profile's key
/// ring holds no keys whenever it is locked, so its files can be neither read nor written.
/// </summary>
/// <param name="userKeysStore">Keeps each profile's keys document.</param>
/// <param name="localKeyStore">Keeps this device's copy of each profile's keys document.</param>
/// <param name="indexStore">Keeps the index of the user's protected profiles.</param>
/// <param name="localIndexStore">Keeps this device's copy of the index.</param>
/// <param name="keyRings">The key rings, one per profile.</param>
/// <param name="keysDocuments">Creates and unwraps keys documents.</param>
/// <param name="timeProvider">Dates new profiles.</param>
/// <param name="derivedKeyStore">Holds this device's key encryption key, from the passkey PRF.</param>
/// <param name="keyWrapService">Wraps the master key for device unlock.</param>
public class E2eProtectedProfileManager(
    IUserKeysStore userKeysStore,
    IE2eLocalKeyStore localKeyStore,
    IE2eProfileIndexStore indexStore,
    IE2eLocalProfileIndexStore localIndexStore,
    IE2eKeyRings keyRings,
    IE2eKeysDocumentService keysDocuments,
    TimeProvider timeProvider,
    IDerivedKeyStore derivedKeyStore,
    IKeyWrapService keyWrapService)
    : IE2eProtectedProfileManager
{
    /// <summary>
    /// How many times a change to a keys document or to the index is tried when another device or tab keeps writing it
    /// at the same time, before <see cref="E2eDocumentChangedException"/> is let through.
    /// </summary>
    public const int MaxWriteAttempts = 3;

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions();

    private readonly IUserKeysStore userKeysStore = userKeysStore ?? throw new ArgumentNullException(nameof(userKeysStore));
    private readonly IE2eLocalKeyStore localKeyStore = localKeyStore ?? throw new ArgumentNullException(nameof(localKeyStore));
    private readonly IE2eProfileIndexStore indexStore = indexStore ?? throw new ArgumentNullException(nameof(indexStore));
    private readonly IE2eLocalProfileIndexStore localIndexStore = localIndexStore ?? throw new ArgumentNullException(nameof(localIndexStore));
    private readonly IE2eKeyRings keyRings = keyRings ?? throw new ArgumentNullException(nameof(keyRings));
    private readonly IE2eKeysDocumentService keysDocuments = keysDocuments ?? throw new ArgumentNullException(nameof(keysDocuments));
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    private readonly IDerivedKeyStore derivedKeyStore = derivedKeyStore ?? throw new ArgumentNullException(nameof(derivedKeyStore));
    private readonly IKeyWrapService keyWrapService = keyWrapService ?? throw new ArgumentNullException(nameof(keyWrapService));
    private readonly Lock gate = new Lock();
    private readonly HashSet<E2eKeyScope> unlocked = [];
    private readonly HashSet<E2eKeyScope> rotating = [];

    /// <inheritdoc/>
    public event EventHandler<E2eKeyScope>? LockStateChanged;

    /// <inheritdoc/>
    public IReadOnlyCollection<E2eKeyScope> Unlocked
    {
        get
        {
            lock (gate)
            {
                return [.. unlocked];
            }
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<E2eProfileIndexEntry>> GetProfilesAsync(CancellationToken ct = default)
    {
        return (await ReadIndexAsync(ct)).Profiles;
    }

    /// <inheritdoc/>
    public async Task<E2eKeyScope> CreateAsync(string passphrase, E2eProfileSettings settings, IProgress<E2eProgress>? progress = null, CancellationToken ct = default)
    {
        var scope = new E2eKeyScope(Guid.NewGuid().ToString("D"));
        await CreateKeysAsync(scope, passphrase, settings, progress, ct);
        return scope;
    }

    /// <inheritdoc/>
    public async Task CreateKeysAsync(E2eKeyScope scope, string passphrase, E2eProfileSettings settings, IProgress<E2eProgress>? progress = null, CancellationToken ct = default)
    {
        CheckProtected(scope);
        CheckPassphrase(passphrase, settings);

        using var keys = await keysDocuments.CreateAsync(passphrase, settings.Argon2, progress, ct);
        var json = keysDocuments.Serialize(keys.Document);

        progress?.Report(new E2eProgress(E2eStep.SaveKeys, false));

        // Never replace a profile's keys: its files would be lost.
        var etag = await userKeysStore.TryCreateAsync(scope, json, ct)
            ?? throw new InvalidOperationException($"Protected profile {scope} already has keys.");

        await UpdateIndexAsync(
            index =>
            {
                if (index.Profiles.Any(p => p.Id == scope.Id))
                {
                    return false;
                }

                index.Profiles.Add(new E2eProfileIndexEntry { Id = scope.Id, Created = timeProvider.GetUtcNow().UtcDateTime });
                return true;
            },
            ct);
        progress?.Report(new E2eProgress(E2eStep.SaveKeys, true));

        progress?.Report(new E2eProgress(E2eStep.StoreDeviceKey, false));
        await localKeyStore.SaveAsync(scope, new E2eLocalKeys { CachedDocument = json, CachedDocumentETag = etag });
        keyRings.Get(scope).Load(keys.MasterKey, new Dictionary<string, byte[]>(StringComparer.Ordinal) { [keys.FileKeyId] = keys.FileKey }, keys.FileKeyId);
        progress?.Report(new E2eProgress(E2eStep.StoreDeviceKey, true));
        SetRotating(scope, false);
        SetUnlocked(scope, true);
    }

    /// <inheritdoc/>
    public async Task UnlockAsync(E2eKeyScope scope, string passphrase, CancellationToken ct = default)
    {
        CheckProtected(scope);
        ArgumentException.ThrowIfNullOrEmpty(passphrase);

        string? json;
        StoredDocument? stored = null;
        try
        {
            stored = await userKeysStore.GetAsync(scope, ct);
            json = stored?.Value;
        }
        catch (HttpRequestException)
        {
            // Offline: this device's copy, if it has one.
            json = (await localKeyStore.GetAsync(scope))?.CachedDocument;
        }

        if (json == null)
        {
            throw new E2eProfileNotFoundException(scope.Id);
        }

        var document = keysDocuments.Parse(json);
        var masterKey = await keysDocuments.UnwrapMasterKeyAsync(document, passphrase, ct);
        try
        {
            keysDocuments.LoadFileKeys(keyRings.Get(scope), document, masterKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(masterKey);
        }

        if (stored != null)
        {
            await SaveCachedDocumentAsync(scope, stored);
        }

        SetRotating(scope, document.RotationInProgress);
        SetUnlocked(scope, true);
    }

    /// <inheritdoc/>
    public async Task EnableDeviceUnlockAsync(E2eKeyScope scope)
    {
        CheckProtected(scope);
        if (!keyRings.Get(scope).TryGetMasterKey(out var masterKey))
        {
            throw new E2eLockedException();
        }

        try
        {
            if (!derivedKeyStore.TryGetKey(E2eKeyNames.KeyEncryptionKey, out var kek))
            {
                throw new InvalidOperationException("Unlocking with the passkey needs you to sign in with your passkey.");
            }

            try
            {
                var local = await localKeyStore.GetAsync(scope) ?? new E2eLocalKeys();
                local.WrappedMasterKey = keyWrapService.Wrap(kek, masterKey, DeviceKeyBinding(scope));
                await localKeyStore.SaveAsync(scope, local);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(kek);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(masterKey);
        }
    }

    /// <inheritdoc/>
    public async Task DisableDeviceUnlockAsync(E2eKeyScope scope)
    {
        CheckProtected(scope);

        var local = await localKeyStore.GetAsync(scope);
        if (local != null && HasDeviceKey(local))
        {
            local.WrappedMasterKey = new WrappedKey();
            await localKeyStore.SaveAsync(scope, local);
        }
    }

    /// <inheritdoc/>
    public async Task<bool> HasDeviceUnlockAsync(E2eKeyScope scope)
    {
        CheckProtected(scope);

        var local = await localKeyStore.GetAsync(scope);
        return local?.CachedDocument != null && HasDeviceKey(local);
    }

    /// <inheritdoc/>
    public async Task<bool> CanUnlockWithDeviceAsync(E2eKeyScope scope)
    {
        CheckProtected(scope);

        var local = await localKeyStore.GetAsync(scope);
        if (local?.CachedDocument == null || !HasDeviceKey(local) || !derivedKeyStore.TryGetKey(E2eKeyNames.KeyEncryptionKey, out var kek))
        {
            return false;
        }

        CryptographicOperations.ZeroMemory(kek);
        return true;
    }

    /// <inheritdoc/>
    public async Task UnlockWithDeviceAsync(E2eKeyScope scope)
    {
        CheckProtected(scope);

        var local = await localKeyStore.GetAsync(scope);
        if (local?.CachedDocument == null || !HasDeviceKey(local) || !derivedKeyStore.TryGetKey(E2eKeyNames.KeyEncryptionKey, out var kek))
        {
            throw new E2ePassphraseRequiredException();
        }

        byte[] masterKey;
        try
        {
            masterKey = keyWrapService.Unwrap(kek, local.WrappedMasterKey, DeviceKeyBinding(scope));
        }
        catch (CryptographicException ex)
        {
            // Another passkey (e.g. registered again), or another profile's key put here: the passphrase is needed.
            throw new E2ePassphraseRequiredException(ex);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
        }

        UserKeysDocument document;
        try
        {
            var stored = await TryGetStoredDocumentAsync(scope);
            if (stored == null || string.Equals(stored.ETag, local.CachedDocumentETag, StringComparison.Ordinal))
            {
                // Offline, or unchanged since this device last read it: this device's copy.
                document = keysDocuments.Parse(local.CachedDocument);
                keysDocuments.LoadFileKeys(keyRings.Get(scope), document, masterKey);
            }
            else
            {
                document = keysDocuments.Parse(stored.Value);
                local.CachedDocument = stored.Value;
                local.CachedDocumentETag = stored.ETag;
                try
                {
                    keysDocuments.LoadFileKeys(keyRings.Get(scope), document, masterKey);
                }
                catch (InvalidDataException ex)
                {
                    // The keys were rotated on another device (its passphrase changed): the master key this device
                    // keeps unwraps nothing in them, and is forgotten. Only the new passphrase unlocks the profile now.
                    local.WrappedMasterKey = new WrappedKey();
                    await localKeyStore.SaveAsync(scope, local);
                    throw new E2ePassphraseRequiredException(E2eErrorCodes.KeysChanged, ex);
                }

                await localKeyStore.SaveAsync(scope, local);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(masterKey);
        }

        SetRotating(scope, document.RotationInProgress);
        SetUnlocked(scope, true);
    }

    /// <inheritdoc/>
    public bool IsUnlocked(E2eKeyScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        lock (gate)
        {
            return unlocked.Contains(scope);
        }
    }

    /// <inheritdoc/>
    public bool IsRotationInProgress(E2eKeyScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        lock (gate)
        {
            return rotating.Contains(scope);
        }
    }

    /// <inheritdoc/>
    public void Lock(E2eKeyScope scope)
    {
        CheckProtected(scope);

        keyRings.Get(scope).Clear();
        SetRotating(scope, false);
        SetUnlocked(scope, false);
    }

    /// <inheritdoc/>
    public void LockAll()
    {
        foreach (var scope in Unlocked)
        {
            Lock(scope);
        }
    }

    /// <inheritdoc/>
    public async Task ChangePassphraseAsync(E2eKeyScope scope, string currentPassphrase, string newPassphrase, E2eProfileSettings settings, IProgress<E2eProgress>? progress = null, CancellationToken ct = default)
    {
        CheckProtected(scope);
        ArgumentException.ThrowIfNullOrEmpty(currentPassphrase);
        CheckPassphrase(newPassphrase, settings);

        for (var attempt = 1; ; attempt++)
        {
            var stored = await userKeysStore.GetAsync(scope, ct)
                ?? throw new E2eProfileNotFoundException(scope.Id);
            var document = keysDocuments.Parse(stored.Value);
            E2eNewKeys keys;
            var masterKey = await keysDocuments.UnwrapMasterKeyAsync(document, currentPassphrase, ct);
            try
            {
                // New random keys on every attempt: nothing made for a version that lost the race is kept.
                keys = await keysDocuments.RotateAsync(document, masterKey, newPassphrase, settings.Argon2, progress, ct);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(masterKey);
            }

            using (keys)
            {
                var updated = keysDocuments.Serialize(keys.Document);
                progress?.Report(new E2eProgress(E2eStep.SaveKeys, false));
                string etag;
                try
                {
                    etag = await userKeysStore.SetAsync(scope, updated, stored.ETag, ct);
                }
                catch (E2eDocumentChangedException) when (attempt < MaxWriteAttempts)
                {
                    // Changed on another device since it was read (its passphrase too, perhaps, in which case the
                    // current passphrase no longer unwraps it): start again from the new version.
                    continue;
                }

                progress?.Report(new E2eProgress(E2eStep.SaveKeys, true));

                // From here the old master key unwraps nothing new. This device holds the new keys, and keeps no copy
                // of the old master key for device unlock.
                progress?.Report(new E2eProgress(E2eStep.StoreDeviceKey, false));
                keysDocuments.LoadFileKeys(keyRings.Get(scope), keys.Document, keys.MasterKey);
                await SaveRotatedLocalKeysAsync(scope, new StoredDocument(updated, etag), keys.MasterKey);
                progress?.Report(new E2eProgress(E2eStep.StoreDeviceKey, true));
            }

            SetRotating(scope, true);
            SetUnlocked(scope, true);
            return;
        }
    }

    /// <inheritdoc/>
    public async Task CompleteRotationAsync(E2eKeyScope scope, CancellationToken ct = default)
    {
        CheckProtected(scope);

        for (var attempt = 1; ; attempt++)
        {
            var stored = await userKeysStore.GetAsync(scope, ct)
                ?? throw new E2eProfileNotFoundException(scope.Id);
            var document = keysDocuments.Parse(stored.Value);
            var activeKeyId = keyRings.Get(scope).ActiveKeyId ?? throw new E2eLockedException();
            if (!document.Keys.TryGetValue(activeKeyId, out var active) || active.Status != FileKeyEntry.ActiveStatus)
            {
                // Rotated again on another device since this one encrypted the files: that rotation is still to be
                // finished, by whoever unlocks the profile next.
                return;
            }

            if (!document.RotationInProgress)
            {
                await SaveCachedDocumentAsync(scope, stored);
                SetRotating(scope, false);
                return;
            }

            // Only the mark changes: the keys are as they were read.
            document.RotationInProgress = false;
            var updated = keysDocuments.Serialize(document);
            try
            {
                var etag = await userKeysStore.SetAsync(scope, updated, stored.ETag, ct);
                await SaveCachedDocumentAsync(scope, new StoredDocument(updated, etag));
                SetRotating(scope, false);
                return;
            }
            catch (E2eDocumentChangedException) when (attempt < MaxWriteAttempts)
            {
                // Changed on another device since it was read: start again from the new version.
            }
        }
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(E2eKeyScope scope, CancellationToken ct = default)
    {
        CheckProtected(scope);

        Lock(scope);
        await userKeysStore.DeleteAsync(scope, ct);
        await localKeyStore.DeleteAsync(scope);
        await UpdateIndexAsync(index => index.Profiles.RemoveAll(p => p.Id == scope.Id) > 0, ct);
    }

    /// <inheritdoc/>
    public async Task SetIdleTimeoutAsync(E2eKeyScope scope, TimeSpan? idleTimeout, CancellationToken ct = default)
    {
        CheckProtected(scope);
        if (idleTimeout is { } timeout && timeout < TimeSpan.FromSeconds(1))
        {
            throw new ArgumentOutOfRangeException(nameof(idleTimeout), "An idle timeout must be at least a second.");
        }

        var seconds = idleTimeout is { } after ? (int)Math.Min(int.MaxValue, Math.Round(after.TotalSeconds)) : 0;
        await UpdateIndexAsync(
            index =>
            {
                var entry = index.Profiles.FirstOrDefault(p => p.Id == scope.Id)
                    ?? throw new E2eProfileNotFoundException(scope.Id);
                entry.IdleTimeoutSeconds = seconds;
                return true;
            },
            ct);
    }

    /// <inheritdoc/>
    public Task ForgetAsync(E2eKeyScope scope)
    {
        CheckProtected(scope);

        Lock(scope);
        return localKeyStore.DeleteAsync(scope);
    }

    /// <inheritdoc/>
    public async Task<bool> TryRestoreToIndexAsync(E2eKeyScope scope, CancellationToken ct = default)
    {
        CheckProtected(scope);

        try
        {
            if (await userKeysStore.GetAsync(scope, ct) == null)
            {
                return false;
            }
        }
        catch (HttpRequestException)
        {
            // Offline: whether the keys are still there is not known, so the profile is kept.
            return true;
        }

        await UpdateIndexAsync(
            index =>
            {
                if (index.Profiles.Any(p => p.Id == scope.Id))
                {
                    return false;
                }

                index.Profiles.Add(new E2eProfileIndexEntry { Id = scope.Id, Created = timeProvider.GetUtcNow().UtcDateTime });
                return true;
            },
            ct);
        return true;
    }

    private static void CheckProtected(E2eKeyScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (!Guid.TryParseExact(scope.Id, "D", out _))
        {
            throw new ArgumentException($"'{scope}' is not a protected profile: their ids are GUIDs.", nameof(scope));
        }
    }

    private static bool HasDeviceKey(E2eLocalKeys local) => !string.IsNullOrEmpty(local.WrappedMasterKey.Wrapped);

    // The scope is the associated data of the master key wrapped for device unlock, so a wrapped key moved to another
    // profile's entry does not unwrap.
    private static byte[] DeviceKeyBinding(E2eKeyScope scope) => Encoding.UTF8.GetBytes(scope.Id);

    private static void CheckPassphrase(string passphrase, E2eProfileSettings settings)
    {
        ArgumentException.ThrowIfNullOrEmpty(passphrase);
        ArgumentNullException.ThrowIfNull(settings);
        if (passphrase.Length < settings.MinimumPassphraseLength)
        {
            throw new E2ePassphraseTooShortException(settings.MinimumPassphraseLength);
        }
    }

    private static E2eProfileIndex ParseIndex(string json)
    {
        E2eProfileIndex? index;
        try
        {
            index = JsonSerializer.Deserialize<E2eProfileIndex>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("The protected profile index is not valid JSON.", ex);
        }

        if (index == null)
        {
            throw new InvalidDataException("The protected profile index is empty.");
        }

        if (index.Version != E2eProfileIndex.CurrentVersion)
        {
            throw new InvalidDataException($"Protected profile index version {index.Version} is not supported.");
        }

        return index;
    }

    private async Task SaveCachedDocumentAsync(E2eKeyScope scope, StoredDocument stored)
    {
        var local = await localKeyStore.GetAsync(scope) ?? new E2eLocalKeys();
        local.CachedDocument = stored.Value;
        local.CachedDocumentETag = stored.ETag;
        await localKeyStore.SaveAsync(scope, local);
    }

    // After a rotation: the new document, and the new master key for device unlock when the passkey's key is in memory.
    // Otherwise device unlock is turned off here, so no copy of the old master key stays on this device.
    private async Task SaveRotatedLocalKeysAsync(E2eKeyScope scope, StoredDocument stored, byte[] masterKey)
    {
        var local = await localKeyStore.GetAsync(scope) ?? new E2eLocalKeys();
        local.CachedDocument = stored.Value;
        local.CachedDocumentETag = stored.ETag;
        if (HasDeviceKey(local))
        {
            if (derivedKeyStore.TryGetKey(E2eKeyNames.KeyEncryptionKey, out var kek))
            {
                try
                {
                    local.WrappedMasterKey = keyWrapService.Wrap(kek, masterKey, DeviceKeyBinding(scope));
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(kek);
                }
            }
            else
            {
                local.WrappedMasterKey = new WrappedKey();
            }
        }

        await localKeyStore.SaveAsync(scope, local);
    }

    // The keys document from the store, or null when it cannot be reached (offline, or not in this session) or is gone.
    private async Task<StoredDocument?> TryGetStoredDocumentAsync(E2eKeyScope scope)
    {
        try
        {
            return await userKeysStore.GetAsync(scope);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
        {
            return null;
        }
    }

    private async Task<E2eProfileIndex> ReadIndexAsync(CancellationToken ct)
    {
        string? json;
        try
        {
            json = (await indexStore.GetIndexAsync(ct))?.Value;
            if (json != null)
            {
                await localIndexStore.SaveAsync(json);
            }
        }
        catch (HttpRequestException)
        {
            // Offline: this device's copy, if it has one.
            json = await localIndexStore.GetAsync();
        }

        return json == null ? new E2eProfileIndex() : ParseIndex(json);
    }

    // Reads the index from the store (never this device's copy: a change must start from the current version), applies
    // the change and writes it back only if nobody wrote it in between. If someone did, the change is applied again to
    // their version, up to MaxWriteAttempts times. The change returns false when there is nothing to write.
    private async Task UpdateIndexAsync(Func<E2eProfileIndex, bool> change, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            var stored = await indexStore.GetIndexAsync(ct);
            if (stored != null)
            {
                await localIndexStore.SaveAsync(stored.Value);
            }

            var index = stored == null ? new E2eProfileIndex() : ParseIndex(stored.Value);
            if (!change(index))
            {
                return;
            }

            var json = JsonSerializer.Serialize(index, JsonOptions);
            try
            {
                await indexStore.SetIndexAsync(json, stored?.ETag, ct);
                await localIndexStore.SaveAsync(json);
                return;
            }
            catch (E2eDocumentChangedException) when (attempt < MaxWriteAttempts)
            {
                // Written by another device or tab since it was read: apply the change to their version.
            }
        }
    }

    private void SetRotating(E2eKeyScope scope, bool value)
    {
        lock (gate)
        {
            if (value)
            {
                rotating.Add(scope);
            }
            else
            {
                rotating.Remove(scope);
            }
        }
    }

    private void SetUnlocked(E2eKeyScope scope, bool value)
    {
        bool changed;
        lock (gate)
        {
            changed = value ? unlocked.Add(scope) : unlocked.Remove(scope);
        }

        if (changed)
        {
            LockStateChanged?.Invoke(this, scope);
        }
    }
}
