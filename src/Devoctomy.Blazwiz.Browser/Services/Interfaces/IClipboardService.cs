namespace Devoctomy.Blazwiz.Browser.Services.Interfaces;

/// <summary>
/// Copies text to the clipboard, optionally clearing it again after a while, for secrets such as passwords.
/// </summary>
public interface IClipboardService
{
    /// <summary>
    /// Copies text to the clipboard. With <paramref name="clearAfter"/>, the clipboard is cleared once that time has
    /// passed, unless something else has been copied since (when the browser lets the page see that), and a later copy
    /// replaces any clear still waiting. If the page does not have focus then, it is cleared when it has focus again.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="clearAfter">How long until the clipboard is cleared, or null to leave it.</param>
    /// <returns>A task that completes once the text is copied.</returns>
    Task CopyAsync(string text, TimeSpan? clearAfter = null);
}
