using MyApp.Portal.Application.Services;
using MyApp.Portal.Models.Notes;

namespace MyApp.Portal.Application.UnitTests.Services;

/// <summary>
/// Tests use « and » to mark the selection before and after formatting, e.g. "say «hello»" selects "hello".
/// A caret with nothing selected is written as «».
/// </summary>
public class MarkdownFormatServiceTests
{
    private readonly MarkdownFormatService sut = new();

    [Theory]
    [InlineData("say «hello» there", "say **«hello»** there")]
    [InlineData("say «hello »there", "say **«hello»** there")]
    [InlineData("say **«hello»** there", "say «hello» there")]
    [InlineData("say «**hello**» there", "say «hello» there")]
    [InlineData("say «»", "say **«bold text»**")]
    public void GivenSelection_WhenBold_ThenTogglesMarkers(string before, string expected)
    {
        // Act & Assert
        Assert.Equal(expected, Format(before, MarkdownFormat.Bold));
    }

    [Theory]
    [InlineData("«word»", "_«word»_")]
    [InlineData("_«word»_", "«word»")]
    [InlineData("«»", "_«italic text»_")]
    public void GivenSelection_WhenItalic_ThenTogglesUnderscores(string before, string expected)
    {
        // Act & Assert
        Assert.Equal(expected, Format(before, MarkdownFormat.Italic));
    }

    [Theory]
    [InlineData("«old»", "~~«old»~~")]
    [InlineData("~~«old»~~", "«old»")]
    public void GivenSelection_WhenStrikethrough_ThenTogglesTildes(string before, string expected)
    {
        // Act & Assert
        Assert.Equal(expected, Format(before, MarkdownFormat.Strikethrough));
    }

    [Theory]
    [InlineData("run «dotnet test»", "run `«dotnet test»`")]
    [InlineData("run `«dotnet test»`", "run «dotnet test»")]
    public void GivenSelection_WhenInlineCode_ThenTogglesBackticks(string before, string expected)
    {
        // Act & Assert
        Assert.Equal(expected, Format(before, MarkdownFormat.InlineCode));
    }

    [Theory]
    [InlineData("Ti«»tle", MarkdownFormat.Heading1, "# Title«»")]
    [InlineData("# Ti«»tle", MarkdownFormat.Heading1, "Title«»")]
    [InlineData("# Ti«»tle", MarkdownFormat.Heading2, "## Title«»")]
    [InlineData("### Ti«»tle", MarkdownFormat.Heading3, "Title«»")]
    [InlineData("before\nTi«»tle\nafter", MarkdownFormat.Heading2, "before\n## Title«»\nafter")]
    [InlineData("###### Ti«»tle", MarkdownFormat.Heading1, "# Title«»")]
    [InlineData("#\tTi«»tle", MarkdownFormat.Heading1, "Title«»")]
    [InlineData("#Ti«»tle", MarkdownFormat.Heading1, "# #Title«»")]
    [InlineData("####### Ti«»tle", MarkdownFormat.Heading1, "# ####### Title«»")]
    public void GivenCaretInLine_WhenHeading_ThenSetsOrRemovesLevelForWholeLine(string before, MarkdownFormat format, string expected)
    {
        // Act & Assert
        Assert.Equal(expected, Format(before, format));
    }

    [Theory]
    [InlineData("«one\ntwo»", "«- one\n- two»")]
    [InlineData("«- one\n- two»", "«one\ntwo»")]
    [InlineData("«one\n\ntwo»", "«- one\n\n- two»")]
    [InlineData("o«n»e", "«- one»")]
    [InlineData("«1. one\n2. two»", "«- one\n- two»")]
    [InlineData("«  indented»", "«  - indented»")]
    [InlineData("«* one\n+ two»", "«one\ntwo»")]
    [InlineData("«  - one\n  - two»", "«  one\n  two»")]
    [InlineData("«- [ ] milk»", "«- milk»")]
    [InlineData("«-one»", "«- -one»")]
    public void GivenLines_WhenBulletList_ThenTogglesBullets(string before, string expected)
    {
        // Act & Assert
        Assert.Equal(expected, Format(before, MarkdownFormat.BulletList));
    }

