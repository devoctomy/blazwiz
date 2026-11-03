using Devoctomy.Blazwiz.UI.Shell.Interfaces;

namespace Devoctomy.Blazwiz.UI.Shell;

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
