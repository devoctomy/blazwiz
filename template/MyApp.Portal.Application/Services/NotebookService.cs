using System.Text;
using System.Text.Json;
using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Configuration;
using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;
using Devoctomy.Blazwiz.UI.Sync.Services.Interfaces;
using MyApp.Portal.Application.Exceptions;
using MyApp.Portal.Application.Services.Interfaces;
using MyApp.Portal.Models.Notes;

namespace MyApp.Portal.Application.Services;

/// <summary>
/// Implementation of <see cref="INotebookService"/>. A created notebook's pages are under its own folder in the
/// user's prefix, and its name is in a <see cref="TitleFile"/> there, encrypted like its pages. Whenever the name is read
/// (or set), it is kept as the profile's label on this device, so the notebook is shown by name while locked here too.
/// </summary>
/// <param name="profiles">The user's protected profiles.</param>
/// <param name="manager">Says which are unlocked.</param>
/// <param name="registry">The sync profiles, for each notebook's file store.</param>
/// <param name="displayNames">The default notebook's display name.</param>
public class NotebookService(
    ISyncProtectedProfileService profiles,
    IE2eProtectedProfileManager manager,
    ISyncProfileRegistry registry,
    ISyncProfileDisplayNames displayNames)
    : INotebookService
{
    /// <summary>
    /// The file in a created notebook's root holding its name.
    /// </summary>
    public const string TitleFile = ".notebook";

    private readonly ISyncProtectedProfileService profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
    private readonly IE2eProtectedProfileManager manager = manager ?? throw new ArgumentNullException(nameof(manager));
    private readonly ISyncProfileRegistry registry = registry ?? throw new ArgumentNullException(nameof(registry));
    private readonly ISyncProfileDisplayNames displayNames = displayNames ?? throw new ArgumentNullException(nameof(displayNames));

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Notebook>> GetNotebooksAsync()
    {
        var notebooks = new List<Notebook>();
        foreach (var profile in profiles.Profiles.Where(IsNotebook))
        {
            notebooks.Add(await DescribeAsync(profile));
        }

        return notebooks;
    }

    /// <inheritdoc/>
    public async Task<Notebook?> FindAsync(string? name)
    {
        var profile = profiles.Profiles.FirstOrDefault(p => IsNotebook(p) && p.Name == (name ?? NoteService.ProfileName));
        return profile == null ? null : await DescribeAsync(profile);
    }

    /// <inheritdoc/>
    public async Task<Notebook> CreateAsync(string title, string passphrase, IProgress<E2eProgress>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new NotesException("Give the notebook a name.");
        }

        var scope = await profiles.CreateAsync(passphrase, progress);
        var json = JsonSerializer.SerializeToUtf8Bytes(new NotebookTitle { Title = title.Trim() });
        await registry.GetFileStore(scope.Id).WriteAsync(RootOf(scope.Id) + TitleFile, json);
        await profiles.SetLabelAsync(scope, title.Trim());
        return await FindAsync(scope.Id)
            ?? throw new InvalidOperationException($"Notebook {scope.Id} was created but is not listed.");
    }

    private static bool IsNotebook(SyncProtectedProfile profile) => profile.IsCreatedByUser || profile.Name == NoteService.ProfileName;

    private static string RootOf(string id) => $"{SyncDefaults.ProtectedProfilesFolder}/{id}/";

    private async Task<Notebook> DescribeAsync(SyncProtectedProfile profile)
    {
        var isDefault = !profile.IsCreatedByUser;
        var notebook = new Notebook
        {
            Name = profile.Name,
            Id = profile.Scope.Id,
            Root = isDefault ? NoteService.DefaultRoot : RootOf(profile.Scope.Id),
            IsDefault = isDefault,
            HasPassphrase = profile.IsSetUp,
            IsUnlocked = manager.IsUnlocked(profile.Scope),
        };
        if (isDefault)
        {
            notebook.Title = displayNames.GetDisplayName(profile.Name);
            return notebook;
        }

        var title = notebook.IsUnlocked ? await ReadTitleAsync(notebook) : null;
        if (title != null && title != profile.Label)
        {
            await profiles.SetLabelAsync(profile.Scope, title);
        }

        notebook.Title = title ?? profile.Label ?? notebook.Id;
        return notebook;
    }

    private async Task<string?> ReadTitleAsync(Notebook notebook)
    {
        try
        {
            var file = await registry.GetFileStore(notebook.Name).ReadAsync(notebook.Root + TitleFile);
            return file == null ? null : JsonSerializer.Deserialize<NotebookTitle>(Encoding.UTF8.GetString(file.Content))?.Title;
        }
        catch (Exception)
        {
            // Not synced here yet, or locked meanwhile: shown by its id until it can be read.
            return null;
        }
    }

    private sealed class NotebookTitle
    {
        public string Title { get; set; } = string.Empty;
    }
}
