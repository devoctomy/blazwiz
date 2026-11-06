using Blazing.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Devoctomy.Blazwiz.UI.Models;
using Devoctomy.Blazwiz.UI.Services.Interfaces;

namespace Devoctomy.Blazwiz.UI.ViewModels;

/// <summary>
/// ViewModel for the toast host: the toasts <see cref="IToastService"/> is showing.
/// </summary>
public partial class ToastHostViewModel : ViewModelBase
{
    private readonly IToastService toastService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ToastHostViewModel"/> class.
    /// </summary>
    /// <param name="toastService">The toasts.</param>
    public ToastHostViewModel(IToastService toastService)
    {
        this.toastService = toastService ?? throw new ArgumentNullException(nameof(toastService));
        this.toastService.Changed += OnChanged;
    }

    /// <summary>
    /// Gets the toasts shown, oldest first.
    /// </summary>
    public IReadOnlyList<Toast> Toasts => toastService.Toasts;

    /// <summary>
    /// Takes a toast away.
    /// </summary>
    /// <param name="id">The toast's id.</param>
    [RelayCommand]
    public void Dismiss(Guid id)
    {
        toastService.Dismiss(id);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            toastService.Changed -= OnChanged;
        }

        base.Dispose(disposing);
    }

    private void OnChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(Toasts));
    }
}
