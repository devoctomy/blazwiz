namespace Devoctomy.Blazwiz.UI.Sync.ViewModels;

/// <summary>
/// The page showing a file's history (<see cref="FileHistoryViewModel"/>): usually its editor, which may have unsaved
/// changes. With unsaved changes, picking a past version first asks whether to save them.
/// </summary>
public interface IFileHistoryHost
{
    /// <summary>
    /// Gets a value indicating whether the page has unsaved changes to the file.
    /// </summary>
    bool HasUnsavedChanges { get; }

    /// <summary>
    /// Saves the page's changes to the file, as its own Save does.
    /// </summary>
    /// <returns>True when they were saved; false keeps the page as it is (it says why).</returns>
    Task<bool> SaveChangesAsync();

    /// <summary>
    /// Drops the page's unsaved changes to the file.
    /// </summary>
    void DiscardChanges();
}
