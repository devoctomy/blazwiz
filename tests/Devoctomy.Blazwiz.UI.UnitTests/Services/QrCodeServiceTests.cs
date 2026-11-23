using System.Text;
using Devoctomy.Blazwiz.UI.Services;

namespace Devoctomy.Blazwiz.UI.UnitTests.Services;

public class QrCodeServiceTests
{
    [Fact]
    public void GivenText_WhenCreateImageUrl_ThenAnSvgDataUrl()
    {
        // Act
        var url = new QrCodeService().CreateImageUrl("otpauth://totp/App:bob?secret=JBSWY3DPEHPK3PXP&issuer=App");

        // Assert
        const string Prefix = "data:image/svg+xml;base64,";
        Assert.StartsWith(Prefix, url, StringComparison.Ordinal);
        var svg = Encoding.UTF8.GetString(Convert.FromBase64String(url[Prefix.Length..]));
        Assert.Contains("<svg", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenDifferentText_WhenCreateImageUrl_ThenDifferentImages()
    {
        // Arrange
        var sut = new QrCodeService();

        // Act
        var first = sut.CreateImageUrl("one");
        var second = sut.CreateImageUrl("two");

        // Assert
        Assert.NotEqual(first, second);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void GivenNoText_WhenCreateImageUrl_ThenThrowsArgumentException(string? text)
    {
        // Act & Assert
        Assert.ThrowsAny<ArgumentException>(() => new QrCodeService().CreateImageUrl(text!));
    }
}
