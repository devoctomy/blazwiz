using System.ComponentModel;
using Devoctomy.Blazwiz.UI.Sync.ViewModels;
using Microsoft.AspNetCore.Components;

namespace Devoctomy.Blazwiz.UI.Sync.Components;

/// <summary>
/// The form a new passphrase is entered in, in two tabs: the passphrase, and Advanced (the Argon2id settings its key is
/// derived with, and their benchmark). Put it in the form of any dialog that sets a passphrase, with the dialog's own
/// fields as its content, shown at the top of the first tab:
/// <c>&lt;E2ePassphraseForm Form="ViewModel.PassphraseForm" IdPrefix="photos"&gt;...&lt;/E2ePassphraseForm&gt;</c>. It
/// shows the view model it is given, which the dialog's view model owns. Logic lives in
/// <see cref="E2ePassphraseFormViewModel"/>.
/// </summary>
public partial class E2ePassphraseForm : ComponentBase, IDisposable
{
    private E2ePassphraseFormViewModel? subscribed;

    /// <summary>
    /// Gets or sets the form's view model, owned by the view model of the dialog showing it.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public E2ePassphraseFormViewModel Form { get; set; } = default!;

    /// <summary>
    /// Gets or sets what every element id here starts with, e.g. <c>e2e</c> for <c>e2e-passphrase</c>.
    /// </summary>
    [Parameter]
    public string IdPrefix { get; set; } = "e2e";

    /// <summary>
    /// Gets or sets the first tab's title, when it holds more than the passphrase; "Passphrase" otherwise.
    /// </summary>
    [Parameter]
    public string? FirstTabTitle { get; set; }

    /// <summary>
    /// Gets or sets the text of the acknowledgement that the passphrase cannot be recovered, when the app words it for
    /// what the passphrase protects; the framework's otherwise.
    /// </summary>
    [Parameter]
    public string? AcknowledgeText { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the fields are disabled, e.g. while the passphrase is being set.
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// Gets or sets the dialog's own content, shown at the top of the first tab, above the passphrase fields.
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    protected override void OnParametersSet()
    {
        if (ReferenceEquals(subscribed, Form))
        {
            return;
        }

        Unsubscribe();
        subscribed = Form;
        subscribed.PropertyChanged += OnFormChanged;
        subscribed.Argon2.PropertyChanged += OnFormChanged;
    }

    /// <summary>
    /// Stops listening to the form.
    /// </summary>
    /// <param name="disposing">Whether called from <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            Unsubscribe();
        }
    }

    private void Unsubscribe()
    {
        if (subscribed != null)
        {
            subscribed.PropertyChanged -= OnFormChanged;
            subscribed.Argon2.PropertyChanged -= OnFormChanged;
            subscribed = null;
        }
    }

    private void OnFormChanged(object? sender, PropertyChangedEventArgs e)
    {
        _ = InvokeAsync(StateHasChanged);
    }
}
