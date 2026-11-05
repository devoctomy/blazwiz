using Devoctomy.Blazwiz.Core.Extensions;
using Devoctomy.Blazwiz.UI.Extensions;
using Devoctomy.Blazwiz.UI.Models;
using Microsoft.Extensions.DependencyInjection;
using MyApp.Portal.Application.Services;
using MyApp.Portal.Application.Services.Interfaces;
using MyApp.Portal.Application.Text;

namespace MyApp.Portal.Application.Extensions;

/// <summary>
/// Extension methods for the notes example: markdown notes kept as files in their own sync profile.
/// </summary>
public static class NotesServiceCollectionExtensions
{
    /// <summary>
    /// Adds the notes services and the Files page to the sidebar. Needs the sync module with the notes profile
    /// (<see cref="Sync.NotesSyncProfile"/>).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddNotes(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // The sync module says "files"; the notes example says "notes".
        services.AddBlazwizText(text => text.AddOverrides<NotesText>());
        services.AddScoped<INotesStateStore, IndexedDbNotesStateStore>();
        services.AddScoped<INoteService, NoteService>();
        services.AddSingleton<INoteNameService, NoteNameService>();
        services.AddSingleton<INoteTreeService, NoteTreeService>();
        services.AddSingleton<IMarkdownRenderer, MarkdownRenderer>();
        services.AddSingleton<IMarkdownFormatService, MarkdownFormatService>();
        services.AddNavigationItem(new NavigationItem
        {
            Text = "Files",
            Href = "files",
            Icon = "bi-journal-text",
            Description = "Your markdown notes, organised into folders.",
            Order = 10,
        });
        return services;
    }
}
