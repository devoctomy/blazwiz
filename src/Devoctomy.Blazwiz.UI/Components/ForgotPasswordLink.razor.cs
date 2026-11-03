using Devoctomy.Blazwiz.UI.Shell;
using Devoctomy.Blazwiz.UI.Shell.Interfaces;
using Microsoft.AspNetCore.Components;

namespace Devoctomy.Blazwiz.UI.Components;

/// <summary>
/// The sign-in page's link to resetting a forgotten password, shown while that page is turned on.
/// </summary>
public partial class ForgotPasswordLink : ComponentBase
{
    [Inject]
    private IPageRegistry Pages { get; set; } = default!;

    private bool IsEnabled => Pages.IsEnabled(BlazwizPages.ForgotPassword);
}
