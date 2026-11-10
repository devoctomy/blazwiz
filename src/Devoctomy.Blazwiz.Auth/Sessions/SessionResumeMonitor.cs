using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.Auth.Sessions.Interfaces;
using Devoctomy.Blazwiz.Browser.Services.Interfaces;

namespace Devoctomy.Blazwiz.Auth.Sessions;

/// <inheritdoc/>
public sealed class SessionResumeMonitor(IPageResumeService pageResumeService, IAuthenticationService authService)
    : ISessionResumeMonitor, IDisposable
{
    private readonly IPageResumeService pageResumeService = pageResumeService ?? throw new ArgumentNullException(nameof(pageResumeService));
    private readonly IAuthenticationService authService = authService ?? throw new ArgumentNullException(nameof(authService));
    private bool isStarted;
    private bool isChecking;

    /// <inheritdoc/>
    public event EventHandler? SessionResumed;

    /// <inheritdoc/>
    public async Task StartAsync()
    {
        if (isStarted)
        {
            return;
        }

        isStarted = true;
        pageResumeService.Resumed += OnResumed;
        await pageResumeService.StartAsync();
    }

    /// <inheritdoc/>
    public async Task StopAsync()
    {
        if (!isStarted)
        {
            return;
        }

        isStarted = false;
        pageResumeService.Resumed -= OnResumed;
        await pageResumeService.StopAsync();
    }

    /// <inheritdoc/>
    public async Task CheckAsync()
    {
        if (isChecking)
        {
            // Visible and online often arrive together: one check covers both.
            return;
        }

        isChecking = true;
        try
        {
            if (await authService.CheckSessionAsync())
            {
                SessionResumed?.Invoke(this, EventArgs.Empty);
            }
        }
        finally
        {
            isChecking = false;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (isStarted)
        {
            pageResumeService.Resumed -= OnResumed;
            isStarted = false;
        }
    }

    private async void OnResumed(object? sender, EventArgs e)
    {
        try
        {
            await CheckAsync();
        }
        catch (Exception)
        {
            // Event handlers must not throw; the next resume checks again.
        }
    }
}
