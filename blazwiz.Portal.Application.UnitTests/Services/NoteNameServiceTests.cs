using blazwiz.Portal.Application.Services;

namespace blazwiz.Portal.Application.UnitTests.Services;

public class NoteNameServiceTests
{
    private readonly NoteNameService sut = new();

    [Fact]
    public void GivenService_WhenGettingNameLimits_ThenMarkdownExtensionAndOneHundredCharacters()
    {
        Assert.Equal(".md", sut.NoteExtension);
        Assert.Equal(100, sut.MaxNameLength);
    }

    [Theory]
    [InlineData("Shopping list")]
    [InlineData("  padded  ")]
    [InlineData("notes.md")]
    [InlineData("2026 plans (draft)")]
    public void GivenValidName_WhenValidate_ThenReturnsNull(string name)
    {
        // Act & Assert
        Assert.Null(sut.Validate(name));
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
        Assert.Equal("Please enter a name.", sut.Validate(name));
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
    [InlineData("issue #4")]
    [InlineData("100%")]
    [InlineData("{draft}")]
    [InlineData("a^b")]
    [InlineData("~home")]
    [InlineData("[todo]")]
    [InlineData("back`tick")]
    public void GivenInvalidCharacters_WhenValidate_ThenReturnsError(string name)
    {
        // Act
        var result = sut.Validate(name);

        // Assert
        Assert.NotNull(result);
        Assert.StartsWith("Names cannot contain", result);
    }

    [Fact]
    public void GivenTooLongName_WhenValidate_ThenReturnsError()
    {
        // Act
        var result = sut.Validate(new string('a', sut.MaxNameLength + 1));

        // Assert
        Assert.Equal($"Names can be at most {sut.MaxNameLength} characters.", result);
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
        Assert.Equal(expected, sut.ToNoteFileName(name));
    }

    [Theory]
    [InlineData("todo.md", "todo")]
    [InlineData("todo.MD", "todo")]
    [InlineData("todo", "todo")]
    public void GivenFileName_WhenWithoutExtension_ThenRemovesMarkdownExtension(string fileName, string expected)
    {
        // Act & Assert
        Assert.Equal(expected, sut.WithoutExtension(fileName));
    }
}
