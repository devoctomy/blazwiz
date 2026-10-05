using Blazing.Mvvm.ComponentModel;
using blazwiz.Core.Cryptography.Interfaces;
using blazwiz.Core.Offline.Interfaces;
using blazwiz.Core.Offline.Models;
//#if (IncludeNotes)
using blazwiz.Core.Sync.Interfaces;
//#endif
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
    private readonly IDerivedKeyStore keyStore;
    private readonly IOfflineModeService offlineModeService;
//#if (IncludeNotes)
    private readonly IConnectivityService connectivityService;
    private readonly IPersistentStorageService persistentStorageService;
//#endif
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
    private bool signInOffline;
//#if (IncludeNotes)
    private bool isAdvancedOptionsExpanded;
    private bool startWithSyncOff;
//#endif

    /// <summary>
    /// Initializes a new instance of the <see cref="LoginViewModel"/> class.
    /// </summary>
    /// <param name="authService">The authentication service.</param>
    /// <param name="navigationService">The navigation service.</param>
    /// <param name="themeService">The theme service.</param>
    /// <param name="localStorageService">The local storage service.</param>
    /// <param name="webAuthnService">The WebAuthn service for passkey authentication.</param>
    /// <param name="passwordCryptoService">The password crypto service, using the remember me key derived from WebAuthn.</param>
    /// <param name="keyStore">The keys derived from WebAuthn, forgotten when nobody is signed in.</param>
    /// <param name="offlineModeService">Offline mode, for signing in offline.</param>
    ///#if (IncludeNotes)
    /// <param name="connectivityService">The connectivity service, for turning sync off at sign in.</param>
    /// <param name="persistentStorageService">Asks the browser for persistent storage after sign in.</param>
    ///#endif
    /// <param name="appSettings">The application settings.</param>
    public LoginViewModel(
        IAuthenticationService authService,
        INavigationService navigationService,
        IThemeService themeService,
        ILocalStorageService localStorageService,
        IWebAuthnService webAuthnService,
        IPasswordCryptoService passwordCryptoService,
        IDerivedKeyStore keyStore,
        IOfflineModeService offlineModeService,
//#if (IncludeNotes)
        IConnectivityService connectivityService,
        IPersistentStorageService persistentStorageService,
//#endif
        AppSettings appSettings)
    {
        this.authService = authService ?? throw new ArgumentNullException(nameof(authService));
        this.navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        this.themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        this.localStorageService = localStorageService ?? throw new ArgumentNullException(nameof(localStorageService));
        this.webAuthnService = webAuthnService ?? throw new ArgumentNullException(nameof(webAuthnService));
        this.passwordCryptoService = passwordCryptoService ?? throw new ArgumentNullException(nameof(passwordCryptoService));
        this.keyStore = keyStore ?? throw new ArgumentNullException(nameof(keyStore));
        this.offlineModeService = offlineModeService ?? throw new ArgumentNullException(nameof(offlineModeService));
//#if (IncludeNotes)
        this.connectivityService = connectivityService ?? throw new ArgumentNullException(nameof(connectivityService));
        this.persistentStorageService = persistentStorageService ?? throw new ArgumentNullException(nameof(persistentStorageService));
//#endif
        this.appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
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
    /// Gets or sets a value indicating whether to sign in offline with a remembered account: the passkey proves who
    /// the user is on this device, and nothing calls the sign-in service or AWS.
    /// </summary>
    public bool SignInOffline
    {
        get => signInOffline;
        set => SetProperty(ref signInOffline, value);
    }

    /// <summary>
    /// Gets the Bootstrap icon for the theme toggle button.
    /// </summary>
    public string ThemeIcon => themeService.IsDark ? "bi-sun" : "bi-moon-stars";
//#if (IncludeNotes)

    /// <summary>
    /// Gets or sets a value indicating whether the advanced options section is expanded. It starts expanded whenever
    /// an advanced option differs from its default, so a setting that changes behaviour is never hidden.
    /// </summary>
    public bool IsAdvancedOptionsExpanded
    {
        get => isAdvancedOptionsExpanded;
        set => SetProperty(ref isAdvancedOptionsExpanded, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether to sign in with sync turned off, so the first sync makes no network calls.
    /// </summary>
    public bool StartWithSyncOff
    {
        get => startWithSyncOff;
        set => SetProperty(ref startWithSyncOff, value);
    }
//#endif

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

        // Nobody is signed in (signed out, or the session expired), so no keys should be held.
        keyStore.Clear();

        // Still in offline mode (e.g. its session expired): offer signing in offline again.
        SignInOffline = offlineModeService.IsOffline;
        await LoadSavedUsersAsync();
//#if (IncludeNotes)
        await LoadAdvancedOptionsAsync();
//#endif
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
            // A typed password can only be checked by the sign-in service, so this is always an online sign in.
            offlineModeService.Exit();
            var result = await authService.LoginAsync(Username, Password);
            if (result.Success)
            {
//#if (IncludeNotes)
                await ApplySignInOptionsAsync();
//#endif
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
//#if (IncludeNotes)
                await ApplySignInOptionsAsync();
//#endif
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

//#if (IncludeNotes)
    /// <summary>
    /// Expands or collapses the advanced options section.
    /// </summary>
    [RelayCommand]
    public void ToggleAdvancedOptions()
    {
        IsAdvancedOptionsExpanded = !IsAdvancedOptionsExpanded;
    }

//#endif
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
            if (result.Success && result.PrfEnabled && result.KeysDerived)
            {
                var webAuthnCredential = new WebAuthnStoredCredential
                {
                    CredentialID = result.CredentialID ?? string.Empty,
                    UserID = result.UserID ?? string.Empty,
                    CreatedAt = DateTime.UtcNow,
                    EncryptedPassword = await passwordCryptoService.EncryptAsync(Password),
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

            if (authResult.Success && authResult.KeysDerived)
            {
                // Decrypting the remembered password proves the passkey (and the user's fingerprint or PIN) on this
                // device, which is all an offline sign in has to go on.
                var decryptedPassword = await passwordCryptoService.DecryptAsync(user.WebAuthnCredential.EncryptedPassword!);
                if (SignInOffline)
                {
                    await SignInOfflineAsync(user.Email, OfflineEntryReason.Chosen);
                    return;
                }

                offlineModeService.Exit();
                var loginResult = await authService.LoginAsync(user.Email, decryptedPassword);

                if (loginResult.Success)
                {
//#if (IncludeNotes)
                    await ApplySignInOptionsAsync();
//#endif
                    navigationService.NavigateTo("/");
                }
                else if (loginResult.ServiceUnreachable)
                {
                    // Only when the sign-in service could not be reached: never when it refused the sign in.
                    await SignInOfflineAsync(user.Email, OfflineEntryReason.SignInUnreachable);
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

    private async Task SignInOfflineAsync(string email, OfflineEntryReason reason)
    {
        await authService.SignInOfflineAsync(email, reason);
//#if (IncludeNotes)
        await ApplySignInOptionsAsync();
//#endif
        navigationService.NavigateTo("/");
    }

    private void FallbackToManualLogin(SavedUser user)
    {
        // The keys came from a biometric sign in that failed, so they must not outlive it.
        keyStore.Clear();
        Username = user.Email;
        Password = string.Empty;
        IsUsernameReadonly = false;
        ShowUsersList = false;
        ShowRememberMe = false;
    }

//#if (IncludeNotes)
    private async Task LoadAdvancedOptionsAsync()
    {
        await connectivityService.InitializeAsync();
        StartWithSyncOff = !connectivityService.IsSyncEnabled;
        IsAdvancedOptionsExpanded = StartWithSyncOff;
    }

    private async Task ApplySignInOptionsAsync()
    {
        try
        {
            // Before the first sync after sign in starts, so sync turned off makes no network calls at all.
            if (StartWithSyncOff == connectivityService.IsSyncEnabled)
            {
                await connectivityService.SetSyncEnabledAsync(!StartWithSyncOff);
            }

            // Straight after the sign-in click, so Firefox's permission prompt appears at a sensible moment.
            await persistentStorageService.RequestAfterSignInAsync();
        }
        catch (Exception)
        {
            // Neither is worth failing the sign in over.
        }
    }

//#endif
    private void ClearNewPassword()
    {
        NewPassword = string.Empty;
        ConfirmNewPassword = string.Empty;
        IsNewPasswordRequired = false;
    }
}
