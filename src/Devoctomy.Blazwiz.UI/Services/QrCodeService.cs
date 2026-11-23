using System.Text;
using Devoctomy.Blazwiz.UI.Services.Interfaces;
using QRCoder;

namespace Devoctomy.Blazwiz.UI.Services;

/// <summary>
/// <see cref="IQrCodeService"/> using the QRCoder library, drawing SVG so it stays sharp at any size and needs no
/// JavaScript.
/// </summary>
public class QrCodeService : IQrCodeService
{
    // Pixels per module: the image scales with CSS, so this only sets the SVG's own coordinates.
    private const int ModuleSize = 4;

    /// <inheritdoc/>
    public string CreateImageUrl(string text)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        var svg = new SvgQRCode(data).GetGraphic(ModuleSize);
        return "data:image/svg+xml;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes(svg));
    }
}
