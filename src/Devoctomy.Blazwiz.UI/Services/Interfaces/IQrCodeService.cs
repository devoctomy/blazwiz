namespace Devoctomy.Blazwiz.UI.Services.Interfaces;

/// <summary>
/// Draws QR codes, e.g. for setting up an authenticator app from another screen.
/// </summary>
public interface IQrCodeService
{
    /// <summary>
    /// Draws a QR code holding some text, as an image to show in an <c>img</c> element.
    /// </summary>
    /// <param name="text">The text, e.g. an <c>otpauth://</c> link.</param>
    /// <returns>The image as a <c>data:image/svg+xml</c> URL.</returns>
    string CreateImageUrl(string text);
}
