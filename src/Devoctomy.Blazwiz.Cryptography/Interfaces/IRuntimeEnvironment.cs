namespace Devoctomy.Blazwiz.Cryptography.Interfaces;

/// <summary>
/// Says where the code is running, for the services that must behave differently in the browser.
/// </summary>
public interface IRuntimeEnvironment
{
    /// <summary>
    /// Gets a value indicating whether the code is running in the browser (WebAssembly), where there is one thread.
    /// </summary>
    bool IsBrowser { get; }
}
