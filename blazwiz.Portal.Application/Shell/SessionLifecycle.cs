using blazwiz.Portal.Application.Shell.Interfaces;
using blazwiz.Portal.Models.Auth;

namespace blazwiz.Portal.Application.Shell;

/// <inheritdoc/>
public class SessionLifecycle(IEnumerable<ISessionLifecycleHandler> handlers)
    : ISessionLifecycle
{
    private readonly IReadOnlyList<ISessionLifecycleHandler> handlers =
        (handlers ?? throw new ArgumentNullException(nameof(handlers))).OrderBy(h => h.Order).ToList();

    /// <inheritdoc/>
    public Task SignedInAsync() => RunAsync(h => h.OnSignedInAsync());

    /// <inheritdoc/>
    public Task ShellLoadingAsync(UserSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return RunAsync(h => h.OnShellLoadingAsync(session));
    }

    /// <inheritdoc/>
    public Task SessionStartedAsync(UserSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return RunAsync(h => h.OnSessionStartedAsync(session));
    }

    /// <inheritdoc/>
    public Task SigningOutAsync() => RunAsync(h => h.OnSigningOutAsync());

    /// <inheritdoc/>
    public Task SignedOutAsync() => RunAsync(h => h.OnSignedOutAsync());

    private async Task RunAsync(Func<ISessionLifecycleHandler, Task> call)
    {
        foreach (var handler in handlers)
        {
            try
            {
                await call(handler);
            }
            catch (Exception)
            {
                // One module failing (e.g. offline) must not stop the others or the sign in, sign out or shell.
            }
        }
    }
}
