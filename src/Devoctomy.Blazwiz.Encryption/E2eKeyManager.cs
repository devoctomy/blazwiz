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
    IArgon2idKeyDerivationService argon2,
    IKeyWrapService keyWrapService,
    Argon2idOptions argon2Options,
    TimeProvider timeProvider)
    : IE2eKeyManager
{
    /// <summary>
    /// The length of a new passphrase salt, in bytes.
    /// </summary>
    public const int SaltLength = 16;

    /// <summary>
    /// The prefix of every file key id.
    /// </summary>
    public const string KeyIdPrefix = "k_";

    private const string Base32Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    private static readonly E2eKeyScope Scope = E2eKeyScope.Account;
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions();

    private readonly IUserKeysStore userKeysStore = userKeysStore ?? throw new ArgumentNullException(nameof(userKeysStore));
    private readonly IE2eLocalKeyStore localKeyStore = localKeyStore ?? throw new ArgumentNullException(nameof(localKeyStore));
    private readonly IDerivedKeyStore derivedKeyStore = derivedKeyStore ?? throw new ArgumentNullException(nameof(derivedKeyStore));
    private readonly IE2eKeyRing keyRing = keyRing ?? throw new ArgumentNullException(nameof(keyRing));
    private readonly IArgon2idKeyDerivationService argon2 = argon2 ?? throw new ArgumentNullException(nameof(argon2));
    private readonly IKeyWrapService keyWrapService = keyWrapService ?? throw new ArgumentNullException(nameof(keyWrapService));
    private readonly Argon2idOptions argon2Options = argon2Options ?? throw new ArgumentNullException(nameof(argon2Options));
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

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

        var document = Parse(json);
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
        byte[]? passphraseKey = null;
        byte[]? masterKey = null;
        byte[]? fileKey = null;
        try
        {
            progress?.Report(new E2eProgress(E2eStep.DeriveKey, false));
            var salt = RandomNumberGenerator.GetBytes(SaltLength);
            var parameters = argon2Options.Parameters.Clone();
            passphraseKey = await argon2.DeriveKeyAsync(passphrase, salt, parameters, ct);
            progress?.Report(new E2eProgress(E2eStep.DeriveKey, true));

            progress?.Report(new E2eProgress(E2eStep.CreateKeys, false));
            masterKey = keyWrapService.CreateKey();
            fileKey = keyWrapService.CreateKey();
            var keyId = CreateKeyId();
            var document = new UserKeysDocument
            {
                Kdf = KdfSettings.Create(salt, parameters),
                MasterKey = keyWrapService.Wrap(passphraseKey, masterKey),
            };
            document.Keys[keyId] = CreateFileKeyEntry(keyWrapService.Wrap(masterKey, fileKey, Encoding.UTF8.GetBytes(keyId)));
            var json = JsonSerializer.Serialize(document, JsonOptions);
            progress?.Report(new E2eProgress(E2eStep.CreateKeys, true));

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
                WrappedMasterKey = keyWrapService.Wrap(kek, masterKey),
                CachedDocument = json,
            });
            keyRing.Load(masterKey, new Dictionary<string, byte[]>(StringComparer.Ordinal) { [keyId] = fileKey }, keyId);
            progress?.Report(new E2eProgress(E2eStep.StoreDeviceKey, true));
            SetStatus(E2eStatus.Unlocked);
        }
        finally
        {
            Wipe(kek, passphraseKey, masterKey, fileKey);
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
            var document = Parse(json);
            masterKey = await UnwrapWithPassphraseAsync(document, passphrase, ct);
            UnlockFileKeys(document, masterKey);
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
        var document = Parse(json);
        byte[]? masterKey = null;
        byte[]? passphraseKey = null;
        try
        {
            masterKey = await UnwrapWithPassphraseAsync(document, currentPassphrase, ct);
            var salt = RandomNumberGenerator.GetBytes(SaltLength);
            var parameters = argon2Options.Parameters.Clone();
            passphraseKey = await argon2.DeriveKeyAsync(newPassphrase, salt, parameters, ct);
            document.Kdf = KdfSettings.Create(salt, parameters);
            document.MasterKey = keyWrapService.Wrap(passphraseKey, masterKey);
            var updated = JsonSerializer.Serialize(document, JsonOptions);
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
            Wipe(masterKey, passphraseKey);
        }
    }

    /// <inheritdoc/>
    public async Task VerifyPassphraseAsync(string passphrase, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(passphrase);

        var json = await userKeysStore.GetAsync(Scope, ct)
            ?? throw new InvalidOperationException("End to end encryption is not enabled.");
        var masterKey = await UnwrapWithPassphraseAsync(Parse(json), passphrase, ct);
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

        var document = Parse(local.CachedDocument);
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

        var masterKey = await UnwrapWithPassphraseAsync(document, passphrase, ct);
        try
        {
            UnlockFileKeys(document, masterKey);
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

    private static UserKeysDocument Parse(string json)
    {
        UserKeysDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<UserKeysDocument>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("The keys document is not valid JSON.", ex);
        }

        if (document == null)
        {
            throw new InvalidDataException("The keys document is empty.");
        }

        if (document.Version != UserKeysDocument.CurrentVersion)
        {
            throw new InvalidDataException($"Keys document version {document.Version} is not supported.");
        }

        if (!string.Equals(document.Kdf.Algorithm, KdfSettings.Argon2idAlgorithm, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Key derivation '{document.Kdf.Algorithm}' is not supported.");
        }

        if (document.Keys.Count(k => k.Value.Status == FileKeyEntry.ActiveStatus) != 1)
        {
            throw new InvalidDataException("The keys document must have exactly one active file key.");
        }

        return document;
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

    private static string CreateKeyId()
    {
        // 10 random bytes as 16 Crockford base32 characters: 80 bits, so ids never collide.
        var bytes = RandomNumberGenerator.GetBytes(10);
        var id = new StringBuilder(KeyIdPrefix, KeyIdPrefix.Length + 16);
        var buffer = 0;
        var bits = 0;
        foreach (var b in bytes)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                bits -= 5;
                id.Append(Base32Alphabet[(buffer >> bits) & 31]);
            }
        }

        return id.ToString();
    }

    private FileKeyEntry CreateFileKeyEntry(WrappedKey wrapped) => new FileKeyEntry
    {
        Algorithm = wrapped.Algorithm,
        Nonce = wrapped.Nonce,
        Wrapped = wrapped.Wrapped,
        Created = timeProvider.GetUtcNow().UtcDateTime,
        Status = FileKeyEntry.ActiveStatus,
    };

    private byte[] GetKeyEncryptionKey()
    {
        return derivedKeyStore.TryGetKey(E2eKeyNames.KeyEncryptionKey, out var kek)
            ? kek
            : throw new InvalidOperationException("End to end encryption needs you to sign in with your passkey.");
    }

    private async Task<byte[]> UnwrapWithPassphraseAsync(UserKeysDocument document, string passphrase, CancellationToken ct)
    {
        byte[] salt;
        try
        {
            salt = Convert.FromBase64String(document.Kdf.Salt);
        }
        catch (FormatException ex)
        {
            throw new InvalidDataException("The keys document salt is not valid base64.", ex);
        }

        var passphraseKey = await argon2.DeriveKeyAsync(passphrase, salt, document.Kdf.ToParameters(), ct);
        try
        {
            return keyWrapService.Unwrap(passphraseKey, document.MasterKey);
        }
        catch (CryptographicException ex)
        {
            // The GCM tag only checks out with the right passphrase key, so this is the passphrase check.
            throw new E2eWrongPassphraseException(ex);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passphraseKey);
        }
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
            UnlockFileKeys(document, masterKey);
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

    private void UnlockFileKeys(UserKeysDocument document, byte[] masterKey)
    {
        var fileKeys = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        try
        {
            foreach (var (id, entry) in document.Keys)
            {
                try
                {
                    // The id is the associated data, so an entry moved to another id does not unwrap.
                    fileKeys[id] = keyWrapService.Unwrap(masterKey, entry, Encoding.UTF8.GetBytes(id));
                }
                catch (CryptographicException ex)
                {
                    throw new InvalidDataException($"File key '{id}' does not belong to this master key.", ex);
                }
            }

            var activeKeyId = document.Keys.Single(k => k.Value.Status == FileKeyEntry.ActiveStatus).Key;
            keyRing.Load(masterKey, fileKeys, activeKeyId);
        }
        finally
        {
            foreach (var key in fileKeys.Values)
            {
                CryptographicOperations.ZeroMemory(key);
            }
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
