using System.ComponentModel;
using Blazing.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Devoctomy.Blazwiz.Cryptography.Models;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;
using Devoctomy.Blazwiz.UI.Sync.Models;
using Devoctomy.Blazwiz.UI.Sync.Text;
using Microsoft.Extensions.Localization;

namespace Devoctomy.Blazwiz.UI.Sync.ViewModels;

/// <summary>
/// ViewModel for the form a new passphrase is entered in, wherever one is: creating a protected profile, setting a
/// profile's first passphrase, and changing a passphrase. Two tabs: the passphrase (entered twice, with the
/// acknowledgement that it cannot be recovered), and Advanced, the Argon2id settings its key is derived with. Those
/// start at the settings that apply, can be set to anything, and once changed are only accepted after a benchmark has
/// completed with exactly those values. The view model of the dialog showing the form owns this one: it is given one of
/// its own, resets it when the dialog opens, validates it and reads what was entered. The form's view has no view model
/// of its own to follow, so the owner also says when the form changes: on this view model's
/// <see cref="INotifyPropertyChanged.PropertyChanged"/> it raises its own for the property holding the form, and its
/// view shows the form again.
/// </summary>
public partial class E2ePassphraseFormViewModel : ViewModelBase
{
    private readonly ISyncProtectedProfileService profiles;
    private readonly IStringLocalizer<SyncUiText> text;

    private E2ePassphraseFormTab activeTab;
    private bool asksForCurrentPassphrase;
    private string currentPassphrase = string.Empty;
    private string passphrase = string.Empty;
    private string confirmPassphrase = string.Empty;
    private bool acknowledgedNoRecovery;

    /// <summary>
    /// Initializes a new instance of the <see cref="E2ePassphraseFormViewModel"/> class.
    /// </summary>
    /// <param name="profiles">Says which Argon2id settings apply to a profile.</param>
    /// <param name="argon2">The Argon2id settings entered, and their benchmark.</param>
    /// <param name="text">The sync UI's text.</param>
    public E2ePassphraseFormViewModel(ISyncProtectedProfileService profiles, Argon2idBenchmarkViewModel argon2, IStringLocalizer<SyncUiText> text)
    {
        this.profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        Argon2 = argon2 ?? throw new ArgumentNullException(nameof(argon2));
        this.text = text ?? throw new ArgumentNullException(nameof(text));
        Argon2.PropertyChanged += OnArgon2Changed;
    }

    /// <summary>
    /// Gets the Argon2id settings entered on the Advanced tab, and their benchmark.
    /// </summary>
    public Argon2idBenchmarkViewModel Argon2 { get; }

    /// <summary>
    /// Gets a value indicating whether the dialog showing the form may be confirmed: false while the Argon2id settings
    /// were changed and not benchmarked yet, so its confirm button is disabled until they are.
    /// </summary>
    public bool CanConfirm => !Argon2.NeedsBenchmark;

    /// <summary>
    /// Gets the tab shown.
    /// </summary>
    public E2ePassphraseFormTab ActiveTab
    {
        get => activeTab;
        private set => SetProperty(ref activeTab, value);
    }

    /// <summary>
    /// Gets a value indicating whether the form asks for the current passphrase too (changing a passphrase).
    /// </summary>
    public bool AsksForCurrentPassphrase
    {
        get => asksForCurrentPassphrase;
        private set => SetProperty(ref asksForCurrentPassphrase, value);
    }

    /// <summary>
    /// Gets or sets the current passphrase, when changing it.
    /// </summary>
    public string CurrentPassphrase
    {
        get => currentPassphrase;
        set => SetProperty(ref currentPassphrase, value ?? string.Empty);
    }

    /// <summary>
    /// Gets or sets the new passphrase.
    /// </summary>
    public string Passphrase
    {
        get => passphrase;
        set => SetProperty(ref passphrase, value ?? string.Empty);
    }

