using Devoctomy.Blazwiz.Testing;
using Devoctomy.Blazwiz.UI.Sync.Text;
using MyApp.Portal.Application.Text;

namespace MyApp.Portal.Application.UnitTests.Text;

/// <summary>
/// The notes example's wording (<see cref="NotesText"/>) overrides framework text, so it must keep naming text the
/// framework has: after a framework upgrade, this fails if one of its keys was renamed or removed.
/// </summary>
public class NotesTextTests
{
    [Fact]
    public void GivenTheNotesOverrides_WhenComparedWithTheFramework_ThenEveryKeyIsFrameworkText()
    {
        // Act & Assert
        Assert.Empty(new FrameworkText(typeof(SyncUiText)).UnknownOverrideKeys(typeof(NotesText)));
    }
}
