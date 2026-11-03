namespace Devoctomy.Blazwiz.Sync.Models;

/// <summary>
/// The folder a user picked on their computer, and whether the app may use it now.
/// </summary>
public class SyncFolderStatus
{
    /// <summary>
    /// The permission value meaning the app may read and write the folder.
    /// </summary>
    public const string Granted = "granted";

    /// <summary>
    /// Gets or sets the folder's name (the browser does not reveal its full path).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the browser's permission: <c>granted</c>, <c>prompt</c> (ask again after a click) or <c>denied</c>.
    /// </summary>
    public string Permission { get; set; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the app may read and write the folder now.
    /// </summary>
    public bool IsGranted => Permission == Granted;
}
