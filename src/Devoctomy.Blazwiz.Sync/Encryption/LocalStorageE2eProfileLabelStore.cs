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
/// id, encrypted (AES-GCM) with <see cref="E2eKeyNames.LabelKey"/>, the key derived from the user's passkey when they
/// sign in with it. A label can say what the profile holds, so it is never kept in plain text.
/// </summary>
/// <remarks>
/// Without that key (signed in with a password, or after a reload) labels can't be read or kept, so profiles are shown
/// by their ids, and a label set then lasts only until the page is closed (<see cref="ISyncProtectedProfileService"/>
/// keeps it in memory). Removing a label without the key removes every label of the user on this device, as the
/// entry can't be changed; the others are set again as their profiles are unlocked.
/// </remarks>
/// <param name="localStorageService">The local storage.</param>
/// <param name="userContext">The signed in user.</param>
/// <param name="cryptoService">Encrypts and decrypts the labels.</param>
/// <param name="keyStore">Holds the key derived from the passkey, if the user signed in with it.</param>
public class LocalStorageE2eProfileLabelStore(
    ILocalStorageService localStorageService,
    ISyncUserContext userContext,
    ICryptoService cryptoService,
    IDerivedKeyStore keyStore)
    : IE2eProfileLabelStore
{
    /// <summary>
    /// The local storage key prefix, followed by the user id.
    /// </summary>
    public const string StorageKeyPrefix = "e2e_labels_";

    private readonly ILocalStorageService localStorageService = localStorageService ?? throw new ArgumentNullException(nameof(localStorageService));
    private readonly ISyncUserContext userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
    private readonly ICryptoService cryptoService = cryptoService ?? throw new ArgumentNullException(nameof(cryptoService));
    private readonly IDerivedKeyStore keyStore = keyStore ?? throw new ArgumentNullException(nameof(keyStore));

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, string>> GetAllAsync()
    {
        var storageKey = await GetStorageKeyAsync();
        var key = GetKey();
        return key == null ? new Dictionary<string, string>(StringComparer.Ordinal) : await ReadAsync(storageKey, key);
    }

    /// <inheritdoc/>
    public async Task SetAsync(string profileId, string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentNullException.ThrowIfNull(label);

        var storageKey = await GetStorageKeyAsync();
        var key = GetKey();
        if (key == null)
        {
            return;
        }

        var labels = await ReadAsync(storageKey, key);
        labels[profileId] = label;
        await WriteAsync(storageKey, key, labels);
    }

    /// <inheritdoc/>
    public async Task RemoveAsync(string profileId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);

        var storageKey = await GetStorageKeyAsync();
        var key = GetKey();
        if (key == null)
        {
            await localStorageService.RemoveItemAsync(storageKey);
            return;
        }

        var labels = await ReadAsync(storageKey, key);
        if (labels.Remove(profileId))
        {
            await WriteAsync(storageKey, key, labels);
        }
    }

    private string? GetKey()
    {
        if (!keyStore.TryGetKey(E2eKeyNames.LabelKey, out var key))
        {
            return null;
        }

        try
        {
            return Convert.ToBase64String(key);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private async Task<Dictionary<string, string>> ReadAsync(string storageKey, string key)
    {
        var stored = await localStorageService.GetItemAsync(storageKey);
        if (string.IsNullOrEmpty(stored))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        try
        {
            using var input = new MemoryStream(Convert.FromBase64String(stored));
            using var output = new MemoryStream();
            await cryptoService.DecryptAsync(input, output, key);
            var labels = JsonSerializer.Deserialize<Dictionary<string, string>>(output.ToArray()) ?? [];
            return new Dictionary<string, string>(labels, StringComparer.Ordinal);
        }
        catch (Exception ex) when (ex is FormatException or JsonException or InvalidOperationException or CryptographicException)
        {
            // Only labels: unreadable ones (e.g. kept with another passkey) are dropped, and profiles are shown by their
            // ids until labelled again.
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    private async Task WriteAsync(string storageKey, string key, Dictionary<string, string> labels)
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(labels)));
        using var output = new MemoryStream();
        await cryptoService.EncryptAsync(input, output, key);
        await localStorageService.SetItemAsync(storageKey, Convert.ToBase64String(output.ToArray()));
    }

    private async Task<string> GetStorageKeyAsync()
    {
        var owner = await userContext.GetUserIdAsync()
            ?? throw new InvalidOperationException("Profile labels are only available when signed in.");
        return StorageKeyPrefix + owner;
    }
}
