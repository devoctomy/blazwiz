namespace Devoctomy.Blazwiz.Core.Configuration;

/// <summary>
/// Identifies the app built on blazwiz. Set once at startup with
/// <see cref="Extensions.CoreServiceCollectionExtensions.AddBlazwizApp"/>.
/// </summary>
public class BlazwizAppOptions
{
    /// <summary>
    /// Gets or sets the app's name: lower case letters and numbers, starting with a letter (e.g. <c>acmeportal</c>).
    /// Everything the app keeps in the browser is named after it, so apps on the same origin do not collide.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the app's unique ID (a GUID). Keys derived from WebAuthn are salted with it, so changing it makes
    /// everything encrypted with them unreadable.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets the prefix given to every name the app keeps in the browser: local storage keys, IndexedDB databases, OPFS
    /// folders, Web Locks and broadcast channels.
    /// </summary>
    public string StorageKeyPrefix => $"{Name}_";
}
