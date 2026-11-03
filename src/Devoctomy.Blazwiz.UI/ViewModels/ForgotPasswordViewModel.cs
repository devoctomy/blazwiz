using Blazing.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Devoctomy.Blazwiz.Auth.Exceptions;
using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.UI.Models;
using Devoctomy.Blazwiz.UI.Text.Interfaces;

namespace Devoctomy.Blazwiz.UI.ViewModels;

/// <summary>
/// ViewModel for resetting a forgotten password: ask for a code by email, then set a new password with it.
/// </summary>
public partial class ForgotPasswordViewModel : ViewModelBase
{
    private readonly IPasswordService passwordService;
    private readonly INavigationService navigationService;
    private readonly IErrorText errorText;
    private ForgotPasswordStep step = ForgotPasswordStep.Request;
    private string email = string.Empty;
    private string code = string.Empty;
    private string newPassword = string.Empty;
    private string confirmNewPassword = string.Empty;
    private bool isBusy;
    private string? errorMessage;

    /// <summary>
    /// Initializes a new instance of the <see cref="ForgotPasswordViewModel"/> class.
    /// </summary>
    /// <param name="passwordService">Sends the code and resets the password.</param>
    /// <param name="navigationService">Returns to the sign-in page.</param>
    /// <param name="errorText">Describes errors.</param>
    /// <param name="appSettings">The app's title, shown with the form.</param>
    public ForgotPasswordViewModel(IPasswordService passwordService, INavigationService navigationService, IErrorText errorText, AppSettings appSettings)
    {
        this.passwordService = passwordService ?? throw new ArgumentNullException(nameof(passwordService));
        this.navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        this.errorText = errorText ?? throw new ArgumentNullException(nameof(errorText));
        AppSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
    }

    /// <summary>
    /// Gets the app's settings (its title).
    /// </summary>
    public AppSettings AppSettings { get; }

    /// <summary>
    /// Gets the step the user is on.
    /// </summary>
    public ForgotPasswordStep Step { get => step; private set => SetProperty(ref step, value); }

    /// <summary>
    /// Gets or sets the account's email address.
    /// </summary>
    public string Email { get => email; set => SetProperty(ref email, value); }

    /// <summary>
    /// Gets or sets the verification code from the email.
    /// </summary>
    public string Code { get => code; set => SetProperty(ref code, value); }

    /// <summary>
    /// Gets or sets the new password.
    /// </summary>
    public string NewPassword { get => newPassword; set => SetProperty(ref newPassword, value); }

    /// <summary>
    /// Gets or sets the new password again, to catch typing mistakes.
    /// </summary>
    public string ConfirmNewPassword { get => confirmNewPassword; set => SetProperty(ref confirmNewPassword, value); }

    /// <summary>
    /// Gets a value indicating whether a request is in progress.
    /// </summary>
    public bool IsBusy { get => isBusy; private set => SetProperty(ref isBusy, value); }

    /// <summary>
    /// Gets why the last step failed, if it did.
    /// </summary>
    public string? ErrorMessage { get => errorMessage; private set => SetProperty(ref errorMessage, value); }

    /// <summary>
    /// Sends a verification code to the email address, then asks for it.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task RequestCodeAsync()
    {
        await RunAsync(async () =>
        {
            await passwordService.RequestPasswordResetAsync(Email);
            Code = string.Empty;
            Step = ForgotPasswordStep.Reset;
        });
    }

    /// <summary>
    /// Sets the new password with the verification code.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task ResetPasswordAsync()
    {
        if (!string.IsNullOrEmpty(NewPassword) && NewPassword != ConfirmNewPassword)
        {
            ErrorMessage = errorText.Describe(AuthErrorCodes.PasswordsDoNotMatch, []);
            return;
        }

        await RunAsync(async () =>
        {
            await passwordService.ResetPasswordAsync(Email, Code, NewPassword);
            NewPassword = string.Empty;
            ConfirmNewPassword = string.Empty;
            Step = ForgotPasswordStep.Done;
        });
    }

    /// <summary>
    /// Goes back to asking for the email address, to send a new code.
    /// </summary>
    [RelayCommand]
    public void StartAgain()
    {
        ErrorMessage = null;
        Code = string.Empty;
        Step = ForgotPasswordStep.Request;
    }

    /// <summary>
    /// Returns to the sign-in page.
    /// </summary>
    [RelayCommand]
    public void BackToSignIn()
    {
        navigationService.NavigateTo("/login");
    }

    private async Task RunAsync(Func<Task> action)
    {
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            await action();
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
}
