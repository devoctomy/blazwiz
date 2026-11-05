using Devoctomy.Blazwiz.Auth.Extensions;
using Devoctomy.Blazwiz.Core.Extensions;
using Devoctomy.Blazwiz.UI.Extensions;
using Devoctomy.Blazwiz.UI.Models;
using Microsoft.Extensions.DependencyInjection;
using MyApp.Portal.Application.Services;
using MyApp.Portal.Application.Services.Interfaces;
using MyApp.Portal.Application.Text;

namespace MyApp.Portal.Application.Extensions;

/// <summary>
/// Extension methods for the notes example: notebooks of markdown pages, each a sync profile with its own passphrase.
/// </summary>
public static class NotesServiceCollectionExtensions
{
    /// <summary>
    /// Adds the notes services and the Notebooks page to the sidebar. Needs the sync module with the notes profile
    /// (<see cref="Sync.NotesSyncProfile"/>), which is the default notebook.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddNotes(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // The sync module says "files"; the notes example says "notes".
        services.AddBlazwizText(text => text.AddOverrides<NotesText>());
        services.AddScoped<INotesStateStore, IndexedDbNotesStateStore>();
        services.AddScoped<INoteServiceFactory, NoteServiceFactory>();
        services.AddScoped<INotebookService, NotebookService>();
        services.AddSessionLifecycleHandler<NotebookLabelHandler>();
        services.AddSingleton<INoteNameService, NoteNameService>();
        services.AddSingleton<INoteTreeService, NoteTreeService>();
        services.AddSingleton<IMarkdownRenderer, MarkdownRenderer>();
        services.AddSingleton<IMarkdownFormatService, MarkdownFormatService>();
        services.AddNavigationItem(new NavigationItem
        {
            Text = "Notebooks",
            Href = "notebooks",
            Icon = "bi-journals",
            Description = "Your notebooks: markdown pages, organised into folders, each notebook with its own passphrase.",
            Order = 10,
        });
        return services;
    }
}
