using Blazing.Mvvm.Components;
using Microsoft.AspNetCore.Components;
using MyApp.Portal.Application.ViewModels;

namespace MyApp.Portal.Pages;

/// <summary>
/// Code-behind for the files page: the pages of a notebook. Logic lives in <see cref="FilesViewModel"/>.
/// </summary>
public partial class Files : MvvmComponentBase<FilesViewModel>
{
    /// <summary>
    /// Gets or sets the notebook shown (its sync profile name), or null for the default notebook.
    /// </summary>
    [SupplyParameterFromQuery(Name = NoteEditorViewModel.NotebookQueryParameter)]
    public string? NotebookName { get; set; }

    /// <inheritdoc/>
    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();
        await ViewModel.OpenNotebookAsync(NotebookName);
    }
}
