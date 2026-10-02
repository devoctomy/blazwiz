using blazwiz.Core.Sync;

namespace blazwiz.Core.UnitTests.Sync;

public class GlobSyncPathMatcherTests
{
    [Theory]
    [InlineData("notes/**/*.md", "notes/")]
    [InlineData("/notes/**/*.md", "notes/")]
    [InlineData("notes/a*.md", "notes/")]
    [InlineData("notes/Work/*.md", "notes/Work/")]
    [InlineData("*.md", "")]
    [InlineData("notes/readme.md", "notes/")]
    public void GivenPattern_WhenConstructed_ThenPrefixIsLiteralPartBeforeFirstWildcard(string pattern, string expected)
    {
        // Act
        var sut = new GlobSyncPathMatcher(pattern, false);

        // Assert
        Assert.Equal(expected, sut.Prefix);
    }

    [Theory]
    [InlineData("notes/a.md", true)]
    [InlineData("notes/Work/a.md", false)]
    [InlineData("notes/a.txt", false)]
    [InlineData("other/a.md", false)]
    public void GivenSingleStar_WhenIsMatch_ThenDoesNotCrossSlash(string key, bool expected)
    {
        // Arrange
        var sut = new GlobSyncPathMatcher("notes/*.md", false);

        // Act & Assert
        Assert.Equal(expected, sut.IsMatch(key));
    }

    [Theory]
    [InlineData("notes/a.md", true)]
    [InlineData("notes/Work/a.md", true)]
    [InlineData("notes/Work/Meetings/2026-10-01.md", true)]
    [InlineData("notes/Work/a.MD", false)]
    [InlineData("Notes/a.md", false)]
    [InlineData("notes/Work/", false)]
    [InlineData("", false)]
    [InlineData("/notes/a.md", false)]
    public void GivenDoubleStar_WhenIsMatch_ThenCrossesSlashCaseSensitively(string key, bool expected)
    {
        // Arrange
        var sut = new GlobSyncPathMatcher("notes/**/*.md", false);

        // Act & Assert
        Assert.Equal(expected, sut.IsMatch(key));
    }

    [Theory]
    [InlineData("notes/Work/", true)]
    [InlineData("notes/Work/Meetings/", true)]
    [InlineData("notes/", false)]
    [InlineData("other/Work/", false)]
    public void GivenEmptyFoldersIncluded_WhenIsMatch_ThenFolderMarkersUnderPrefixMatch(string key, bool expected)
    {
        // Arrange
        var sut = new GlobSyncPathMatcher("notes/**/*.md", true);

        // Act & Assert
        Assert.Equal(expected, sut.IsMatch(key));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void GivenNoPattern_WhenConstructing_ThenThrows(string? pattern)
    {
        Assert.ThrowsAny<ArgumentException>(() => new GlobSyncPathMatcher(pattern!, false));
    }
}
