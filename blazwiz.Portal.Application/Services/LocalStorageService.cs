using System.Text.Json;
using blazwiz.Portal.Application.Services.Interfaces;
using Microsoft.JSInterop;

namespace blazwiz.Portal.Application.Services;

/// <inheritdoc/>
public class LocalStorageService(IJSRuntime jsRuntime)
    : ILocalStorageService
{
    private readonly IJSRuntime jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));

    /// <inheritdoc/>
    public async Task<string?> GetItemAsync(string key)
    {
        return await jsRuntime.InvokeAsync<string?>("localStorage.getItem", key);
    }

    /// <inheritdoc/>
    public async Task SetItemAsync(string key, string value)
    {
        await jsRuntime.InvokeVoidAsync("localStorage.setItem", key, value);
    }

    /// <inheritdoc/>
    public async Task RemoveItemAsync(string key)
    {
        await jsRuntime.InvokeVoidAsync("localStorage.removeItem", key);
    }

    /// <inheritdoc/>
    public async Task<T?> GetObjectAsync<T>(string key)
    {
        var json = await GetItemAsync(key);
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    /// <inheritdoc/>
    public async Task SetObjectAsync<T>(string key, T value)
    {
        await SetItemAsync(key, JsonSerializer.Serialize(value));
    }
}
