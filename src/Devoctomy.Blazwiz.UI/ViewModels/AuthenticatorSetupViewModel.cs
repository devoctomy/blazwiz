using Blazing.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.UI.Models;
using Devoctomy.Blazwiz.UI.Services.Interfaces;

namespace Devoctomy.Blazwiz.UI.ViewModels;

/// <summary>
/// ViewModel for setting up an authenticator app with a secret: as a QR code to scan from another screen, a link that
/// opens the authenticator app on this device, and the secret as text to copy or type. Owned by the view model of the
/// page showing it (the Profile page, or the sign-in page when the app requires two-step verification), which says
/// when it changes.
/// </summary>
public partial class AuthenticatorSetupViewModel : ViewModelBase
{
    /// <summary>
    /// How long a copied secret stays on the clipboard.
    /// </summary>
    public static readonly TimeSpan ClipboardLifetime = TimeSpan.FromMinutes(2);

    private readonly IQrCodeService qrCodeService;
    private readonly IClipboardService clipboardService;
    private readonly AppSettings appSettings;
    private string secret = string.Empty;
    private string appLink = string.Empty;
    private string qrCodeImageUrl = string.Empty;
    private bool isCopied;
    private bool copyFailed;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthenticatorSetupViewModel"/> class.
    /// </summary>
    /// <param name="qrCodeService">Draws the QR code.</param>
    /// <param name="clipboardService">Copies the secret.</param>
    /// <param name="appSettings">The app's title, which the authenticator app lists the account under.</param>
    public AuthenticatorSetupViewModel(IQrCodeService qrCodeService, IClipboardService clipboardService, AppSettings appSettings)
    {
        this.qrCodeService = qrCodeService ?? throw new ArgumentNullException(nameof(qrCodeService));
        this.clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
        this.appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
    }

    /// <summary>
    /// Gets a value indicating whether there is a secret to show.
    /// </summary>
    public bool IsShown => secret.Length > 0;

    /// <summary>
    /// Gets the secret, as the authenticator app takes it (base32).
    /// </summary>
    public string Secret
    {
        get => secret;
        private set
        {
            if (SetProperty(ref secret, value))
            {
                OnPropertyChanged(nameof(IsShown));
                OnPropertyChanged(nameof(GroupedSecret));
            }
        }
    }

    /// <summary>
    /// Gets the secret in groups of four, easier to read and type.
    /// </summary>
    public string GroupedSecret => string.Join(' ', secret.Chunk(4).Select(group => new string(group)));

    /// <summary>
    /// Gets the <c>otpauth://</c> link that opens the authenticator app on this device with the account filled in.
    /// </summary>
    public string AppLink
    {
        get => appLink;
        private set => SetProperty(ref appLink, value);
    }

    /// <summary>
    /// Gets the QR code holding <see cref="AppLink"/>, as an image URL.
    /// </summary>
    public string QrCodeImageUrl
    {
        get => qrCodeImageUrl;
        private set => SetProperty(ref qrCodeImageUrl, value);
    }

    /// <summary>
    /// Gets a value indicating whether the secret has just been copied.
    /// </summary>
    public bool IsCopied
    {
        get => isCopied;
        private set => SetProperty(ref isCopied, value);
    }

    /// <summary>
    /// Gets a value indicating whether copying the secret failed, so it has to be typed.
    /// </summary>
    public bool CopyFailed
    {
        get => copyFailed;
        private set => SetProperty(ref copyFailed, value);
    }

    /// <summary>
    /// Shows a secret for an account.
    /// </summary>
    /// <param name="newSecret">The secret.</param>
    /// <param name="account">The account the authenticator app lists it under, usually the email address.</param>
    public void Show(string newSecret, string account)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newSecret);
        ArgumentNullException.ThrowIfNull(account);

        var issuer = Uri.EscapeDataString(appSettings.ApplicationTitle);
        var label = Uri.EscapeDataString(account.Trim());
        IsCopied = false;
        CopyFailed = false;
        Secret = newSecret;
        AppLink = $"otpauth://totp/{issuer}:{label}?secret={Uri.EscapeDataString(newSecret)}&issuer={issuer}";
        QrCodeImageUrl = qrCodeService.CreateImageUrl(AppLink);
    }

    /// <summary>
    /// Forgets the secret, e.g. once set up or abandoned.
    /// </summary>
    public void Clear()
    {
        IsCopied = false;
        CopyFailed = false;
        Secret = string.Empty;
        AppLink = string.Empty;
        QrCodeImageUrl = string.Empty;
    }

    /// <summary>
    /// Copies the secret to the clipboard, to paste into an authenticator app on this device. It is cleared from the
    /// clipboard after <see cref="ClipboardLifetime"/>.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task CopySecretAsync()
    {
        if (!IsShown)
        {
            return;
        }

        try
        {
            await clipboardService.CopyAsync(secret, ClipboardLifetime);
            IsCopied = true;
            CopyFailed = false;
        }
        catch (Exception)
        {
            // The browser refused (no permission, or not a secure page): the secret is still there to type.
            IsCopied = false;
            CopyFailed = true;
        }
    }
}
