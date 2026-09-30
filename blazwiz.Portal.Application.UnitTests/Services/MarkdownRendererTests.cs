using System.Text.RegularExpressions;
using blazwiz.Portal.Application.Services;

namespace blazwiz.Portal.Application.UnitTests.Services;

public class MarkdownRendererTests
{
    private readonly MarkdownRenderer sut = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GivenEmptyMarkdown_WhenToHtml_ThenReturnsEmpty(string? markdown)
    {
        Assert.Equal(string.Empty, sut.ToHtml(markdown));
    }

    [Fact]
    public void GivenMarkdown_WhenToHtml_ThenRendersHtml()
    {
        // Act
        var result = sut.ToHtml("# Title\n\nSome **bold** and _italic_ and ~~struck~~ text.");

        // Assert
        Assert.Contains(">Title</h1>", result);
        Assert.Contains("<strong>bold</strong>", result);
        Assert.Contains("<em>italic</em>", result);
        Assert.Contains("<del>struck</del>", result);
    }

    [Fact]
    public void GivenTableAndTaskList_WhenToHtml_ThenRendersExtensions()
    {
        // Act
        var result = sut.ToHtml("| a | b |\n| - | - |\n| 1 | 2 |\n\n- [x] done\n- [ ] todo");

        // Assert
        Assert.Contains("<table>", result);
        Assert.Contains("type=\"checkbox\"", result);
    }

    [Fact]
    public void GivenRawHtml_WhenToHtml_ThenHtmlIsEscaped()
    {
        // Act
        var result = sut.ToHtml("<script>alert(1)</script>\n\nHello <b onmouseover=\"x()\">there</b>");

        // Assert
        Assert.DoesNotContain("<script>", result);
        Assert.DoesNotContain("<b ", result);
        Assert.Contains("&lt;script&gt;", result);
    }

    [Theory]
    [InlineData("[click](javascript:alert(1))")]
    [InlineData("[click](JavaScript:alert(1))")]
    [InlineData("[click](java\tscript:alert(1))")]
    [InlineData("[click](data:text/html;base64,PHNjcmlwdD4=)")]
    [InlineData("[click](vbscript:msgbox)")]
    [InlineData("<javascript:alert(1)>")]
    public void GivenUnsafeLink_WhenToHtml_ThenUrlIsRemoved(string markdown)
    {
        // Act
        var result = sut.ToHtml(markdown);

        // Assert: any link that is rendered points nowhere.
        Assert.DoesNotMatch(new Regex("href=\"(?!#\")", RegexOptions.IgnoreCase), result);
    }

    [Theory]
    [InlineData("[site](https://example.com)", "href=\"https://example.com\"")]
    [InlineData("[mail](mailto:someone@example.com)", "href=\"mailto:someone@example.com\"")]
    [InlineData("[section](#heading)", "href=\"#heading\"")]
    [InlineData("[page](other/page.md)", "href=\"other/page.md\"")]
    [InlineData("[query](page?time=10:30)", "href=\"page?time=10:30\"")]
    public void GivenSafeLink_WhenToHtml_ThenUrlIsKept(string markdown, string expected)
    {
        Assert.Contains(expected, sut.ToHtml(markdown));
    }
}
