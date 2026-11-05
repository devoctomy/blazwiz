using MyApp.Portal.Models.Notes;

namespace MyApp.Portal.Application.Services.Interfaces;

/// <summary>
/// Creates the note service of a notebook.
/// </summary>
public interface INoteServiceFactory
{
    /// <summary>
    /// Creates the note service working on a notebook's pages.
    /// </summary>
    /// <param name="notebook">The notebook.</param>
    /// <returns>The note service.</returns>
    INoteService Create(Notebook notebook);
}
