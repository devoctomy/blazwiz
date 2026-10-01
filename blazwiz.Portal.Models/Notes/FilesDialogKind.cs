namespace blazwiz.Portal.Models.Notes;

/// <summary>
/// The dialog shown on the files page.
/// </summary>
public enum FilesDialogKind
{
    /// <summary>
    /// No dialog.
    /// </summary>
    None,

    /// <summary>
    /// Asks for the name of a new note.
    /// </summary>
    NewNote,

    /// <summary>
    /// Asks for the name of a new folder.
    /// </summary>
    NewFolder,

    /// <summary>
    /// Asks for a new name for a note or folder.
    /// </summary>
    Rename,

    /// <summary>
    /// Asks which folder to move a note or folder into.
    /// </summary>
    Move,

    /// <summary>
    /// Asks for confirmation before deleting a note or folder.
    /// </summary>
    Delete,
}
