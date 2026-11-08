using Blazing.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Devoctomy.Blazwiz.Auth.Exceptions;
using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Core.Exceptions;
using Devoctomy.Blazwiz.UI.Text;
using Devoctomy.Blazwiz.UI.Text.Interfaces;
using Microsoft.Extensions.Localization;

namespace Devoctomy.Blazwiz.UI.ViewModels;

/// <summary>
/// ViewModel for changing the signed-in user's password on the Profile page. A password remembered for this user on
/// this device (for passkey and offline sign in) is encrypted again, or forgotten when that is not possible.
/// </summary>
public partial class ChangePasswordViewModel : ViewModelBase
{
    private readonly IPasswordService passwordService;
    private readonly IAuthenticationService authService;
    private readonly ILocalStorageService localStorageService;
    private readonly IPasswordCryptoService passwordCryptoService;
    private readonly IStringLocalizer<UiText> text;
    private readonly IErrorText errorText;
    private string currentPassword = string.Empty;
    private string newPassword = string.Empty;
    private string confirmNewPassword = string.Empty;
    private bool isBusy;
    private string? errorMessage;
    private string? successMessage;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChangePasswordViewModel"/> class.
    /// </summary>
    /// <param name="passwordService">Changes the password.</param>
    /// <param name="authService">The signed-in user.</param>
    /// <param name="localStorageService">Where remembered users are kept.</param>
    /// <param name="passwordCryptoService">Encrypts a remembered password.</param>
    /// <param name="text">The UI text.</param>
    /// <param name="errorText">Describes errors.</param>
    public ChangePasswordViewModel(
        IPasswordService passwordService,
        IAuthenticationService authService,
        ILocalStorageService localStorageService,
        IPasswordCryptoService passwordCryptoService,
        IStringLocalizer<UiText> text,
        IErrorText errorText)
    {
        this.passwordService = passwordService ?? throw new ArgumentNullException(nameof(passwordService));
        this.authService = authService ?? throw new ArgumentNullException(nameof(authService));
        this.localStorageService = localStorageService ?? throw new ArgumentNullException(nameof(localStorageService));
        this.passwordCryptoService = passwordCryptoService ?? throw new ArgumentNullException(nameof(passwordCryptoService));
        this.text = text ?? throw new ArgumentNullException(nameof(text));
        this.errorText = errorText ?? throw new ArgumentNullException(nameof(errorText));
    }

    /// <summary>
    /// Gets or sets the current password.
    /// </summary>
    public string CurrentPassword { get => currentPassword; set => SetProperty(ref currentPassword, value); }

    /// <summary>
    /// Gets or sets the new password.
    /// </summary>
    public string NewPassword { get => newPassword; set => SetProperty(ref newPassword, value); }

    /// <summary>
    /// Gets or sets the new password again, to catch typing mistakes.
    /// </summary>
    public string ConfirmNewPassword { get => confirmNewPassword; set => SetProperty(ref confirmNewPassword, value); }

    /// <summary>
    /// Gets a value indicating whether the password is being changed.
    /// </summary>
    public bool IsBusy { get => isBusy; private set => SetProperty(ref isBusy, value); }

    /// <summary>
    /// Gets why the password was not changed, if it was not.
    /// </summary>
    public string? ErrorMessage { get => errorMessage; private set => SetProperty(ref errorMessage, value); }

    /// <summary>
    /// Gets the message saying the password was changed.
    /// </summary>
    public string? SuccessMessage { get => successMessage; private set => SetProperty(ref successMessage, value); }

    /// <summary>
    /// Changes the password.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task ChangePasswordAsync()
    {
        ErrorMessage = null;
        SuccessMessage = null;
        if (string.IsNullOrEmpty(CurrentPassword) || string.IsNullOrEmpty(NewPassword) || string.IsNullOrEmpty(ConfirmNewPassword))
        {
            ErrorMessage = errorText.Describe(AuthErrorCodes.PasswordsRequired, []);
            return;
        }

        if (NewPassword != ConfirmNewPassword)
        {
            ErrorMessage = errorText.Describe(AuthErrorCodes.PasswordsDoNotMatch, []);
            return;
        }

        IsBusy = true;
        try
        {
            await passwordService.ChangePasswordAsync(CurrentPassword, NewPassword);
            var kept = await UpdateRememberedPasswordAsync(NewPassword);
            SuccessMessage = text[kept ? "Profile.ChangePassword.Done" : "Profile.ChangePassword.DonePasskeyForgotten"];
            CurrentPassword = string.Empty;
            NewPassword = string.Empty;
            ConfirmNewPassword = string.Empty;
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

    // The remembered (encrypted) password would no longer sign in. Returns false when it had to be forgotten.
    private async Task<bool> UpdateRememberedPasswordAsync(string password)
    {
        var session = await authService.GetCurrentUserAsync();
        var users = await localStorageService.GetObjectAsync<SavedUsersData>(LoginViewModel.SavedUsersStorageKey);
        var user = session == null ? null : users?.Users.FirstOrDefault(u => u.IsFor(session.Username, session.Email));
        var credential = user?.WebAuthnCredential;
        if (users == null || credential == null || string.IsNullOrEmpty(credential.EncryptedPassword))
        {
            return true;
        }

        var kept = true;
        try
        {
            credential.EncryptedPassword = await passwordCryptoService.EncryptAsync(password, user!.Email);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // No passkey key in this session: forget the old password rather than keep one that no longer works.
            credential.EncryptedPassword = null;
            kept = false;
        }

        await localStorageService.SetObjectAsync(LoginViewModel.SavedUsersStorageKey, users);
        return kept;
    }
}
