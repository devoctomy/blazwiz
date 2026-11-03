using Devoctomy.Blazwiz.Auth.Models;

namespace Devoctomy.Blazwiz.Auth.Sessions.Interfaces;

/// <summary>
/// Calls every <see cref="ISessionLifecycleHandler"/> in order. Used by the sign-in page and the app shell.
/// </summary>
public interface ISessionLifecycle
{
    /// <summary>
    /// Calls <see cref="ISessionLifecycleHandler.OnSignedInAsync"/> on every handler.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task SignedInAsync();

    /// <summary>
    /// Calls <see cref="ISessionLifecycleHandler.OnShellLoadingAsync"/> on every handler.
    /// </summary>
    /// <param name="session">The signed-in user.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task ShellLoadingAsync(UserSession session);

    /// <summary>
    /// Calls <see cref="ISessionLifecycleHandler.OnSessionStartedAsync"/> on every handler.
    /// </summary>
    /// <param name="session">The signed-in user.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task SessionStartedAsync(UserSession session);

    /// <summary>
    /// Calls <see cref="ISessionLifecycleHandler.OnSigningOutAsync"/> on every handler.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task SigningOutAsync();

    /// <summary>
    /// Calls <see cref="ISessionLifecycleHandler.OnSignedOutAsync"/> on every handler.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task SignedOutAsync();
}
