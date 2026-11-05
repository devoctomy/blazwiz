using System.Text.Json;
using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;
using Devoctomy.Blazwiz.Sync.Interfaces;

namespace Devoctomy.Blazwiz.Sync.Encryption;

/// <summary>
/// Keeps the signed in user's profile labels in local storage, one entry per user, as a JSON object of labels by
/// profile id.
/// </summary>
/// <param name="localStorageService">The local storage.</param>
/// <param name="userContext">The signed in user.</param>
public class LocalStorageE2eProfileLabelStore(ILocalStorageService localStorageService, ISyncUserContext userContext)
    : IE2eProfileLabelStore
{
    /// <summary>
    /// The local storage key prefix, followed by the user id.
    /// </summary>
    public const string StorageKeyPrefix = "e2e_labels_";

    private readonly ILocalStorageService localStorageService = localStorageService ?? throw new ArgumentNullException(nameof(localStorageService));
    private readonly ISyncUserContext userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, string>> GetAllAsync()
    {
        return await ReadAsync(await GetStorageKeyAsync());
    }

    /// <inheritdoc/>
    public async Task SetAsync(string profileId, string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentNullException.ThrowIfNull(label);

        var key = await GetStorageKeyAsync();
        var labels = await ReadAsync(key);
        labels[profileId] = label;
        await localStorageService.SetItemAsync(key, JsonSerializer.Serialize(labels));
    }

    /// <inheritdoc/>
    public async Task RemoveAsync(string profileId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);

        var key = await GetStorageKeyAsync();
        var labels = await ReadAsync(key);
        if (labels.Remove(profileId))
        {
            await localStorageService.SetItemAsync(key, JsonSerializer.Serialize(labels));
        }
    }

    private async Task<Dictionary<string, string>> ReadAsync(string key)
    {
        var json = await localStorageService.GetItemAsync(key);
        if (string.IsNullOrEmpty(json))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        try
        {
            return new Dictionary<string, string>(JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [], StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            // Only labels: unreadable ones are dropped, and profiles are shown by their ids until labelled again.
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    private async Task<string> GetStorageKeyAsync()
    {
        var owner = await userContext.GetUserIdAsync()
            ?? throw new InvalidOperationException("Profile labels are only available when signed in.");
        return StorageKeyPrefix + owner;
    }
}
