using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.UI.Services.Interfaces;

namespace Devoctomy.Blazwiz.UI.Services;

/// <inheritdoc/>
public class SessionEndedNoticeService : ISessionEndedNoticeService
{
    private SessionEndedEventArgs? pending;

    /// <inheritdoc/>
    public void Set(SessionEndedEventArgs args)
    {
        pending = args ?? throw new ArgumentNullException(nameof(args));
    }

    /// <inheritdoc/>
    public SessionEndedEventArgs? Take()
    {
        var result = pending;
        pending = null;
        return result;
    }
}
