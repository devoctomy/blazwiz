using Devoctomy.Blazwiz.Encryption.Models;
using MyApp.Portal.Models.Notes;

namespace MyApp.Portal.Application.Services.Interfaces;

/// <summary>
/// The user's notebooks: the default notebook (the notes profile) and the protected profiles they create as notebooks,
/// each with its own passphrase and keys. A notebook's name is kept encrypted inside it, and on each device where it
/// has been unlocked as the profile's label there.
/// </summary>
public interface INotebookService
{
    /// <summary>
    /// Gets the user's notebooks: the default notebook first, then the ones they created. Empty until their profiles
    /// are loaded.
    /// </summary>
    /// <returns>The notebooks.</returns>
    Task<IReadOnlyList<Notebook>> GetNotebooksAsync();

    /// <summary>
    /// Gets a notebook by its sync profile's name, or the default notebook.
    /// </summary>
    /// <param name="name">The sync profile's name, or null for the default notebook.</param>
    /// <returns>The notebook, or null when there is no such notebook (yet).</returns>
    Task<Notebook?> FindAsync(string? name);

    /// <summary>
    /// Creates a notebook: a protected profile with its own passphrase, with the name kept encrypted inside it. It is
    /// unlocked.
    /// </summary>
    /// <param name="title">The notebook's name.</param>
    /// <param name="passphrase">Its passphrase. There is no recovery if it is lost.</param>
    /// <param name="progress">Told about each step of creating its keys.</param>
    /// <returns>The new notebook.</returns>
    Task<Notebook> CreateAsync(string title, string passphrase, IProgress<E2eProgress>? progress = null);

    /// <summary>
    /// Unlocks a notebook with biometrics (<see cref="Notebook.CanUnlockWithBiometrics"/>), syncing it first when
    /// online and asking for the passkey if its key is not in memory.
    /// </summary>
    /// <param name="notebook">The notebook.</param>
    /// <returns>True if it was unlocked; false if the passkey was cancelled or could not unlock it.</returns>
    Task<bool> UnlockWithBiometricsAsync(Notebook notebook);

    /// <summary>
    /// Stops unlocking a notebook with biometrics on this device. Its passphrase still unlocks it.
    /// </summary>
    /// <param name="notebook">The notebook.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task ForgetBiometricsAsync(Notebook notebook);
}
