using Devoctomy.Blazwiz.UI.ViewModels;
using Microsoft.AspNetCore.Components;

namespace Devoctomy.Blazwiz.UI.Components;

/// <summary>
/// Shows the secret to set up an authenticator app with, three ways: a link that opens the app on this device (a
/// phone cannot scan its own screen), a QR code to scan from another screen, and the secret as text to copy or type.
/// Used in a form that then asks for the app's first code:
/// <c>&lt;AuthenticatorSetupPanel Setup="ViewModel.Setup" IdPrefix="profile-mfa" /&gt;</c>. It shows the view model
/// it is given, which the page's view model owns, and is shown again whenever the page is: the page's view model says
/// when the set-up changes. Logic lives in <see cref="AuthenticatorSetupViewModel"/>.
/// </summary>
public partial class AuthenticatorSetupPanel : ComponentBase
{
    /// <summary>
    /// Gets or sets the set-up's view model, owned by the view model of the page showing it.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public AuthenticatorSetupViewModel Setup { get; set; } = default!;

    /// <summary>
    /// Gets or sets what every element id here starts with, e.g. <c>profile-mfa</c> for <c>profile-mfa-secret</c>.
    /// </summary>
    [Parameter]
    public string IdPrefix { get; set; } = "mfa";
}
