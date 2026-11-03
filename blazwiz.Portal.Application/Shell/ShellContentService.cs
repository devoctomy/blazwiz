using blazwiz.Portal.Application.Shell.Interfaces;

namespace blazwiz.Portal.Application.Shell;

/// <inheritdoc/>
public class ShellContentService : IShellContentService
{
    /// <inheritdoc/>
    public event EventHandler? ReloadRequested;

    /// <inheritdoc/>
    public void RequestReload()
    {
        ReloadRequested?.Invoke(this, EventArgs.Empty);
    }
}
