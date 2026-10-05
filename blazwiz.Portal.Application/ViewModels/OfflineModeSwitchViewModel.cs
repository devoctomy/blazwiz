using Blazing.Mvvm.ComponentModel;
using blazwiz.Core.Offline.Interfaces;
using blazwiz.Portal.Application.Services.Interfaces;
using CommunityToolkit.Mvvm.Input;

namespace blazwiz.Portal.Application.ViewModels;

/// <summary>
/// ViewModel for the offline mode switch. Turning it on makes the session an offline one lasting 15 minutes; turning
/// it off ends that session, so the user signs in online again.
/// </summary>
public partial class OfflineModeSwitchViewModel : ViewModelBase
{
    private readonly IOfflineModeService offlineModeService;
    private readonly IAuthenticationService authService;

    private bool isWorking;
    private string? error;

    /// <summary>
    /// Initializes a new instance of the <see cref="OfflineModeSwitchViewModel"/> class.
    /// </summary>
    /// <param name="offlineModeService">Offline mode.</param>
    /// <param name="authService">Turns the session into an offline one.</param>
    public OfflineModeSwitchViewModel(IOfflineModeService offlineModeService, IAuthenticationService authService)
    {
        this.offlineModeService = offlineModeService ?? throw new ArgumentNullException(nameof(offlineModeService));
        this.authService = authService ?? throw new ArgumentNullException(nameof(authService));
        this.offlineModeService.Changed += OnChanged;
    }

    /// <summary>
    /// Gets a value indicating whether offline mode is on.
    /// </summary>
    public bool IsOffline => offlineModeService.IsOffline;

    /// <summary>
    /// Gets a value indicating whether the switch is changing offline mode.
    /// </summary>
    public bool IsWorking
    {
        get => isWorking;
        private set => SetProperty(ref isWorking, value);
    }

    /// <summary>
    /// Gets why offline mode could not be turned on, or null.
    /// </summary>
    public string? Error
    {
        get => error;
        private set => SetProperty(ref error, value);
    }

    /// <summary>
    /// Turns offline mode on or off.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task ToggleAsync()
    {
        if (IsWorking)
        {
            return;
        }

        Error = null;
        if (offlineModeService.IsOffline)
        {
            // Ends the offline session: the app returns to the login page.
            offlineModeService.Exit();
            return;
        }

        IsWorking = true;
        try
        {
            await authService.GoOfflineAsync();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            OnPropertyChanged(nameof(IsOffline));
        }
        finally
        {
            IsWorking = false;
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            offlineModeService.Changed -= OnChanged;
        }

        base.Dispose(disposing);
    }

    private void OnChanged(object? sender, bool isOffline)
    {
        OnPropertyChanged(nameof(IsOffline));
    }
}
