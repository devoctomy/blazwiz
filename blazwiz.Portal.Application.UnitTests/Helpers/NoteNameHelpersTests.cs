using blazwiz.Portal.Application.Helpers;

namespace blazwiz.Portal.Application.UnitTests.Helpers;

public class NoteNameHelpersTests
{
    [Theory]
    [InlineData("Shopping list")]
    [InlineData("  padded  ")]
    [InlineData("notes.md")]
    [InlineData("2026 plans (draft)")]
    public void GivenValidName_WhenValidate_ThenReturnsNull(string name)
    {
        // Act & Assert
        Assert.Null(NoteNameHelpers.Validate(name));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".")]
    [InlineData("..")]
    public void GivenEmptyName_WhenValidate_ThenAsksForName(string? name)
    {
        // Act & Assert
        Assert.Equal("Please enter a name.", NoteNameHelpers.Validate(name));
    }

    [Theory]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("what?")]
    [InlineData("a:b")]
    [InlineData("a*b")]
    [InlineData("\"quoted\"")]
    [InlineData("<tag>")]
    [InlineData("a|b")]
    [InlineData("tab\there")]
    public void GivenInvalidCharacters_WhenValidate_ThenReturnsError(string name)
    {
        // Act
        var result = NoteNameHelpers.Validate(name);

        // Assert
        Assert.NotNull(result);
        Assert.StartsWith("Names cannot contain", result);
    }

    [Fact]
    public void GivenTooLongName_WhenValidate_ThenReturnsError()
    {
        // Act
        var result = NoteNameHelpers.Validate(new string('a', NoteNameHelpers.MaxNameLength + 1));

        // Assert
        Assert.Equal($"Names can be at most {NoteNameHelpers.MaxNameLength} characters.", result);
    }

    [Theory]
    [InlineData("todo", "todo.md")]
    [InlineData("  todo  ", "todo.md")]
    [InlineData("todo.md", "todo.md")]
    [InlineData("todo.MD", "todo.md")]
    [InlineData("todo.txt", "todo.txt.md")]
    public void GivenName_WhenToNoteFileName_ThenHasMarkdownExtension(string name, string expected)
    {
        // Act & Assert
        Assert.Equal(expected, NoteNameHelpers.ToNoteFileName(name));
    }

    [Theory]
    [InlineData("todo.md", "todo")]
    [InlineData("todo.MD", "todo")]
    [InlineData("todo", "todo")]
    public void GivenFileName_WhenWithoutExtension_ThenRemovesMarkdownExtension(string fileName, string expected)
    {
        // Act & Assert
        Assert.Equal(expected, NoteNameHelpers.WithoutExtension(fileName));
    }
}
