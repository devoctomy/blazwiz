using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Auth.Sessions.Interfaces;
using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;
using MyApp.Portal.Application.Services.Interfaces;

namespace MyApp.Portal.Application.Services;

/// <summary>
/// Labels a notebook on this device with its name whenever it is unlocked, wherever that happens (its page, Settings or
/// the passkey), so it is shown by name everywhere on this device from then on, even while locked.
/// </summary>
/// <param name="manager">Says when a notebook is unlocked.</param>
/// <param name="notebookService">Reads the notebook's name, and labels it.</param>
public sealed class NotebookLabelHandler(IE2eProtectedProfileManager manager, INotebookService notebookService)
    : ISessionLifecycleHandler, IDisposable
{
    private readonly IE2eProtectedProfileManager manager = manager ?? throw new ArgumentNullException(nameof(manager));
    private readonly INotebookService notebookService = notebookService ?? throw new ArgumentNullException(nameof(notebookService));
    private bool listening;

    /// <inheritdoc/>
    public Task OnSessionStartedAsync(UserSession session)
    {
        if (!listening)
        {
            listening = true;
            manager.LockStateChanged += OnLockStateChanged;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task OnSigningOutAsync()
    {
        Stop();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Stop();
    }

    private void Stop()
    {
        if (listening)
        {
            listening = false;
            manager.LockStateChanged -= OnLockStateChanged;
        }
    }

    private async void OnLockStateChanged(object? sender, E2eKeyScope scope)
    {
        if (!manager.IsUnlocked(scope))
        {
            return;
        }

        try
        {
            // Finding an unlocked notebook reads its name, and labels it.
            await notebookService.FindAsync(scope.Id);
        }
        catch (Exception)
        {
            // Labelled again the next time it is unlocked or listed.
        }
    }
}