    [Theory]
    [InlineData("«one\ntwo\nthree»", "«1. one\n2. two\n3. three»")]
    [InlineData("«1. one\n2. two»", "«one\ntwo»")]
    [InlineData("«- one\n- two»", "«1. one\n2. two»")]
    [InlineData("«1) one\n2) two»", "«one\ntwo»")]
    [InlineData("«10. ten»", "«ten»")]
    [InlineData("«- [x] done»", "«1. done»")]
    [InlineData("«1.one»", "«1. 1.one»")]
    public void GivenLines_WhenNumberedList_ThenNumbersEachLine(string before, string expected)
    {
        // Act & Assert
        Assert.Equal(expected, Format(before, MarkdownFormat.NumberedList));
    }

    [Theory]
    [InlineData("«milk\neggs»", "«- [ ] milk\n- [ ] eggs»")]
    [InlineData("«- [ ] milk\n- [x] eggs»", "«milk\neggs»")]
    [InlineData("«- milk»", "«- [ ] milk»")]
    [InlineData("«- [X] done»", "«done»")]
    [InlineData("«* [x] done»", "«done»")]
    [InlineData("«1. milk»", "«- [ ] milk»")]
    [InlineData("«- [] milk»", "«- [ ] [] milk»")]
    public void GivenLines_WhenTaskList_ThenTogglesCheckboxes(string before, string expected)
    {
        // Act & Assert
        Assert.Equal(expected, Format(before, MarkdownFormat.TaskList));
    }

    [Theory]
    [InlineData("«wise\nwords»", "«> wise\n> words»")]
    [InlineData("«> wise\n> words»", "«wise\nwords»")]
    [InlineData("«>wise»", "«wise»")]
    [InlineData("«>> nested»", "«> nested»")]
    [InlineData("«> wise\nwords»", "«> > wise\n> words»")]
    [InlineData("«> wise\n\n> words»", "«wise\n\nwords»")]
    public void GivenLines_WhenQuote_ThenTogglesQuote(string before, string expected)
    {
        // Act & Assert
        Assert.Equal(expected, Format(before, MarkdownFormat.Quote));
    }

    [Fact]
    public void GivenSelectionEndingAfterLineBreak_WhenBulletList_ThenNextLineIsUnchanged()
    {
        // Act & Assert
        Assert.Equal("«- one»\ntwo", Format("«one\n»two", MarkdownFormat.BulletList));
    }

    [Theory]
    [InlineData("«var x = 1;»", "```\n«var x = 1;»\n```")]
    [InlineData("text «»", "text \n```\n«code»\n```")]
    [InlineData("«```\nvar x = 1;\n```»", "«var x = 1;»")]
    [InlineData("«```csharp\nvar x = 1;\n```»", "«var x = 1;»")]
    [InlineData("«```\none\n\ntwo\n```  »", "«one\n\ntwo»")]
    [InlineData("«```\nvar x = 1;```»", "«var x = 1;»")]
    [InlineData("«```\n```»", "«»")]
    [InlineData("«```\nunclosed»", "```\n«```\nunclosed»\n```")]
    [InlineData("«```x```»", "```\n«```x```»\n```")]
    public void GivenSelection_WhenCodeBlock_ThenTogglesFence(string before, string expected)
    {
        // Act & Assert
        Assert.Equal(expected, Format(before, MarkdownFormat.CodeBlock));
    }

    [Theory]
    [InlineData("see «docs»", "see [docs](«https://»)")]
    [InlineData("see «https://example.com»", "see [«link text»](https://example.com)")]
    [InlineData("see «»", "see [«link text»](https://)")]
    public void GivenSelection_WhenLink_ThenInsertsLinkSelectingThePartToType(string before, string expected)
    {
        // Act & Assert
        Assert.Equal(expected, Format(before, MarkdownFormat.Link));
    }

