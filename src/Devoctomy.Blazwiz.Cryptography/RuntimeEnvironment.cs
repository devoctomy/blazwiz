using Devoctomy.Blazwiz.Cryptography.Interfaces;

namespace Devoctomy.Blazwiz.Cryptography;

/// <summary>
/// Implementation of <see cref="IRuntimeEnvironment"/> that asks the operating system.
/// </summary>
public class RuntimeEnvironment : IRuntimeEnvironment
{
    /// <inheritdoc/>
    public bool IsBrowser => OperatingSystem.IsBrowser();
}
