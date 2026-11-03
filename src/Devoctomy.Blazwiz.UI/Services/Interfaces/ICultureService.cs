using System.Globalization;

namespace Devoctomy.Blazwiz.UI.Services.Interfaces;

/// <summary>
/// The language the app is shown in, from the cultures the app supports (<c>TextBuilder.SupportedCultures</c>, or
/// <c>Text:SupportedCultures</c> in configuration). The user's choice is kept on the device.
/// </summary>
public interface ICultureService
{
    /// <summary>
    /// Gets the cultures the app supports, the first being the default. Empty when the app only has its neutral text.
    /// </summary>
    IReadOnlyList<CultureInfo> SupportedCultures { get; }

    /// <summary>
    /// Gets the culture the app is shown in.
    /// </summary>
    CultureInfo CurrentCulture { get; }

    /// <summary>
    /// Applies the language: the one the user chose, else the browser's when the app supports it, else the app's
    /// default. Call once at startup, before the app runs (resources for the culture are loaded as it starts).
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task ApplySavedCultureAsync();

    /// <summary>
    /// Keeps the user's choice and reloads the app in that language.
    /// </summary>
    /// <param name="name">The culture's name, one of <see cref="SupportedCultures"/>.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">The app does not support the culture.</exception>
    Task ChangeCultureAsync(string name);
}
