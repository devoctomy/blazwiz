using blazwiz.Core.Sync.Models;
using blazwiz.Portal.Application.Services;
using blazwiz.Portal.Models.Notes;

namespace blazwiz.Portal.Application.UnitTests.Services;

public class NoteTreeServiceTests
{
    private readonly NoteTreeService sut = new();

    // Work/          (folder)
    //   Projects/    (folder)
    //     plan.md
    //   agenda.md
    // Archive/       (folder)
    // b.md
    // A.md
    private readonly List<NoteEntry> entries =
    [
        Folder("work", "Work", null),
        Folder("projects", "Projects", "work"),
        Note("plan", "plan.md", "projects"),
        Note("agenda", "agenda.md", "work"),
        Folder("archive", "Archive", null),
        Note("b", "b.md", null),
        Note("a", "A.md", null),
    ];

    [Fact]
    public void GivenEntries_WhenGetChildren_ThenFoldersFirstThenByNameIgnoringCase()
    {
        // Act
        var result = sut.GetChildren(entries, null);

        // Assert
        Assert.Equal(["archive", "work", "a", "b"], result.Select(e => e.Id));
    }

    [Fact]
    public void GivenNestedFolders_WhenGetDescendantIds_ThenReturnsAllLevels()
    {
        // Act
        var result = sut.GetDescendantIds(entries, "work");

        // Assert
        Assert.Equal(["agenda", "plan", "projects"], result.Order());
    }

    [Fact]
    public void GivenEmptyFolder_WhenGetDescendantIds_ThenReturnsEmpty()
    {
        // Act & Assert
        Assert.Empty(sut.GetDescendantIds(entries, "archive"));
    }

    [Fact]
    public void GivenNestedNote_WhenGetFolderPath_ThenReturnsOutermostFirst()
    {
        // Act
        var result = sut.GetFolderPath(entries, entries.Single(e => e.Id == "plan"));

        // Assert
        Assert.Equal(["Work", "Projects"], result);
    }

    [Fact]
    public void GivenTopLevelNote_WhenGetFolderPath_ThenReturnsEmpty()
    {
        // Act & Assert
        Assert.Empty(sut.GetFolderPath(entries, entries.Single(e => e.Id == "a")));
    }

    [Fact]
    public void GivenCollapsedFolders_WhenBuildVisibleItems_ThenOnlyTopLevelIsShown()
    {
        // Act
        var result = sut.BuildVisibleItems(entries, new HashSet<string>());

        // Assert
        Assert.Equal(["archive", "work", "a", "b"], result.Select(i => i.Entry.Id));
        Assert.All(result, i => Assert.Equal(0, i.Depth));
        Assert.Equal(2, result.Single(i => i.Entry.Id == "work").ChildCount);
        Assert.False(result.Single(i => i.Entry.Id == "work").IsExpanded);
    }

    [Fact]
    public void GivenExpandedFolders_WhenBuildVisibleItems_ThenChildrenFollowTheirFolderIndented()
    {
        // Act
        var result = sut.BuildVisibleItems(entries, new HashSet<string> { "work", "projects" });

        // Assert
        Assert.Equal(["archive", "work", "projects", "plan", "agenda", "a", "b"], result.Select(i => i.Entry.Id));
        Assert.Equal([0, 0, 1, 2, 1, 0, 0], result.Select(i => i.Depth));
        Assert.True(result.Single(i => i.Entry.Id == "work").IsExpanded);
    }

    [Fact]
    public void GivenExpandedFolderInsideCollapsedFolder_WhenBuildVisibleItems_ThenItsChildrenAreHidden()
    {
        // Act
        var result = sut.BuildVisibleItems(entries, new HashSet<string> { "projects" });

        // Assert
        Assert.DoesNotContain(result, i => i.Entry.Id == "plan");
    }

    [Fact]
    public void GivenFolder_WhenBuildMoveTargets_ThenExcludesItselfAndItsDescendants()
    {
        // Act
        var result = sut.BuildMoveTargets(entries, "work", "All notes");

        // Assert
        Assert.Equal([string.Empty, "archive"], result.Select(o => o.Id));
        Assert.Equal("All notes", result[0].Name);
    }

