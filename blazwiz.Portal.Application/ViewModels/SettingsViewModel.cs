using Blazing.Mvvm.ComponentModel;
using blazwiz.Portal.Application.Shell.Interfaces;

namespace blazwiz.Portal.Application.ViewModels;

/// <summary>
/// ViewModel for the Settings page: the sections the framework, its modules and the app add.
/// </summary>
public class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsSectionRegistry sectionRegistry;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsViewModel"/> class.
    /// </summary>
    /// <param name="sectionRegistry">The sections to show.</param>
    public SettingsViewModel(ISettingsSectionRegistry sectionRegistry)
    {
        this.sectionRegistry = sectionRegistry ?? throw new ArgumentNullException(nameof(sectionRegistry));
    }

    /// <summary>
    /// Gets the components showing each section, in order.
    /// </summary>
    public IReadOnlyList<Type> Sections => sectionRegistry.GetSections();
}
