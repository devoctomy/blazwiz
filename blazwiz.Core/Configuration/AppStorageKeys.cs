using blazwiz.Core.Configuration.Interfaces;

namespace blazwiz.Core.Configuration;

/// <inheritdoc/>
public class AppStorageKeys(BlazwizAppOptions options)
    : IAppStorageKeys
{
    private readonly BlazwizAppOptions options = options ?? throw new ArgumentNullException(nameof(options));

    /// <inheritdoc/>
    public string Prefix => options.StorageKeyPrefix;

    /// <inheritdoc/>
    public string For(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Prefix + name;
    }
}
