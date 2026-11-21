using Devoctomy.Blazwiz.UI.Sync.ViewModels;
using Microsoft.AspNetCore.Components;

namespace Devoctomy.Blazwiz.UI.Sync.Components;

/// <summary>
/// The Argon2id settings as fields, with the button that benchmarks them on this device. It shows the view model it is
/// given, so whoever owns that reads the values entered: the benchmark panel in Settings, and the Advanced tab of
/// <see cref="E2ePassphraseForm"/>. It is shown again whenever the view showing it is, so that view follows the view
/// model. Logic lives in <see cref="Argon2idBenchmarkViewModel"/>.
/// </summary>
public partial class Argon2idEditor : ComponentBase
{
    /// <summary>
    /// Gets or sets the settings shown and benchmarked.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public Argon2idBenchmarkViewModel Model { get; set; } = default!;

    /// <summary>
    /// Gets or sets what every element id here starts with, so two editors on one page do not share ids.
    /// </summary>
    [Parameter]
    public string IdPrefix { get; set; } = "argon2id";

    /// <summary>
    /// Gets or sets a value indicating whether the fields and the button are disabled, e.g. while the settings are in
    /// use.
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the fields are laid out for a narrow place (a dialog): two to a row.
    /// </summary>
    [Parameter]
    public bool Compact { get; set; }
}