    [Theory]
    [InlineData("«»", "---\n\n«»")]
    [InlineData("above«»", "above\n\n---\n\n«»")]
    [InlineData("above\n«»", "above\n\n---\n\n«»")]
    [InlineData("above\n\n«»\nbelow", "above\n\n---\n«»\nbelow")]
    public void GivenCaret_WhenHorizontalRule_ThenInsertsRuleOnItsOwnLine(string before, string expected)
    {
        // Act & Assert
        Assert.Equal(expected, Format(before, MarkdownFormat.HorizontalRule));
    }

    [Fact]
    public void GivenReversedOrOutOfRangeSelection_WhenApply_ThenSelectionIsNormalised()
    {
        // Act
        var edit = sut.Apply("hello", 99, 0, MarkdownFormat.Bold);

        // Assert
        Assert.Equal("**hello**", edit.ApplyTo("hello"));
        Assert.Equal(2, edit.SelectionStart);
        Assert.Equal(7, edit.SelectionEnd);
    }

    [Fact]
    public void GivenLinesWithABlankLineBetween_WhenHeading_ThenTheBlankLineIsLeftAlone()
    {
        // Act & Assert
        Assert.Equal("«# one\n\n# two»", Format("«one\n\ntwo»", MarkdownFormat.Heading1));
        Assert.Equal("«one\n\ntwo»", Format("«# one\n\n# two»", MarkdownFormat.Heading1));
    }

    [Theory]
    [InlineData(MarkdownFormat.BulletList, "«»")]
    [InlineData(MarkdownFormat.Quote, "> «»")]
    public void GivenAnEmptyNote_WhenListOrQuote_ThenAListAddsNothingAndAQuoteAddsItsMarker(MarkdownFormat format, string expected)
    {
        // Act & Assert
        Assert.Equal(expected, Format("«»", format));
    }

    [Theory]
    [InlineData("«var x = 1;»\nmore", "```\n«var x = 1;»\n```\nmore")]
    [InlineData("«var x = 1;» more", "```\n«var x = 1;»\n```\n more")]
    public void GivenSelectionFollowedByText_WhenCodeBlock_ThenTheFenceEndsOnItsOwnLine(string before, string expected)
    {
        // Act & Assert
        Assert.Equal(expected, Format(before, MarkdownFormat.CodeBlock));
    }

    [Theory]
    [InlineData("see «http://example.com»", "see [«link text»](http://example.com)")]
    [InlineData("see «HTTPS://example.com»", "see [«link text»](HTTPS://example.com)")]
    public void GivenSelectedUrlInAnyCase_WhenLink_ThenItBecomesTheLinkTarget(string before, string expected)
    {
        // Act & Assert
        Assert.Equal(expected, Format(before, MarkdownFormat.Link));
    }

    [Fact]
    public void GivenUnknownFormat_WhenApply_ThenThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        Assert.Equal("format", Assert.Throws<ArgumentOutOfRangeException>(() => sut.Apply("text", 0, 0, (MarkdownFormat)999)).ParamName);
    }

    [Fact]
    public void GivenNullText_WhenApply_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => sut.Apply(null!, 0, 0, MarkdownFormat.Bold));
    }

    private string Format(string markedText, MarkdownFormat format)
    {
        var start = markedText.IndexOf('«');
        var end = markedText.IndexOf('»') - 1;
        var text = markedText.Replace("«", string.Empty).Replace("»", string.Empty);

        var edit = sut.Apply(text, start, end, format);
        var result = edit.ApplyTo(text);

        return result[..edit.SelectionStart] + "«" + result[edit.SelectionStart..edit.SelectionEnd] + "»" + result[edit.SelectionEnd..];
    }
}
