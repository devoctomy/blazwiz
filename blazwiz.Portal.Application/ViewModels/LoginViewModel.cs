using Blazing.Mvvm.ComponentModel;
using blazwiz.Portal.Application.Helpers;
using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Models.Auth;
using blazwiz.Portal.Models.Settings;
using CommunityToolkit.Mvvm.Input;

namespace blazwiz.Portal.Application.ViewModels;

/// <summary>
/// ViewModel for the login page, managing standard login, saved users and WebAuthn (biometric) login.
/// </summary>
public partial class LoginViewModel : ViewModelBase
{
    /// <summary>
    /// The local storage key holding the remembered users.
    /// </summary>
    public const string SavedUsersStorageKey = "blazwiz_saved_users";

    private readonly IAuthenticationService authService;
    private readonly INavigationService navigationService;
    private readonly IThemeService themeService;
    private readonly ILocalStorageService localStorageService;
    private readonly IWebAuthnService webAuthnService;
    private readonly IPasswordCryptoService passwordCryptoService;
    private readonly AppSettings appSettings;

    private string username = string.Empty;
    private string password = string.Empty;
    private string newPassword = string.Empty;
    private string confirmNewPassword = string.Empty;
    private bool isNewPasswordRequired;
    private bool isBusy;
    private string? errorMessage;
    private SavedUser? loadingUser;
    private List<SavedUser> savedUsers = [];
    private bool showUsersList;
    private bool showRememberMe = true;
    private bool rememberMe;
    private bool rememberMeWhenNewPasswordStarted;
    private bool isUsernameReadonly;
    private bool showWebAuthnPrompt;
    private bool isWebAuthnProcessing;
    private string? webAuthnErrorMessage;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoginViewModel"/> class.
    /// </summary>
    /// <param name="authService">The authentication service.</param>
    /// <param name="navigationService">The navigation service.</param>
    /// <param name="themeService">The theme service.</param>
    /// <param name="localStorageService">The local storage service.</param>
    /// <param name="webAuthnService">The WebAuthn service for passkey authentication.</param>
    /// <param name="passwordCryptoService">The password crypto service for PRF-based encryption.</param>
    /// <param name="appSettings">The application settings.</param>
    public LoginViewModel(
        IAuthenticationService authService,
        INavigationService navigationService,
        IThemeService themeService,
        ILocalStorageService localStorageService,
        IWebAuthnService webAuthnService,
        IPasswordCryptoService passwordCryptoService,
        AppSettings appSettings)
    {
        this.authService = ValidationHelpers.VerifiedAssignment(authService);
        this.navigationService = ValidationHelpers.VerifiedAssignment(navigationService);
        this.themeService = ValidationHelpers.VerifiedAssignment(themeService);
        this.localStorageService = ValidationHelpers.VerifiedAssignment(localStorageService);
        this.webAuthnService = ValidationHelpers.VerifiedAssignment(webAuthnService);
        this.passwordCryptoService = ValidationHelpers.VerifiedAssignment(passwordCryptoService);
        this.appSettings = ValidationHelpers.VerifiedAssignment(appSettings);
    }

    /// <summary>
    /// Gets or sets the username entered by the user.
    /// </summary>
    public string Username
    {
        get => username;
        set => SetProperty(ref username, value);
    }

    /// <summary>
    /// Gets or sets the password entered by the user.
    /// </summary>
    public string Password
    {
        get => password;
        set => SetProperty(ref password, value);
    }

    /// <summary>
    /// Gets or sets the new password, when the user must choose one.
    /// </summary>
    public string NewPassword
    {
        get => newPassword;
        set => SetProperty(ref newPassword, value);
    }

    /// <summary>
    /// Gets or sets the new password again, to catch typos.
    /// </summary>
    public string ConfirmNewPassword
    {
        get => confirmNewPassword;
        set => SetProperty(ref confirmNewPassword, value);
    }