    [Fact]
    public void GivenNote_WhenBuildMoveTargets_ThenListsEveryFolderInTreeOrderWithDepth()
    {
        // Act
        var result = sut.BuildMoveTargets(entries, "a", "All notes");

        // Assert
        Assert.Equal([string.Empty, "archive", "work", "projects"], result.Select(o => o.Id));
        Assert.Equal([0, 1, 1, 2], result.Select(o => o.Depth));
    }

    [Fact]
    public void GivenFiles_WhenBuildEntries_ThenNotesMarkersAndImpliedFoldersBecomeEntries()
    {
        // Arrange
        var older = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var newer = older.AddDays(1);
        var files = new[]
        {
            new SyncFileInfo { Key = "notes/Work/Meetings/Agenda.md", ModifiedAt = newer },
            new SyncFileInfo { Key = "notes/Work/", ModifiedAt = older },
            new SyncFileInfo { Key = "notes/Empty/", ModifiedAt = older },
            new SyncFileInfo { Key = "notes/Top.md", ModifiedAt = older },
            new SyncFileInfo { Key = "notes/image.png", ModifiedAt = older },
            new SyncFileInfo { Key = "notes/", ModifiedAt = older },
            new SyncFileInfo { Key = "other/a.md", ModifiedAt = older },
        };

        // Act
        var result = sut.BuildEntries(files, "bob").ToDictionary(e => e.Id);

        // Assert
        Assert.Equal(["notes/Empty/", "notes/Top.md", "notes/Work/", "notes/Work/Meetings/", "notes/Work/Meetings/Agenda.md"], result.Keys.Order(StringComparer.Ordinal));
        var agenda = result["notes/Work/Meetings/Agenda.md"];
        Assert.Equal(NoteEntryKind.Note, agenda.Kind);
        Assert.Equal("Agenda.md", agenda.Name);
        Assert.Equal("notes/Work/Meetings/", agenda.ParentId);
        Assert.Equal("bob", agenda.Owner);
        var meetings = result["notes/Work/Meetings/"];
        Assert.True(meetings.IsFolder);
        Assert.Equal("notes/Work/", meetings.ParentId);
        Assert.Null(result["notes/Work/"].ParentId);
        Assert.Equal(newer, result["notes/Work/"].ModifiedAt);
    }

    [Theory]
    [InlineData(null, "Ideas.md", false, "notes/Ideas.md")]
    [InlineData("notes/Work/", "Ideas.md", false, "notes/Work/Ideas.md")]
    [InlineData("notes/Work/", "Meetings", true, "notes/Work/Meetings/")]
    public void GivenParentAndName_WhenGetChildKey_ThenKeyIsPath(string? parent, string name, bool isFolder, string expected)
    {
        Assert.Equal(expected, sut.GetChildKey(parent, name, isFolder));
    }

    [Theory]
    [InlineData("notes/Work/Ideas.md", "Ideas.md", "notes/Work/")]
    [InlineData("notes/Work/Meetings/", "Meetings", "notes/Work/")]
    [InlineData("notes/Top.md", "Top.md", null)]
    [InlineData("notes/Work/", "Work", null)]
    public void GivenKey_WhenGetNameAndParent_ThenReturnsLastPartAndContainingFolder(string key, string name, string? parent)
    {
        Assert.Equal(name, sut.GetName(key));
        Assert.Equal(parent, sut.GetParentKey(key));
    }

    [Fact]
    public void GivenKey_WhenGetFolderNames_ThenOutermostFirst()
    {
        Assert.Equal(["Work", "Meetings"], sut.GetFolderNames("notes/Work/Meetings/Agenda.md"));
        Assert.Empty(sut.GetFolderNames("notes/Top.md"));
    }

    [Theory]
    [InlineData("notes/a.md", true)]
    [InlineData("notes/Work/A.MD", true)]
    [InlineData("notes/Work/", false)]
    [InlineData("notes/.md", false)]
    [InlineData("notes/a.txt", false)]
    [InlineData("other/a.md", false)]
    public void GivenKey_WhenIsNoteKey_ThenOnlyMarkdownUnderTheRoot(string key, bool expected)
    {
        Assert.Equal(expected, sut.IsNoteKey(key));
    }

    private static NoteEntry Folder(string id, string name, string? parentId) =>
        new() { Id = id, Name = name, ParentId = parentId, Kind = NoteEntryKind.Folder };

    private static NoteEntry Note(string id, string name, string? parentId) =>
        new() { Id = id, Name = name, ParentId = parentId, Kind = NoteEntryKind.Note };
}
