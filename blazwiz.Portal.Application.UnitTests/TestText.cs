using blazwiz.Core.Text;
using blazwiz.Core.Text.Interfaces;
using blazwiz.Portal.Application.Text;
using blazwiz.Portal.Application.Text.Interfaces;
using Microsoft.Extensions.Localization;

namespace blazwiz.Portal.Application.UnitTests;

/// <summary>
/// The real text resources, as the app gets them without overrides, so tests check the actual wording.
/// </summary>
public sealed class TestText
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestText"/> class.
    /// </summary>
    public TestText()
    {
        Options = new TextOptions();
        var factory = new OverridingStringLocalizerFactory(Options);
        Ui = new StringLocalizer<UiText>(factory);
        Sync = new StringLocalizer<SyncUiText>(factory);
        Plurals = new TextPlurals(Options);
        Errors = new ErrorText(factory, [new ErrorTextSource(typeof(SyncUiText))], Plurals);
    }

    /// <summary>
    /// Gets the options, e.g. to add overrides in a test.
    /// </summary>
    public TextOptions Options { get; }

    /// <summary>
    /// Gets the framework UI's text.
    /// </summary>
    public IStringLocalizer<UiText> Ui { get; }

    /// <summary>
    /// Gets the sync UI's text.
    /// </summary>
    public IStringLocalizer<SyncUiText> Sync { get; }

    /// <summary>
    /// Gets the plural forms.
    /// </summary>
    public ITextPlurals Plurals { get; }

    /// <summary>
    /// Gets the error descriptions.
    /// </summary>
    public IErrorText Errors { get; }
}
