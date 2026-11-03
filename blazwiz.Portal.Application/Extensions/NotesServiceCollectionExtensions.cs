using blazwiz.Core.Extensions;
using blazwiz.Core.Sync.Configuration;
using blazwiz.Core.Sync.Models;
using blazwiz.Portal.Application.Services;
using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Application.Text;
using blazwiz.Portal.Models.Navigation;
using Microsoft.Extensions.DependencyInjection;

namespace blazwiz.Portal.Application.Extensions;

/// <summary>
/// Extension methods for the notes example: markdown notes kept as files in their own sync profile.
/// </summary>
public static class NotesServiceCollectionExtensions
{
    /// <summary>
    /// Adds the notes sync profile: plain markdown files kept in IndexedDB, OPFS or a folder the user picks, and synced
    /// with <c>notes/</c> in the user's prefix, encrypted when end to end encryption is on.
    /// </summary>
    /// <param name="sync">The sync configuration.</param>
    /// <returns>The sync configuration for method chaining.</returns>
    public static SyncBuilder AddNotesProfile(this SyncBuilder sync)
    {
        ArgumentNullException.ThrowIfNull(sync);

        return sync.AddProfile(NoteService.ProfileName, profile => profile
            .UseIndexedDbIndexStore(database: "sync", store: "notes_index")
            .UseFileStores(stores => stores
                .AddIndexedDb(database: IndexedDbNotesStateStore.Database, store: "files")
                .AddOpfs(root: "notes")
                .AddLocalFolder()
                .UseDefault(SyncStorageKind.IndexedDb))
            .UseRemote("notes/**/*.md", includeEmptyFolders: true)
            .UseEncryption());
    }

    /// <summary>
    /// Adds the notes services and the Files page to the sidebar. Needs the sync module with
    /// <see cref="AddNotesProfile"/>.
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
