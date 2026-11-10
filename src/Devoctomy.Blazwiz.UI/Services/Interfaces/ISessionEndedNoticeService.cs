using Devoctomy.Blazwiz.Auth.Models;

namespace Devoctomy.Blazwiz.UI.Services.Interfaces;

/// <summary>
/// Carries why the last session ended from the shell, which saw it end, to the sign-in page, which says so once.
/// </summary>
public interface ISessionEndedNoticeService
{
    /// <summary>
    /// Remembers why the session ended, replacing anything not yet shown.
    /// </summary>
    /// <param name="args">Why, and which device ended it.</param>
    void Set(SessionEndedEventArgs args);

    /// <summary>
    /// Gets why the last session ended and forgets it, so it is shown once.
    /// </summary>
    /// <returns>Why, or null if nothing is waiting.</returns>
    SessionEndedEventArgs? Take();
}
