using System.ComponentModel;
using Blazing.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.UI.Text;
using Devoctomy.Blazwiz.UI.Text.Interfaces;
using Microsoft.Extensions.Localization;

namespace Devoctomy.Blazwiz.UI.ViewModels;

/// <summary>
/// ViewModel for the Two-step verification panel of the Profile page: turning a code from an authenticator app on
/// (set up with <see cref="Setup"/>, then confirmed with a first code) or off. Shown only for an online session, since
/// it needs the sign-in service.
/// </summary>
public partial class MfaSettingsViewModel : ViewModelBase
{
    private readonly IMfaService mfaService;
    private readonly IAuthenticationService authService;
    private readonly IStringLocalizer<UiText> text;
    private readonly IErrorText errorText;
    private string account = string.Empty;
    private bool isAvailable;
    private bool isEnabled;
    private bool isBusy;
    private bool isConfirmingDisable;
    private string code = string.Empty;
    private string? errorMessage;
    private string? successMessage;

    /// <summary>
    /// Initializes a new instance of the <see cref="MfaSettingsViewModel"/> class.
    /// </summary>
    /// <param name="mfaService">Turns two-step verification on and off.</param>
    /// <param name="authService">The signed-in user.</param>
    /// <param name="setup">Shows the secret to set up the authenticator app with.</param>
    /// <param name="text">The UI's text.</param>
    /// <param name="errorText">Describes errors.</param>
    public MfaSettingsViewModel(
        IMfaService mfaService,
        IAuthenticationService authService,
        AuthenticatorSetupViewModel setup,
        IStringLocalizer<UiText> text,
        IErrorText errorText)
    {
        this.mfaService = mfaService ?? throw new ArgumentNullException(nameof(mfaService));
        this.authService = authService ?? throw new ArgumentNullException(nameof(authService));
        Setup = setup ?? throw new ArgumentNullException(nameof(setup));
        this.text = text ?? throw new ArgumentNullException(nameof(text));
        this.errorText = errorText ?? throw new ArgumentNullException(nameof(errorText));
        Setup.PropertyChanged += OnSetupChanged;
    }

    /// <summary>
    /// Gets the authenticator app set-up, shown while turning two-step verification on.
    /// </summary>
    public AuthenticatorSetupViewModel Setup { get; }

    /// <summary>
    /// Gets a value indicating whether the panel is shown: the session is online and its state could be read.
    /// </summary>
    public bool IsAvailable
    {
        get => isAvailable;
        private set => SetProperty(ref isAvailable, value);
    }

    /// <summary>
    /// Gets a value indicating whether two-step verification is on.
    /// </summary>
    public bool IsEnabled
    {
        get => isEnabled;
        private set => SetProperty(ref isEnabled, value);
    }

    /// <summary>
    /// Gets a value indicating whether a change is under way.
    /// </summary>
    public bool IsBusy
    {
        get => isBusy;
        private set => SetProperty(ref isBusy, value);
    }

    /// <summary>
    /// Gets a value indicating whether the confirmation for turning it off is shown.
    /// </summary>
    public bool IsConfirmingDisable
    {
        get => isConfirmingDisable;
        private set => SetProperty(ref isConfirmingDisable, value);
    }

    /// <summary>
    /// Gets or sets the first code from the authenticator app, to confirm the set-up.
    /// </summary>
    public string Code
    {
        get => code;
        set => SetProperty(ref code, value);
    }

    /// <summary>
    /// Gets the error from the last change, if any.
    /// </summary>
    public string? ErrorMessage
    {
        get => errorMessage;
        private set => SetProperty(ref errorMessage, value);
    }

    /// <summary>
    /// Gets the confirmation of the last change, if any.
    /// </summary>
    public string? SuccessMessage
    {
        get => successMessage;
        private set => SetProperty(ref successMessage, value);
    }

    /// <inheritdoc/>
    public override async Task OnInitializedAsync()
    {
        var session = await authService.GetCurrentUserAsync();
        if (session == null || session.IsOffline)
        {
            return;
        }

        account = session.Email ?? session.Username;
        try
        {
            IsEnabled = await mfaService.IsEnabledAsync();
            IsAvailable = true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Unreachable or refused: the panel stays hidden, as it is offline.
        }
    }

    /// <summary>
    /// Starts turning two-step verification on: shows a new secret for the authenticator app.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public Task BeginSetupAsync()
    {
        return RunAsync(async () =>
        {
            var secret = await mfaService.BeginSetupAsync();
            Code = string.Empty;
            Setup.Show(secret, account);
        });
    }

    /// <summary>
    /// Confirms the authenticator app with its first code, which turns two-step verification on.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public Task ConfirmSetupAsync()
    {
        return RunAsync(async () =>
        {
            await mfaService.CompleteSetupAsync(Code);
            Code = string.Empty;
            Setup.Clear();
            IsEnabled = true;
            SuccessMessage = text["Profile.Mfa.TurnedOn"];
        });
    }

    /// <summary>
    /// Abandons turning two-step verification on.
    /// </summary>
    [RelayCommand]
    public void CancelSetup()
    {
        Code = string.Empty;
        ErrorMessage = null;
        Setup.Clear();
    }

    /// <summary>
    /// Shows the confirmation for turning two-step verification off.
    /// </summary>
    [RelayCommand]
    public void AskDisable()
    {
        ErrorMessage = null;
        SuccessMessage = null;
        IsConfirmingDisable = true;
    }

    /// <summary>
    /// Closes the confirmation without doing anything.
    /// </summary>
    [RelayCommand]
    public void CancelDisable()
    {
        IsConfirmingDisable = false;
    }

    /// <summary>
    /// Turns two-step verification off.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public Task DisableAsync()
    {
        return RunAsync(async () =>
        {
            try
            {
                await mfaService.DisableAsync();
                IsEnabled = false;
                SuccessMessage = text["Profile.Mfa.TurnedOff"];
            }
            finally
            {
                IsConfirmingDisable = false;
            }
        });
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Setup.PropertyChanged -= OnSetupChanged;
        }

        base.Dispose(disposing);
    }

    private async Task RunAsync(Func<Task> change)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        SuccessMessage = null;
        try
        {
            await change();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ErrorMessage = errorText.Describe(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    // The set-up is shown by a component given it as a parameter: it is shown again when this one says it changed.
    private void OnSetupChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(Setup));
    }
}
