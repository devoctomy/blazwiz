using Devoctomy.Blazwiz.Testing;
using Devoctomy.Blazwiz.UI.Sync.Text;
using MyApp.Portal.Application.Text;

namespace MyApp.Portal.Application.UnitTests.Text;

/// <summary>
/// The notes example's wording (<see cref="NotesText"/>) overrides framework text, so it must keep matching it: after a
/// framework upgrade, these fail if one of its keys was renamed or removed, or its placeholders changed.
/// </summary>
public class NotesTextTests
{
    private readonly FrameworkText framework = new(typeof(SyncUiText));

    [Fact]
    public void GivenTheNotesOverrides_WhenComparedWithTheFramework_ThenEveryKeyIsFrameworkText()
    {
        // Act
        var unknown = framework.UnknownOverrideKeys(typeof(NotesText));

        // Assert
        Assert.Empty(unknown);
    }

    [Fact]
    public void GivenTheNotesOverrides_WhenComparedWithTheFramework_ThenTheirPlaceholdersMatch()
    {
        // Act
        var mismatched = framework.MismatchedOverridePlaceholders(typeof(NotesText));

        // Assert
        Assert.Empty(mismatched);
    }
}
