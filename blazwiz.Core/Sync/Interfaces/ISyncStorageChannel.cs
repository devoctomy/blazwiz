using blazwiz.Core.Sync.Models;

namespace blazwiz.Core.Sync.Interfaces;

/// <summary>
/// Tells the app's other tabs when files are being moved to another storage, through <c>wwwroot/js/channel.js</c>.
/// </summary>
public interface ISyncStorageChannel
{
    /// <summary>
    /// Raised when another tab posts a message.
    /// </summary>
    event EventHandler<SyncStorageChannelMessage>? MessageReceived;

    /// <summary>
    /// Starts listening to other tabs. Safe to call more than once.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task StartAsync();

    /// <summary>
    /// Stops listening to other tabs.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task StopAsync();

    /// <summary>
    /// Posts a message to the other tabs.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task PostAsync(SyncStorageChannelMessage message);
}