    /// <summary>
    /// Gets a value indicating whether the user must choose a new password to finish signing in.
    /// </summary>
    public bool IsNewPasswordRequired
    {
        get => isNewPasswordRequired;
        private set => SetProperty(ref isNewPasswordRequired, value);
    }

    /// <summary>
    /// Gets a value indicating whether the placeholder login (any credentials) is in use.
    /// </summary>
    public bool IsPlaceholderLogin => authService.IsPlaceholder;

    /// <summary>
    /// Gets a value indicating whether a login is in progress.
    /// </summary>
    public bool IsBusy
    {
        get => isBusy;
        private set => SetProperty(ref isBusy, value);
    }

    /// <summary>
    /// Gets the error message from the last login attempt, if any.
    /// </summary>
    public string? ErrorMessage
    {
        get => errorMessage;
        private set => SetProperty(ref errorMessage, value);
    }

    /// <summary>Gets or sets the user currently being authenticated, or <see langword="null"/> when no user-specific load is in progress.</summary>
    public SavedUser? LoadingUser { get => loadingUser; set => SetProperty(ref loadingUser, value); }

    /// <summary>Gets the list of saved users.</summary>
    public List<SavedUser> SavedUsers { get => savedUsers; private set => SetProperty(ref savedUsers, value); }

    /// <summary>Gets or sets a value indicating whether the saved users list is shown.</summary>
    public bool ShowUsersList { get => showUsersList; set => SetProperty(ref showUsersList, value); }

    /// <summary>Gets or sets a value indicating whether the remember me checkbox is shown.</summary>
    public bool ShowRememberMe { get => showRememberMe; set => SetProperty(ref showRememberMe, value); }

    /// <summary>Gets or sets a value indicating whether the user wants to be remembered.</summary>
    public bool RememberMe { get => rememberMe; set => SetProperty(ref rememberMe, value); }

    /// <summary>Gets or sets a value indicating whether the username field is readonly.</summary>
    public bool IsUsernameReadonly { get => isUsernameReadonly; set => SetProperty(ref isUsernameReadonly, value); }

    /// <summary>Gets or sets a value indicating whether the WebAuthn setup prompt is shown.</summary>
    public bool ShowWebAuthnPrompt { get => showWebAuthnPrompt; set => SetProperty(ref showWebAuthnPrompt, value); }

    /// <summary>Gets or sets a value indicating whether WebAuthn processing is in progress.</summary>
    public bool IsWebAuthnProcessing { get => isWebAuthnProcessing; set => SetProperty(ref isWebAuthnProcessing, value); }

    /// <summary>Gets or sets the WebAuthn error message.</summary>
    public string? WebAuthnErrorMessage { get => webAuthnErrorMessage; set => SetProperty(ref webAuthnErrorMessage, value); }

    /// <summary>
    /// Gets the Bootstrap icon for the theme toggle button.
    /// </summary>
    public string ThemeIcon => themeService.IsDark ? "bi-sun" : "bi-moon-stars";

    /// <summary>
    /// Gets the application settings.
    /// </summary>
    public AppSettings AppSettings => appSettings;

    /// <inheritdoc/>
    public override async Task OnInitializedAsync()
    {
        await themeService.InitializeAsync();
        OnPropertyChanged(nameof(ThemeIcon));

        if (await authService.GetCurrentUserAsync() != null)
        {
            navigationService.NavigateTo("/");
            return;
        }

        await LoadSavedUsersAsync();
    }

