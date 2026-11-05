using Blazing.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Devoctomy.Blazwiz.Core.Text.Interfaces;
using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.UI.Sync.Text;
using Microsoft.Extensions.Localization;

namespace Devoctomy.Blazwiz.UI.Sync.ViewModels;

/// <summary>
/// ViewModel for the lock indicator beside the sync status pill: hidden while nothing is unlocked, otherwise how many
/// profiles are, with Lock all in its menu.
/// </summary>
public partial class E2eLockIndicatorViewModel : ViewModelBase
{
    private readonly IE2eProtectedProfileManager manager;
    private readonly ITextPlurals plurals;
    private readonly IStringLocalizer<SyncUiText> text;
    private bool isMenuOpen;

    /// <summary>
    /// Initializes a new instance of the <see cref="E2eLockIndicatorViewModel"/> class.
    /// </summary>
    /// <param name="manager">Says which profiles are unlocked, and locks them.</param>
    /// <param name="plurals">Picks the plural form of the text.</param>
    /// <param name="text">The sync UI's text.</param>
    public E2eLockIndicatorViewModel(IE2eProtectedProfileManager manager, ITextPlurals plurals, IStringLocalizer<SyncUiText> text)
    {
        this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
        this.plurals = plurals ?? throw new ArgumentNullException(nameof(plurals));
        this.text = text ?? throw new ArgumentNullException(nameof(text));
        this.manager.LockStateChanged += OnLockStateChanged;
    }

    /// <summary>
    /// Gets how many profiles are unlocked.
    /// </summary>
    public int UnlockedCount => manager.Unlocked.Count;

    /// <summary>
    /// Gets a value indicating whether the indicator is shown: something is unlocked.
    /// </summary>
    public bool IsVisible => UnlockedCount > 0;

    /// <summary>
    /// Gets the indicator's text, e.g. "2 unlocked".
    /// </summary>
    public string Text => plurals.Format(text, "Sync.E2e.Indicator.Unlocked", UnlockedCount);

    /// <summary>
    /// Gets a value indicating whether its menu is open.
    /// </summary>
    public bool IsMenuOpen
    {
        get => isMenuOpen;
        private set => SetProperty(ref isMenuOpen, value);
    }

    /// <summary>
    /// Opens or closes the menu.
    /// </summary>
    [RelayCommand]
    public void ToggleMenu()
    {
        IsMenuOpen = !IsMenuOpen;
    }

    /// <summary>
    /// Closes the menu.
    /// </summary>
    [RelayCommand]
    public void CloseMenu()
    {
        IsMenuOpen = false;
    }

    /// <summary>
    /// Locks every profile, and closes the menu.
    /// </summary>
    [RelayCommand]
    public void LockAll()
    {
        IsMenuOpen = false;
        manager.LockAll();
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            manager.LockStateChanged -= OnLockStateChanged;
        }

        base.Dispose(disposing);
    }

    private void OnLockStateChanged(object? sender, E2eKeyScope scope)
    {
        if (!IsVisible)
        {
            IsMenuOpen = false;
        }

        OnPropertyChanged(nameof(UnlockedCount));
        OnPropertyChanged(nameof(IsVisible));
        OnPropertyChanged(nameof(Text));
    }
}