    /// <summary>
    /// Gets or sets the new passphrase entered again, to confirm it.
    /// </summary>
    public string ConfirmPassphrase
    {
        get => confirmPassphrase;
        set => SetProperty(ref confirmPassphrase, value ?? string.Empty);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the user has confirmed there is no passphrase recovery.
    /// </summary>
    public bool AcknowledgedNoRecovery
    {
        get => acknowledgedNoRecovery;
        set => SetProperty(ref acknowledgedNoRecovery, value);
    }

    /// <summary>
    /// Shows a tab.
    /// </summary>
    /// <param name="tab">The tab.</param>
    [RelayCommand]
    public void ShowTab(E2ePassphraseFormTab tab)
    {
        ActiveTab = tab;
    }

    /// <summary>
    /// Empties the form for a dialog that is opening: no passphrases, the first tab, and the Argon2id settings started
    /// again at the ones that apply (the profile's current ones when it has keys, else its settings' or the app's).
    /// </summary>
    /// <param name="scope">The profile the passphrase is for, or null for a profile about to be created.</param>
    /// <param name="asksForCurrentPassphrase">Whether the current passphrase is asked for too.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task ResetAsync(E2eKeyScope? scope = null, bool asksForCurrentPassphrase = false, CancellationToken ct = default)
    {
        ClearPassphrases();
        AsksForCurrentPassphrase = asksForCurrentPassphrase;
        ActiveTab = E2ePassphraseFormTab.Passphrase;
        Argon2.Reset(await profiles.GetArgon2ParametersAsync(scope, ct), fixKeyLength: true);
    }

    /// <summary>
    /// Forgets the passphrases entered, and the acknowledgement. The Argon2id settings entered are kept.
    /// </summary>
    public void ClearPassphrases()
    {
        CurrentPassphrase = string.Empty;
        Passphrase = string.Empty;
        ConfirmPassphrase = string.Empty;
        AcknowledgedNoRecovery = false;
    }

    /// <summary>
    /// Checks what was entered, and shows the tab with the first thing wrong.
    /// </summary>
    /// <returns>What is wrong, or null when the form can be used.</returns>
    public string? Validate()
    {
        var error = ValidatePassphrases();
        if (error != null)
        {
            ActiveTab = E2ePassphraseFormTab.Passphrase;
            return error;
        }

        error = Argon2.Invalid ?? (Argon2.NeedsBenchmark ? text["Sync.Argon2id.NeedsBenchmark"].Value : null);
        if (error != null)
        {
            ActiveTab = E2ePassphraseFormTab.Advanced;
        }

        return error;
    }

    /// <summary>
    /// Gets the Argon2id settings entered, for the passphrase key. Call once <see cref="Validate"/> finds nothing wrong.
    /// </summary>
    /// <returns>The settings.</returns>
    public Argon2idParameters GetArgon2Parameters() => Argon2.GetParameters();

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Argon2.PropertyChanged -= OnArgon2Changed;
        }

        base.Dispose(disposing);
    }

    // The views showing the form follow this view model alone, so a change to the Argon2id settings is said here too.
    private void OnArgon2Changed(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(Argon2));
        if (e.PropertyName == nameof(Argon2idBenchmarkViewModel.NeedsBenchmark))
        {
            OnPropertyChanged(nameof(CanConfirm));
        }
    }

    private string? ValidatePassphrases()
    {
        if (AsksForCurrentPassphrase && string.IsNullOrEmpty(CurrentPassphrase))
        {
            return text["Sync.E2e.Error.CurrentPassphraseRequired"];
        }

        if (string.IsNullOrEmpty(Passphrase))
        {
            return text["Sync.E2e.Error.PassphraseRequired"];
        }

        if (!string.Equals(Passphrase, ConfirmPassphrase, StringComparison.Ordinal))
        {
            return text["Sync.E2e.Error.PassphrasesDiffer"];
        }

        return AcknowledgedNoRecovery ? null : text["Sync.E2e.Error.AcknowledgeNoRecovery"].Value;
    }
}
