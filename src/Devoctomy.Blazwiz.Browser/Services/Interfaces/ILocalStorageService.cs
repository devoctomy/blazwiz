namespace Devoctomy.Blazwiz.Browser.Services.Interfaces;

/// <summary>
/// Provides access to browser local storage.
/// </summary>
public interface ILocalStorageService
{
    /// <summary>
    /// Gets a value from local storage.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The stored value, or null if not present.</returns>
    Task<string?> GetItemAsync(string key);

    /// <summary>
    /// Stores a value in local storage.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task SetItemAsync(string key, string value);

    /// <summary>
    /// Removes a value from local storage.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task RemoveItemAsync(string key);

    /// <summary>
    /// Gets a JSON serialised object from local storage.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="key">The key.</param>
    /// <returns>The deserialised object, or default if missing or invalid.</returns>
    Task<T?> GetObjectAsync<T>(string key);

    /// <summary>
    /// Stores an object in local storage as JSON.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="key">The key.</param>
    /// <param name="value">The object to store.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task SetObjectAsync<T>(string key, T value);
}
