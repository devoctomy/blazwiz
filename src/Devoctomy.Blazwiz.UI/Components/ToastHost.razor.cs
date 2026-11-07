using Blazing.Mvvm.Components;
using Devoctomy.Blazwiz.UI.ViewModels;

namespace Devoctomy.Blazwiz.UI.Components;

/// <summary>
/// Shows the toasts of <see cref="Services.Interfaces.IToastService"/> over the page. Added to the shell's overlays by
/// <c>AddBlazwizUi</c>. Logic lives in <see cref="ToastHostViewModel"/>.
/// </summary>
public partial class ToastHost : MvvmComponentBase<ToastHostViewModel>
{
}
