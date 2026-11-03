namespace Devoctomy.Blazwiz.Sync.Models;

/// <summary>
/// An object in the remote store, with its content.
/// </summary>
public class RemoteObject : RemoteObjectInfo
{
    /// <summary>
    /// Gets or sets the content.
    /// </summary>
    public byte[] Content { get; set; } = [];
}