    /// <summary>
    /// Attempts to log in with the entered credentials.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task LoginAsync()
    {
        if (IsBusy)
        {
            return;
        }

        ErrorMessage = null;
        IsBusy = true;
        try
        {
            var result = await authService.LoginAsync(Username, Password);
            if (result.Success)
            {
                var rememberMeEnabled = RememberMe && !string.IsNullOrEmpty(Username);
                await DoLogin(rememberMeEnabled);
            }
            else if (result.NewPasswordRequired)
            {
                Password = string.Empty;
                rememberMeWhenNewPasswordStarted = RememberMe;
                IsNewPasswordRequired = true;
            }
            else
            {
                ErrorMessage = result.Error ?? "Login failed.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Login failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Sets the new password and finishes signing in.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task CompleteNewPasswordAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (NewPassword != ConfirmNewPassword)
        {
            ErrorMessage = "The passwords do not match.";
            return;
        }

        ErrorMessage = null;
        IsBusy = true;
        try
        {
            var result = await authService.CompleteNewPasswordAsync(NewPassword);
            if (result.Success)
            {
                // The new password is the one to remember, not the temporary one used to sign in.
                Password = NewPassword;
                ClearNewPassword();
                var rememberMeEnabled = rememberMeWhenNewPasswordStarted && !string.IsNullOrEmpty(Username);
                await DoLogin(rememberMeEnabled);
            }
            else
            {
                ErrorMessage = result.Error ?? "Could not set the new password.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not set the new password: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Abandons choosing a new password and goes back to the sign-in form.
    /// </summary>
    [RelayCommand]
    public void CancelNewPassword()
    {
        ClearNewPassword();
        ErrorMessage = null;
    }

    /// <summary>
    /// Toggles between light and dark themes.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task ToggleThemeAsync()
    {
        await themeService.ToggleThemeAsync();
        OnPropertyChanged(nameof(ThemeIcon));
    }

    /// <summary>Selects a saved user for login.</summary>
    /// <param name="user">The saved user to select.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task SelectUserAsync(SavedUser user)
    {
        if (user.WebAuthnCredential != null && !string.IsNullOrEmpty(user.WebAuthnCredential.EncryptedPassword))
        {
            await AttemptWebAuthnLoginAsync(user);
        }
        else
        {
            Username = user.Email;
            IsUsernameReadonly = false;
            ShowUsersList = false;
            ShowRememberMe = false;
        }
    }

    /// <summary>Removes a saved user from the list.</summary>
    /// <param name="user">The saved user to remove.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task RemoveUserAsync(SavedUser user)
    {
        try
        {
            savedUsers.Remove(user);
            SavedUsers = [.. savedUsers];

            var usersData = new SavedUsersData { Users = savedUsers };
            await localStorageService.SetObjectAsync(SavedUsersStorageKey, usersData);

            if (savedUsers.Count == 0)
            {
                ShowUsersList = false;
                ShowRememberMe = true;
            }
        }
        catch
        {
            // Continue silently
        }
    }

    /// <summary>Shows the login form instead of the saved users list.</summary>
    [RelayCommand]
    public void ShowLoginForm()
    {
        ShowUsersList = false;
        ShowRememberMe = true;
        IsUsernameReadonly = false;
        Username = string.Empty;
        Password = string.Empty;
        RememberMe = false;
        ErrorMessage = null;
    }

    /// <summary>Shows the saved users list.</summary>
    [RelayCommand]
    public void ShowUsersListCommand()
    {
        ShowUsersList = true;
        ShowRememberMe = false;
        Username = string.Empty;
        Password = string.Empty;
        RememberMe = false;
        ErrorMessage = null;
    }

    /// <summary>Sets up WebAuthn biometric login after successful authentication.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task HandleWebAuthnSetupAsync()
    {
        IsWebAuthnProcessing = true;
        WebAuthnErrorMessage = null;

        try
        {
            var result = await webAuthnService.RegisterAsync(Username, null);
            var prfSupported = result.Success && result.PrfEnabled && !string.IsNullOrEmpty(result.PrfOutput);

            if (prfSupported)
            {
                var webAuthnCredential = new WebAuthnStoredCredential
                {
                    CredentialID = result.CredentialID ?? string.Empty,
                    UserID = result.UserID ?? string.Empty,
                    CreatedAt = DateTime.UtcNow,
                    EncryptedPassword = await passwordCryptoService.EncryptWithPrfAsync(Password, result.PrfOutput!),
                };

                await SaveUserAsync(Username, webAuthnCredential);
                CompleteLogin();
                return;
            }

            WebAuthnErrorMessage = "Your authenticator doesn't support the required security features for biometric login. Please continue with regular login.";
        }
        catch (Exception ex)
        {
            WebAuthnErrorMessage = $"Error setting up biometric login: {ex.Message}";
        }
        finally
        {
            IsWebAuthnProcessing = false;
        }
    }

    /// <summary>Skips WebAuthn setup and navigates to the app.</summary>
    [RelayCommand]
    public void SkipWebAuthnSetup()
    {
        CompleteLogin();
    }

    /// <summary>Dismisses the WebAuthn error and navigates to the app.</summary>
    [RelayCommand]
    public void DismissWebAuthnError()
    {
        WebAuthnErrorMessage = null;
        CompleteLogin();
    }

    private async Task DoLogin(bool rememberMe)
    {
        if (rememberMe)
        {
            await SaveUserAsync(Username);

            if (appSettings.EnablePortalLoginAuthn)
            {
                ShowWebAuthnPrompt = true;
            }
            else
            {
                CompleteLogin();
            }
        }
        else
        {
            CompleteLogin();
        }
    }

    private void CompleteLogin()
    {
        ShowWebAuthnPrompt = false;
        Password = string.Empty;
        navigationService.NavigateTo("/");
    }

    private async Task LoadSavedUsersAsync()
    {
        try
        {
            var usersData = await localStorageService.GetObjectAsync<SavedUsersData>(SavedUsersStorageKey);
            if (usersData?.Users != null && usersData.Users.Count > 0)
            {
                SavedUsers = usersData.Users;
                ShowUsersList = true;
                ShowRememberMe = false;
            }
        }
        catch
        {
            SavedUsers = [];
            ShowUsersList = false;
            ShowRememberMe = true;
        }
    }

    private async Task SaveUserAsync(string email, WebAuthnStoredCredential? webAuthnCredential = null)
    {
        try
        {
            var existingUser = savedUsers.FirstOrDefault(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase));

            if (existingUser != null)
            {
                if (webAuthnCredential != null)
                {
                    existingUser.WebAuthnCredential = webAuthnCredential;
                }
            }
            else
            {
                savedUsers.Add(new SavedUser { Email = email, WebAuthnCredential = webAuthnCredential });
            }

            var usersData = new SavedUsersData { Users = savedUsers };
            await localStorageService.SetObjectAsync(SavedUsersStorageKey, usersData);
        }
        catch
        {
            // Continue silently
        }
    }

    private async Task AttemptWebAuthnLoginAsync(SavedUser user)
    {
        IsBusy = true;
        LoadingUser = user;
        ErrorMessage = null;

        try
        {
            var authResult = await webAuthnService.AuthenticateAsync(user.WebAuthnCredential!.CredentialID);

            if (authResult.Success && !string.IsNullOrEmpty(authResult.PrfOutput))
            {
                var decryptedPassword = await passwordCryptoService.DecryptWithPrfAsync(
                    user.WebAuthnCredential.EncryptedPassword!,
                    authResult.PrfOutput);

                var loginResult = await authService.LoginAsync(user.Email, decryptedPassword);

                if (loginResult.Success)
                {
                    navigationService.NavigateTo("/");
                }
                else
                {
                    ErrorMessage = "Biometric login failed. Please enter your password manually.";
                    FallbackToManualLogin(user);
                }
            }
            else
            {
                ErrorMessage = authResult.Error ?? "Biometric authentication failed. Please enter your password manually.";
                FallbackToManualLogin(user);
            }
        }
        catch
        {
            ErrorMessage = "Biometric login error. Please enter your password manually.";
            FallbackToManualLogin(user);
        }
        finally
        {
            IsBusy = false;
            LoadingUser = null;
        }
    }

    private void FallbackToManualLogin(SavedUser user)
    {
        Username = user.Email;
        Password = string.Empty;
        IsUsernameReadonly = false;
        ShowUsersList = false;
        ShowRememberMe = false;
    }

    private void ClearNewPassword()
    {
        NewPassword = string.Empty;
        ConfirmNewPassword = string.Empty;
        IsNewPasswordRequired = false;
    }
}
