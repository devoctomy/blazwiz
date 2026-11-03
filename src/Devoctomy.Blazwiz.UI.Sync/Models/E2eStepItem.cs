using Devoctomy.Blazwiz.Encryption.Models;

namespace Devoctomy.Blazwiz.UI.Sync.Models;

/// <summary>
/// One row of the step list shown while end to end encryption is turned on or off.
/// </summary>
public class E2eStepItem
{
    /// <summary>
    /// Gets or sets the step.
    /// </summary>
    public E2eStep Step { get; set; }

    /// <summary>
    /// Gets or sets what the step does, e.g. "Encrypt your files".
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets how far it has got.
    /// </summary>
    public E2eStepState State { get; set; }

    /// <summary>
    /// Gets or sets extra detail, e.g. "3 of 10", or null.
    /// </summary>
    public string? Detail { get; set; }
}
