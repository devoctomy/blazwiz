using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Devoctomy.Blazwiz.Cryptography.Configuration;
using Devoctomy.Blazwiz.Cryptography.Interfaces;
using Devoctomy.Blazwiz.Cryptography.Models;
using Devoctomy.Blazwiz.Encryption.Exceptions;
using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;

namespace Devoctomy.Blazwiz.Encryption;

/// <summary>
/// Implementation of <see cref="IE2eKeyManager"/> for the account's key scope (<see cref="E2eKeyScope.Account"/>). Raw
/// keys are wiped as soon as they are handed to the <see cref="IE2eKeyRing"/>; the passphrase key is wiped as soon as
/// the master key is wrapped or unwrapped.
/// </summary>
public class E2eKeyManager(
    IUserKeysStore userKeysStore,
    IE2eLocalKeyStore localKeyStore,
    IDerivedKeyStore derivedKeyStore,
    IE2eKeyRing keyRing,
    IE2eKeysDocumentService keysDocuments,
    IKeyWrapService keyWrapService)
    : IE2eKeyManager
{
    private static readonly E2eKeyScope Scope = E2eKeyScope.Account;

    private readonly IUserKeysStore userKeysStore = userKeysStore ?? throw new ArgumentNullException(nameof(userKeysStore));
    private readonly IE2eLocalKeyStore localKeyStore = localKeyStore ?? throw new ArgumentNullException(nameof(localKeyStore));
    private readonly IDerivedKeyStore derivedKeyStore = derivedKeyStore ?? throw new ArgumentNullException(nameof(derivedKeyStore));
    private readonly IE2eKeyRing keyRing = keyRing ?? throw new ArgumentNullException(nameof(keyRing));
    private readonly IE2eKeysDocumentService keysDocuments = keysDocuments ?? throw new ArgumentNullException(nameof(keysDocuments));
    private readonly IKeyWrapService keyWrapService = keyWrapService ?? throw new ArgumentNullException(nameof(keyWrapService));

    private E2eStatus status = E2eStatus.Unknown;

    /// <inheritdoc/>
    public event EventHandler<E2eStatus>? StatusChanged;

    /// <inheritdoc/>
    public E2eStatus Status => status;

    /// <inheritdoc/>
    public bool HasKeyEncryptionKey
    {
        get
        {
            if (!derivedKeyStore.TryGetKey(E2eKeyNames.KeyEncryptionKey, out var kek))
            {
                return false;
            }

            CryptographicOperations.ZeroMemory(kek);
            return true;
        }
    }

    /// <inheritdoc/>
    public async Task<E2eStatus> InitializeAsync(CancellationToken ct = default)
    {
        string? json;
        var reached = true;
        try
        {
            json = await userKeysStore.GetAsync(Scope, ct);
        }
        catch (InvalidOperationException)
        {
            keyRing.Clear(E2eWriteMode.Plaintext);
            return SetStatus(E2eStatus.Unsupported);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Offline, or the identity provider refused: carry on with this device's copy, if any.
            json = null;
            reached = false;
        }

        var local = await localKeyStore.GetAsync(Scope);
        if (!reached)
        {
            json = local?.CachedDocument;
        }

        if (json == null)
        {
            if (local != null)
            {
                // This device has keys, so nothing may be written in plaintext until it is clear what happened.
                keyRing.Clear(E2eWriteMode.Refuse);
                return SetStatus(reached ? E2eStatus.DisabledElsewhere : E2eStatus.Unknown);
            }

            keyRing.Clear(E2eWriteMode.Plaintext);
            return SetStatus(!reached ? E2eStatus.Unknown : HasKeyEncryptionKey ? E2eStatus.Disabled : E2eStatus.Unavailable);
        }

        var document = keysDocuments.Parse(json);
        if (!derivedKeyStore.TryGetKey(E2eKeyNames.KeyEncryptionKey, out var kek))
        {
            keyRing.Clear(E2eWriteMode.Refuse);
            return SetStatus(E2eStatus.Blocked);
        }

        try
        {
            if (local == null || !TryUnlock(document, kek, local.WrappedMasterKey))
            {
                // Not set up here, set up with another passkey, or encryption was enabled again since.
                keyRing.Clear(E2eWriteMode.Refuse);
                return SetStatus(E2eStatus.NeedsSetup);
            }

            if (reached && local.CachedDocument != json)
            {
                local.CachedDocument = json;
                await localKeyStore.SaveAsync(Scope, local);
            }

            return SetStatus(E2eStatus.Unlocked);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
        }
    }

    /// <inheritdoc/>
    public async Task<bool> IsEnabledAsync(CancellationToken ct = default)
    {
        return await userKeysStore.GetAsync(Scope, ct) != null;
    }

    /// <inheritdoc/>
    public async Task EnableAsync(string passphrase, IProgress<E2eProgress>? progress = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(passphrase);

        var kek = GetKeyEncryptionKey();
        try
        {
            using var keys = await keysDocuments.CreateAsync(passphrase, progress, ct);
            var json = keysDocuments.Serialize(keys.Document);

            progress?.Report(new E2eProgress(E2eStep.SaveKeys, false));
            if (!await userKeysStore.TryCreateAsync(Scope, json, ct))
            {
                // Another device got there first: never replace its keys.
                keyRing.Clear(E2eWriteMode.Refuse);
                SetStatus(E2eStatus.NeedsSetup);
                throw new E2eAlreadyEnabledException();
            }

            progress?.Report(new E2eProgress(E2eStep.SaveKeys, true));

            progress?.Report(new E2eProgress(E2eStep.StoreDeviceKey, false));
            await localKeyStore.SaveAsync(Scope, new E2eLocalKeys
            {
                WrappedMasterKey = keyWrapService.Wrap(kek, keys.MasterKey),
                CachedDocument = json,
            });
            keyRing.Load(keys.MasterKey, new Dictionary<string, byte[]>(StringComparer.Ordinal) { [keys.FileKeyId] = keys.FileKey }, keys.FileKeyId);
            progress?.Report(new E2eProgress(E2eStep.StoreDeviceKey, true));
            SetStatus(E2eStatus.Unlocked);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
        }
    }

    /// <inheritdoc/>
    public async Task SetUpDeviceAsync(string passphrase, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(passphrase);

        var kek = GetKeyEncryptionKey();
        byte[]? masterKey = null;
        try
        {
            var json = await userKeysStore.GetAsync(Scope, ct)
                ?? throw new InvalidOperationException("End to end encryption is not enabled.");
            var document = keysDocuments.Parse(json);
            masterKey = await keysDocuments.UnwrapMasterKeyAsync(document, passphrase, ct);
            keysDocuments.LoadFileKeys(keyRing, document, masterKey);
            await localKeyStore.SaveAsync(Scope, new E2eLocalKeys
            {
                WrappedMasterKey = keyWrapService.Wrap(kek, masterKey),
                CachedDocument = json,
            });
            SetStatus(E2eStatus.Unlocked);
        }
        finally
        {
            Wipe(kek, masterKey);
        }
    }

    /// <inheritdoc/>
    public async Task ChangePassphraseAsync(string currentPassphrase, string newPassphrase, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(currentPassphrase);
        ArgumentException.ThrowIfNullOrEmpty(newPassphrase);

        var json = await userKeysStore.GetAsync(Scope, ct)
            ?? throw new InvalidOperationException("End to end encryption is not enabled.");
        var document = keysDocuments.Parse(json);
        byte[]? masterKey = null;
        try
        {
            masterKey = await keysDocuments.UnwrapMasterKeyAsync(document, currentPassphrase, ct);
            await keysDocuments.RewrapMasterKeyAsync(document, masterKey, newPassphrase, ct);
            var updated = keysDocuments.Serialize(document);
            await userKeysStore.SetAsync(Scope, updated, ct);

            var local = await localKeyStore.GetAsync(Scope);
            if (local != null)
            {
                local.CachedDocument = updated;
                await localKeyStore.SaveAsync(Scope, local);
            }
        }
        finally
        {
            Wipe(masterKey);
        }
    }

    /// <inheritdoc/>
    public async Task VerifyPassphraseAsync(string passphrase, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(passphrase);

        var json = await userKeysStore.GetAsync(Scope, ct)
            ?? throw new InvalidOperationException("End to end encryption is not enabled.");
        var masterKey = await keysDocuments.UnwrapMasterKeyAsync(keysDocuments.Parse(json), passphrase, ct);
        CryptographicOperations.ZeroMemory(masterKey);
    }

    /// <inheritdoc/>
    public async Task UnlockLocalCopyAsync(string? passphrase = null, CancellationToken ct = default)
    {
        var local = await localKeyStore.GetAsync(Scope);
        if (local?.CachedDocument == null)
        {
            throw new InvalidOperationException("This device has no keys to unlock.");
        }

        var document = keysDocuments.Parse(local.CachedDocument);
        if (derivedKeyStore.TryGetKey(E2eKeyNames.KeyEncryptionKey, out var kek))
        {
            try
            {
                if (TryUnlock(document, kek, local.WrappedMasterKey))
                {
                    keyRing.SetWriteMode(E2eWriteMode.Plaintext);
                    return;
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(kek);
            }
        }

        // No passkey, or not the one this device was set up with (e.g. registered again): the copy of keys.json has
        // the master key wrapped by the passphrase too.
        if (string.IsNullOrEmpty(passphrase))
        {
            throw new E2ePassphraseRequiredException();
        }

        var masterKey = await keysDocuments.UnwrapMasterKeyAsync(document, passphrase, ct);
        try
        {
            keysDocuments.LoadFileKeys(keyRing, document, masterKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(masterKey);
        }

        keyRing.SetWriteMode(E2eWriteMode.Plaintext);
    }

    /// <inheritdoc/>
    public async Task<bool> CanUnlockLocalCopyWithPasskeyAsync()
    {
        var local = await localKeyStore.GetAsync(Scope);
        if (local?.CachedDocument == null || !derivedKeyStore.TryGetKey(E2eKeyNames.KeyEncryptionKey, out var kek))
        {
            return false;
        }

        try
        {
            var masterKey = keyWrapService.Unwrap(kek, local.WrappedMasterKey);
            CryptographicOperations.ZeroMemory(masterKey);
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
        }
    }

    /// <inheritdoc/>
    public async Task RemoveAsync(CancellationToken ct = default)
    {
        await userKeysStore.DeleteAsync(Scope, ct);
        await ForgetDeviceAsync();
    }

    /// <inheritdoc/>
    public async Task ForgetDeviceAsync()
    {
        await localKeyStore.DeleteAsync(Scope);
        keyRing.Clear(E2eWriteMode.Plaintext);
        SetStatus(HasKeyEncryptionKey ? E2eStatus.Disabled : E2eStatus.Unavailable);
    }

    private static void Wipe(params byte[]?[] keys)
    {
        foreach (var key in keys)
        {
            if (key != null)
            {
                CryptographicOperations.ZeroMemory(key);
            }
        }
    }

    private byte[] GetKeyEncryptionKey()
    {
        return derivedKeyStore.TryGetKey(E2eKeyNames.KeyEncryptionKey, out var kek)
            ? kek
            : throw new InvalidOperationException("End to end encryption needs you to sign in with your passkey.");
    }

    private bool TryUnlock(UserKeysDocument document, byte[] kek, WrappedKey wrappedMasterKey)
    {
        byte[] masterKey;
        try
        {
            masterKey = keyWrapService.Unwrap(kek, wrappedMasterKey);
        }
        catch (CryptographicException)
        {
            return false;
        }

        try
        {
            keysDocuments.LoadFileKeys(keyRing, document, masterKey);
            return true;
        }
        catch (InvalidDataException)
        {
            return false;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(masterKey);
        }
    }

    private E2eStatus SetStatus(E2eStatus value)
    {
        if (status != value)
        {
            status = value;
            StatusChanged?.Invoke(this, value);
        }

        return value;
    }
}
