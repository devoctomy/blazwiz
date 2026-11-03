using Devoctomy.Blazwiz.Auth.Models;

namespace Devoctomy.Blazwiz.Auth.Sessions.Interfaces;

/// <summary>
/// Lets a module act when a session starts and ends: start syncing, fetch credentials, stop background work. Add one
/// with <see cref="Extensions.AuthServiceCollectionExtensions.AddSessionLifecycleHandler{THandler}"/>. Every method
/// does nothing unless implemented. A handler that throws does not stop the others.
/// </summary>
public interface ISessionLifecycleHandler
{
    /// <summary>
    /// Gets the handler's position: lower runs first.
    /// </summary>
    int Order => 0;

    /// <summary>
    /// Called by the sign-in page straight after a successful sign in (online or offline), before it moves on to the
    /// app. Use it for what must follow the user's click, such as browser permission prompts.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task OnSignedInAsync() => Task.CompletedTask;

    /// <summary>
    /// Called by the app shell once it knows who is signed in, before it is shown. Keep it short: the shell waits.
    /// </summary>
    /// <param name="session">The signed-in user.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task OnShellLoadingAsync(UserSession session) => Task.CompletedTask;

    /// <summary>
    /// Called by the app shell once it is shown, in the background: after a sign in and after a reload with a saved
    /// session. Start long-running work here.
    /// </summary>
    /// <param name="session">The signed-in user.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task OnSessionStartedAsync(UserSession session) => Task.CompletedTask;

    /// <summary>
    /// Called when the user signs out or the session ends, before the authentication service signs out.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task OnSigningOutAsync() => Task.CompletedTask;

    /// <summary>
    /// Called after the authentication service has signed out, before the sign-in page is shown.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task OnSignedOutAsync() => Task.CompletedTask;
}
