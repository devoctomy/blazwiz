using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Devoctomy.Blazwiz.Browser.Models;
using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Microsoft.JSInterop;

namespace Devoctomy.Blazwiz.Browser.Services;

/// <summary>
/// <see cref="IDeviceNameService"/> keeping the name in local storage, with the platform and browser for the default
/// name read by <c>js/device.js</c>.
/// </summary>
public sealed class DeviceNameService(IJSRuntime jsRuntime, ILocalStorageService localStorageService)
    : IDeviceNameService
{
    /// <summary>
    /// The longest name allowed.
    /// </summary>
    public const int MaxLength = 32;

    /// <summary>
    /// The local storage key the name is stored under (the storage client adds the app's prefix).
    /// </summary>
    public const string StorageKey = "device_name";

    /// <summary>
    /// The module's path.
    /// </summary>
    public const string ModulePath = "./_content/Devoctomy.Blazwiz.Browser/js/device.js";

    /// <summary>
    /// Stands in for a platform or browser that is not recognised.
    /// </summary>
    public const string Unknown = "Unknown";

    // Each part of the default name is kept short, so the name stays within MaxLength with its digits.
    private const int MaxPartLength = 12;

    private readonly IJSRuntime jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
    private readonly ILocalStorageService localStorageService = localStorageService ?? throw new ArgumentNullException(nameof(localStorageService));
    private string? name;

    /// <inheritdoc/>
    public async Task<string> GetNameAsync()
    {
        if (name != null)
        {
            return name;
        }

        var stored = Sanitize(await localStorageService.GetItemAsync(StorageKey));
        if (stored != null)
        {
            name = stored;
            return stored;
        }

        var made = await MakeDefaultNameAsync();
        await localStorageService.SetItemAsync(StorageKey, made);
        name = made;
        return made;
    }

    /// <inheritdoc/>
    public async Task SetNameAsync(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var trimmed = name.Trim();
        if (trimmed.Length == 0 || trimmed.Length > MaxLength || trimmed.Any(char.IsControl))
        {
            throw new ArgumentException($"A device name must be 1 to {MaxLength} characters, with no control characters.", nameof(name));
        }

        await localStorageService.SetItemAsync(StorageKey, trimmed);
        this.name = trimmed;
    }

    /// <inheritdoc/>
    public string? Sanitize(string? name)
    {
        if (name == null)
        {
            return null;
        }

        var builder = new StringBuilder(name.Length);
        foreach (var c in name.Where(c => !char.IsControl(c)))
        {
            builder.Append(c);
        }

        var cleaned = builder.ToString().Trim();
        if (cleaned.Length > MaxLength)
        {
            cleaned = cleaned[..MaxLength].TrimEnd();
        }

        return cleaned.Length == 0 ? null : cleaned;
    }

    private static string Part(string? value)
    {
        var letters = new string((value ?? string.Empty).Where(char.IsAsciiLetterOrDigit).ToArray());
        if (letters.Length == 0)
        {
            return Unknown;
        }

        return letters.Length > MaxPartLength ? letters[..MaxPartLength] : letters;
    }

    private async Task<string> MakeDefaultNameAsync()
    {
        DeviceDescription? description = null;
        try
        {
            var module = await jsRuntime.InvokeAsync<IJSObjectReference>("import", ModulePath);
            await using (module)
            {
                description = await module.InvokeAsync<DeviceDescription?>("describe");
            }
        }
        catch (JSException)
        {
            // Not knowing the platform or browser only makes the name less helpful.
        }

        var digits = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
        return $"{Part(description?.Platform)}-{Part(description?.Browser)}-{digits}";
    }
}
