using Devoctomy.Blazwiz.Core.Exceptions;

namespace Devoctomy.Blazwiz.Encryption.Exceptions;

/// <summary>
/// Thrown when a new passphrase is shorter than the profile allows, with the code
/// <see cref="E2eErrorCodes.PassphraseTooShort"/> and the minimum length as its argument.
/// </summary>
public class E2ePassphraseTooShortException : BlazwizException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="E2ePassphraseTooShortException"/> class.
    /// </summary>
    /// <param name="minimumLength">The shortest passphrase allowed.</param>
    public E2ePassphraseTooShortException(int minimumLength)
        : base(E2eErrorCodes.PassphraseTooShort, minimumLength)
    {
    }
}
