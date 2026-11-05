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
/// Implementation of <see cref="IE2eKeysDocumentService"/>: Argon2id derives the passphrase key, and every key is
/// wrapped with <see cref="IKeyWrapService"/>. Passphrase keys are wiped as soon as they have been used.
/// </summary>
/// <param name="argon2">Derives the passphrase key.</param>
/// <param name="keyWrapService">Creates and wraps keys.</param>
/// <param name="argon2Options">The Argon2id costs for new documents and new passphrases.</param>
/// <param name="timeProvider">Dates new file keys.</param>
public class E2eKeysDocumentService(
    IArgon2idKeyDerivationService argon2,
    IKeyWrapService keyWrapService,
    Argon2idOptions argon2Options,
    TimeProvider timeProvider)
    : IE2eKeysDocumentService
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

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions();

    private readonly IArgon2idKeyDerivationService argon2 = argon2 ?? throw new ArgumentNullException(nameof(argon2));
    private readonly IKeyWrapService keyWrapService = keyWrapService ?? throw new ArgumentNullException(nameof(keyWrapService));
    private readonly Argon2idOptions argon2Options = argon2Options ?? throw new ArgumentNullException(nameof(argon2Options));
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc/>
    public async Task<E2eNewKeys> CreateAsync(string passphrase, Argon2idParameters? parameters = null, IProgress<E2eProgress>? progress = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(passphrase);

        byte[]? passphraseKey = null;
        byte[]? masterKey = null;
        byte[]? fileKey = null;
        try
        {
            progress?.Report(new E2eProgress(E2eStep.DeriveKey, false));
            var salt = RandomNumberGenerator.GetBytes(SaltLength);
            parameters = (parameters ?? argon2Options.Parameters).Clone();
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
            progress?.Report(new E2eProgress(E2eStep.CreateKeys, true));

            var keys = new E2eNewKeys(document, masterKey, keyId, fileKey);
            masterKey = null;
            fileKey = null;
            return keys;
        }
        finally
        {
            Wipe(passphraseKey, masterKey, fileKey);
        }
    }

    /// <inheritdoc/>
    public UserKeysDocument Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

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

    /// <inheritdoc/>
    public string Serialize(UserKeysDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return JsonSerializer.Serialize(document, JsonOptions);
    }

    /// <inheritdoc/>
    public async Task<byte[]> UnwrapMasterKeyAsync(UserKeysDocument document, string passphrase, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrEmpty(passphrase);

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

    /// <inheritdoc/>
    public async Task RewrapMasterKeyAsync(UserKeysDocument document, byte[] masterKey, string passphrase, Argon2idParameters? parameters = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(masterKey);
        ArgumentException.ThrowIfNullOrEmpty(passphrase);

        var salt = RandomNumberGenerator.GetBytes(SaltLength);
        parameters = (parameters ?? argon2Options.Parameters).Clone();
        var passphraseKey = await argon2.DeriveKeyAsync(passphrase, salt, parameters, ct);
        try
        {
            document.Kdf = KdfSettings.Create(salt, parameters);
            document.MasterKey = keyWrapService.Wrap(passphraseKey, masterKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passphraseKey);
        }
    }

    /// <inheritdoc/>
    public void LoadFileKeys(IE2eKeyRing keyRing, UserKeysDocument document, byte[] masterKey)
    {
        ArgumentNullException.ThrowIfNull(keyRing);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(masterKey);

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
}
