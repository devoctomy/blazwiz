namespace MyApp.Portal.Models.Notes;

/// <summary>
/// Per user state of the notes feature, kept alongside the file index.
/// </summary>
public class NotesState
{
    /// <summary>
    /// Gets or sets the username the state belongs to.
    /// </summary>
    public string Owner { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the welcome note has been created, so it is not created again after being deleted.
    /// </summary>
    public bool WelcomeNoteCreated { get; set; }
}
