using Devoctomy.Blazwiz.Sync.Models;

namespace Devoctomy.Blazwiz.Sync.Interfaces;

/// <summary>
/// The folder the signed in user picked on their computer for <see cref="SyncStorageKind.LocalFolder"/>: picking it,
/// and getting permission to it again after a reload.
/// </summary>
public interface ISyncFolderService
{
    /// <summary>
    /// Gets whether the browser can pick folders (Chromium desktop).
    /// </summary>
    /// <returns>True if it can.</returns>
    Task<bool> IsSupportedAsync();

    /// <summary>
    /// Asks the user to pick a folder for their files, replacing any picked before. Must be called straight after a
    /// click, before anything else is awaited.
    /// </summary>
    /// <returns>The folder's name, or null if the user cancelled.</returns>
    Task<string?> PickAsync();

    /// <summary>
    /// Gets the picked folder and whether the app may use it now.
    /// </summary>
    /// <returns>The status, or null if no folder has been picked.</returns>
    Task<SyncFolderStatus?> GetStatusAsync();

    /// <summary>
    /// Gets whether a profile keeps its files in the picked folder but the browser needs permission to it again.
    /// </summary>
    /// <returns>True if the user must reconnect the folder.</returns>
    Task<bool> NeedsReconnectAsync();

    /// <summary>
    /// Asks for permission to the picked folder again. Must be called straight after a click.
    /// </summary>
    /// <returns>True if permission was granted.</returns>
    Task<bool> ReconnectAsync();
}
