using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Cryptography.Interfaces;
using Devoctomy.Blazwiz.Encryption;
using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;
using Devoctomy.Blazwiz.Sync.Interfaces;

namespace Devoctomy.Blazwiz.Sync.Encryption;

/// <summary>
/// Keeps the signed in user's profile labels in local storage, one entry per user: a JSON object of labels by profile
/// id, encrypted (AES-GCM, bound to the user) with <see cref="E2eKeyNames.LabelKey"/>, the key derived from the
/// user's passkey when they sign in with it. A label can say what the profile holds, so it is never kept in plain
/// text.
/// </summary>
/// <remarks>
/// Without that key (signed in with a password, or after a reload) labels can't be read or kept, so profiles are shown
/// by their ids, and a label set then lasts only until the page is closed (<see cref="ISyncProtectedProfileService"/>
/// keeps it in memory). Removing a label without the key removes every label of the user on this device, as the
/// entry can't be changed; the others are set again as their profiles are unlocked.
/// </remarks>
/// <param name="localStorageService">The local storage.</param>
/// <param name="userContext">The signed in user.</param>
/// <param name="cipher">Encrypts and decrypts the labels.</param>
/// <param name="keyStore">Holds the key derived from the passkey, if the user signed in with it.</param>
public class LocalStorageE2eProfileLabelStore(
    ILocalStorageService localStorageService,
    ISyncUserContext userContext,
    IAeadCipher cipher,
    IDerivedKeyStore keyStore)
    : IE2eProfileLabelStore
{
    /// <summary>
    /// The local storage key prefix, followed by the user id.
    /// </summary>
    public const string StorageKeyPrefix = "e2e_labels_";

    private readonly ILocalStorageService localStorageService = localStorageService ?? throw new ArgumentNullException(nameof(localStorageService));
    private readonly ISyncUserContext userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
    private readonly IAeadCipher cipher = cipher ?? throw new ArgumentNullException(nameof(cipher));
    private readonly IDerivedKeyStore keyStore = keyStore ?? throw new ArgumentNullException(nameof(keyStore));

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, string>> GetAllAsync()
    {
        var owner = await GetOwnerAsync();
        var key = GetKey();
        if (key == null)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        try
        {
            return await ReadAsync(owner, key);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    /// <inheritdoc/>
    public async Task SetAsync(string profileId, string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentNullException.ThrowIfNull(label);

        var owner = await GetOwnerAsync();
        var key = GetKey();
        if (key == null)
        {
            return;
        }

        try
        {
            var labels = await ReadAsync(owner, key);
            labels[profileId] = label;
            await WriteAsync(owner, key, labels);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    /// <inheritdoc/>
    public async Task RemoveAsync(string profileId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);

        var owner = await GetOwnerAsync();
        var key = GetKey();
        if (key == null)
        {
            await localStorageService.RemoveItemAsync(StorageKeyPrefix + owner);
            return;
        }

        try
        {
            var labels = await ReadAsync(owner, key);
            if (labels.Remove(profileId))
            {
                await WriteAsync(owner, key, labels);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    // The user is the associated data, so one user's labels do not decrypt as another's.
    private static byte[] Binding(string owner) => Encoding.UTF8.GetBytes(owner);

    private byte[]? GetKey() => keyStore.TryGetKey(E2eKeyNames.LabelKey, out var key) ? key : null;

    private async Task<Dictionary<string, string>> ReadAsync(string owner, byte[] key)
    {
        var stored = await localStorageService.GetItemAsync(StorageKeyPrefix + owner);
        if (string.IsNullOrEmpty(stored))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        byte[]? plaintext = null;
        try
        {
            var bytes = Convert.FromBase64String(stored);
            if (bytes.Length < cipher.NonceSize + cipher.TagSize)
            {
                throw new InvalidDataException("The stored labels are too short to be encrypted labels.");
            }

            plaintext = cipher.Decrypt(key, bytes.AsSpan(0, cipher.NonceSize), bytes.AsSpan(cipher.NonceSize), Binding(owner));
            var labels = JsonSerializer.Deserialize<Dictionary<string, string>>(plaintext) ?? [];
            return new Dictionary<string, string>(labels, StringComparer.Ordinal);
        }
        catch (Exception ex) when (ex is FormatException or JsonException or InvalidDataException or CryptographicException)
        {
            // Only labels: unreadable ones (e.g. kept with another passkey) are dropped, and profiles are shown by their
            // ids until labelled again.
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
        finally
        {
            if (plaintext != null)
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }
        }
    }

    private async Task WriteAsync(string owner, byte[] key, Dictionary<string, string> labels)
    {
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(labels);
        try
        {
            var nonce = RandomNumberGenerator.GetBytes(cipher.NonceSize);
            var ciphertext = cipher.Encrypt(key, nonce, plaintext, Binding(owner));
            var output = new byte[nonce.Length + ciphertext.Length];
            nonce.CopyTo(output, 0);
            ciphertext.CopyTo(output, nonce.Length);
            await localStorageService.SetItemAsync(StorageKeyPrefix + owner, Convert.ToBase64String(output));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private async Task<string> GetOwnerAsync()
    {
        return await userContext.GetUserIdAsync()
            ?? throw new InvalidOperationException("Profile labels are only available when signed in.");
    }
}
