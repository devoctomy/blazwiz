using Blazing.Mvvm.Components;
using blazwiz.Portal.Application.ViewModels;

namespace blazwiz.Portal.Components;

/// <summary>
/// The countdown under the user's name until the session's current token expires. Logic lives in
/// <see cref="SessionTimerViewModel"/>.
/// </summary>
public partial class SessionTimer : MvvmComponentBase<SessionTimerViewModel>
{
}
