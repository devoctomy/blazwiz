using System.Security.Cryptography;
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
        if (!await userKeysStore.TryCreateAsync(scope, json, ct))
        {
            // Never replace a profile's keys: its files would be lost.
            throw new InvalidOperationException($"Protected profile {scope} already has keys.");
        }

        var index = await ReadIndexAsync(ct);
        index.Profiles.Add(new E2eProfileIndexEntry { Id = scope.Id, Created = timeProvider.GetUtcNow().UtcDateTime });
        await WriteIndexAsync(index, ct);
        progress?.Report(new E2eProgress(E2eStep.SaveKeys, true));

        progress?.Report(new E2eProgress(E2eStep.StoreDeviceKey, false));
        await localKeyStore.SaveAsync(scope, new E2eLocalKeys { CachedDocument = json });
        keyRings.Get(scope).Load(keys.MasterKey, new Dictionary<string, byte[]>(StringComparer.Ordinal) { [keys.FileKeyId] = keys.FileKey }, keys.FileKeyId);
        progress?.Report(new E2eProgress(E2eStep.StoreDeviceKey, true));
        SetUnlocked(scope, true);
    }

    /// <inheritdoc/>
    public async Task UnlockAsync(E2eKeyScope scope, string passphrase, CancellationToken ct = default)
    {
        CheckProtected(scope);
        ArgumentException.ThrowIfNullOrEmpty(passphrase);

        string? json;
        var reached = true;
        try
        {
            json = await userKeysStore.GetAsync(scope, ct);
        }
        catch (HttpRequestException)
        {
            // Offline: this device's copy, if it has one.
            json = (await localKeyStore.GetAsync(scope))?.CachedDocument;
            reached = false;
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

        if (reached)
        {
            await SaveCachedDocumentAsync(scope, json);
        }

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
                local.WrappedMasterKey = keyWrapService.Wrap(kek, masterKey);
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
            masterKey = keyWrapService.Unwrap(kek, local.WrappedMasterKey);
        }
        catch (CryptographicException ex)
        {
            // Another passkey (e.g. registered again): the passphrase is needed.
            throw new E2ePassphraseRequiredException(ex);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
        }

        try
        {
            keysDocuments.LoadFileKeys(keyRings.Get(scope), keysDocuments.Parse(local.CachedDocument), masterKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(masterKey);
        }

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
    public void Lock(E2eKeyScope scope)
    {
        CheckProtected(scope);

        keyRings.Get(scope).Clear();
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
    public async Task ChangePassphraseAsync(E2eKeyScope scope, string currentPassphrase, string newPassphrase, E2eProfileSettings settings, CancellationToken ct = default)
    {
        CheckProtected(scope);
        ArgumentException.ThrowIfNullOrEmpty(currentPassphrase);
        CheckPassphrase(newPassphrase, settings);

        var json = await userKeysStore.GetAsync(scope, ct)
            ?? throw new E2eProfileNotFoundException(scope.Id);
        var document = keysDocuments.Parse(json);
        var masterKey = await keysDocuments.UnwrapMasterKeyAsync(document, currentPassphrase, ct);
        try
        {
            await keysDocuments.RewrapMasterKeyAsync(document, masterKey, newPassphrase, settings.Argon2, ct);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(masterKey);
        }

        var updated = keysDocuments.Serialize(document);
        await userKeysStore.SetAsync(scope, updated, ct);
        await SaveCachedDocumentAsync(scope, updated);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(E2eKeyScope scope, CancellationToken ct = default)
    {
        CheckProtected(scope);

        Lock(scope);
        await userKeysStore.DeleteAsync(scope, ct);
        await localKeyStore.DeleteAsync(scope);
        var index = await ReadIndexAsync(ct);
        if (index.Profiles.RemoveAll(p => p.Id == scope.Id) > 0)
        {
            await WriteIndexAsync(index, ct);
        }
    }

    /// <inheritdoc/>
    public async Task SetIdleTimeoutAsync(E2eKeyScope scope, TimeSpan? idleTimeout, CancellationToken ct = default)
    {
        CheckProtected(scope);
        if (idleTimeout is { } timeout && timeout < TimeSpan.FromSeconds(1))
        {
            throw new ArgumentOutOfRangeException(nameof(idleTimeout), "An idle timeout must be at least a second.");
        }

        var index = await ReadIndexAsync(ct);
        var entry = index.Profiles.FirstOrDefault(p => p.Id == scope.Id)
            ?? throw new E2eProfileNotFoundException(scope.Id);
        entry.IdleTimeoutSeconds = idleTimeout is { } after ? (int)Math.Min(int.MaxValue, Math.Round(after.TotalSeconds)) : 0;
        await WriteIndexAsync(index, ct);
    }

    /// <inheritdoc/>
    public Task ForgetAsync(E2eKeyScope scope)
    {
        CheckProtected(scope);

        Lock(scope);
        return localKeyStore.DeleteAsync(scope);
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

    private async Task SaveCachedDocumentAsync(E2eKeyScope scope, string json)
    {
        var local = await localKeyStore.GetAsync(scope) ?? new E2eLocalKeys();
        local.CachedDocument = json;
        await localKeyStore.SaveAsync(scope, local);
    }

    private async Task<E2eProfileIndex> ReadIndexAsync(CancellationToken ct)
    {
        string? json;
        try
        {
            json = await indexStore.GetIndexAsync(ct);
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

    private async Task WriteIndexAsync(E2eProfileIndex index, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(index, JsonOptions);
        await indexStore.SetIndexAsync(json, ct);
        await localIndexStore.SaveAsync(json);
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
