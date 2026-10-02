namespace blazwiz.Portal.Application.Services.Interfaces;

/// <summary>
/// Converts notes kept by earlier versions of the app (an index of entries plus separate content) to plain files at
/// their folder paths, marked as changed so they upload, then removes the old object stores.
/// </summary>
public interface INoteMigrationService
{
    /// <summary>
    /// Migrates a user's old notes, if there are any. Only does the work once per session.
    /// </summary>
    /// <param name="owner">The username.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task MigrateAsync(string owner);
}
