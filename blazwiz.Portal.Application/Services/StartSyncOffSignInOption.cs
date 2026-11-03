using blazwiz.Core.Sync.Interfaces;
using blazwiz.Portal.Application.Shell.Interfaces;

namespace blazwiz.Portal.Application.Services;

/// <summary>
/// The sign-in option to start with sync turned off, so the first sync after signing in makes no network calls.
/// </summary>
public class StartSyncOffSignInOption(IConnectivityService connectivityService)
    : ISignInOption
{
    private readonly IConnectivityService connectivityService = connectivityService ?? throw new ArgumentNullException(nameof(connectivityService));

    /// <summary>
    /// Gets or sets a value indicating whether to sign in with sync turned off.
    /// </summary>
    public bool StartWithSyncOff { get; set; }

    /// <inheritdoc/>
    public bool IsSet => StartWithSyncOff;

    /// <inheritdoc/>
    public async Task LoadAsync()
    {
        await connectivityService.InitializeAsync();
        StartWithSyncOff = !connectivityService.IsSyncEnabled;
    }

    /// <inheritdoc/>
    public async Task ApplyAsync()
    {
        // Before the first sync after sign in starts, so sync turned off makes no network calls at all.
        if (StartWithSyncOff == connectivityService.IsSyncEnabled)
        {
            await connectivityService.SetSyncEnabledAsync(!StartWithSyncOff);
        }
    }
}
